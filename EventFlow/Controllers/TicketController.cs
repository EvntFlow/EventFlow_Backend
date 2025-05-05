using System.ComponentModel.DataAnnotations;
using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Controllers;

[Route("/api/[controller]")]
public class TicketController : ControllerBase
{
    private readonly TicketService _ticketService;
    private readonly AccountService _accountService;
    private readonly EventService _eventService;
    private readonly PaymentService _paymentService;

    public TicketController(
        TicketService ticketService,
        AccountService accountService,
        EventService eventService,
        PaymentService paymentService
    )
    {
        _ticketService = ticketService;
        _accountService = accountService;
        _eventService = eventService;
        _paymentService = paymentService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IAsyncEnumerable<Ticket>>> GetTickets()
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }

        var tickets = await _ticketService.GetTickets(userId).ToListAsync();
        var enumerable = tickets.ToAsyncEnumerable().Select(
            async (Ticket ticket, CancellationToken ct) =>
            {
                ticket.Event = await _eventService.GetEvent(
                    ticket.Event!.Id, includeCollections: false
                );
                return ticket;
            }
        );

        return Ok(enumerable);
    }

    [HttpDelete]
    [Authorize]
    public async Task<ActionResult> CancelTicket(
        [FromForm(Name = "ticket")] Guid ticketId,
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        try
        {
            var ticket = await _ticketService.GetTicket(ticketId);
            if (ticket is null)
            {
                return this.RedirectWithError(error: ErrorStrings.InvalidTicket);
            }

            var isCancelFromAttendee = await _accountService.IsValidAttendee(userId)
                && await _ticketService.IsTicketOwner(ticketId, userId);
            var isCancelFromOrganizer = !isCancelFromAttendee
                && await _accountService.IsValidOrganizer(userId)
                && await _ticketService.IsTicketOrganizer(ticketId, userId);
            var shouldCancel = isCancelFromAttendee || isCancelFromOrganizer;

            if (!shouldCancel)
            {
                return this.RedirectWithError(error: ErrorStrings.TicketNoAccess);
            }

            var attendeeId = ticket.Attendee.Id;
            var organizerId = ticket.Event!.Organizer.Id;

            var attendeePayment = await _paymentService.GetPaymentMethods(attendeeId).FirstAsync();
            var organizerPayment =
                await _paymentService.GetPaymentMethods(organizerId).FirstAsync();

            await _paymentService.PerformTransaction(
                fromPaymentMethodId: organizerPayment.Id,
                toPaymentMethodId: attendeePayment.Id,
                amount: ticket.Price
            );

            await _ticketService.DeleteTicket(ticketId);

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> CreateTicket(
        [FromForm(Name = "ticketOption"), Required] ICollection<Guid> ticketOptionId,
        [FromForm(Name = "fullName"), Required] string holderFullName,
        [FromForm(Name = "email"), EmailAddress, Required] string holderEmail,
        [FromForm(Name = "phoneNumber"), Phone, Required] string holderPhoneNumber,
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }
        if (!await _accountService.IsValidAttendee(userId))
        {
            return this.RedirectWithError(error: ErrorStrings.NotAnAttendee);
        }

        if (ticketOptionId.Count == 0)
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidTicketOption);
        }
        if (!await _ticketService.IsTicketOptionAvailable(ticketOptionId))
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidTicketOption);
        }

        var prices = await _eventService.GetPrice(ticketOptionId).ToDictionaryAsync();
        var totalPrice = prices.Values.Sum();

        if (totalPrice == 0)
        {
            // Do the actual purchase.
            var tickets = ticketOptionId.Select(id => new Ticket
            {
                Id = Guid.Empty,
                Attendee = new() { Id = userId },
                Event = null,
                TicketOption = new()
                {
                    Id = id,
                    Name = string.Empty,
                    AmountAvailable = 0,
                    AdditionalPrice = 0
                },
                Price = prices[id],
                IsReviewed = false,
                HolderFullName = holderFullName,
                HolderEmail = holderEmail,
                HolderPhoneNumber = holderPhoneNumber
            });
            await _ticketService.CreateTicket(tickets);

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        else
        {
            return this.RedirectToReferrerWithQuery(
                "/Ticket/FinishTicketPayment", [
                    .. Request.Form.SelectMany(kvp =>
                        kvp.Value.Select(v => new KeyValuePair<string, object?>(kvp.Key, v))
                    ),
                    new KeyValuePair<string, object?>(nameof(returnUri), returnUri),
                    new KeyValuePair<string, object?>(nameof(totalPrice), totalPrice),
                ]
            );
        }
    }

    [HttpPost(nameof(FinishTicketPayment))]
    [Authorize]
    public async Task<ActionResult> FinishTicketPayment(
        [FromForm(Name = "ticketOption"), Required] ICollection<Guid> ticketOptionId,
        [FromForm(Name = "fullName"), Required] string holderFullName,
        [FromForm(Name = "email"), EmailAddress, Required] string holderEmail,
        [FromForm(Name = "phoneNumber"), Phone, Required] string holderPhoneNumber,
        [FromForm(Name = "paymentMethod"), Required] Guid paymentMethodId,
        [FromForm] decimal totalPrice,
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }
        if (!await _accountService.IsValidAttendee(userId))
        {
            return this.RedirectWithError(error: ErrorStrings.NotAnAttendee);
        }
        if (!await _paymentService.IsValidPaymentMethod(paymentMethodId, userId))
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidPaymentMethod);
        }

        if (!await _ticketService.IsTicketOptionAvailable(ticketOptionId))
        {
            return this.RedirectWithError(error: ErrorStrings.TicketGone);
        }

        var currentPrices = await _eventService.GetPrice(ticketOptionId).ToDictionaryAsync();
        var currentTotalPrice = currentPrices.Values.Sum();
        if (currentTotalPrice != totalPrice)
        {
            return this.RedirectWithError(error: ErrorStrings.TicketGone);
        }

        try
        {
            var organizerId = await _eventService.GetOrganizerFromTicketOption(ticketOptionId);
            var organizerPaymentMethodId =
                (await _paymentService.GetPaymentMethods(organizerId).FirstAsync()).Id;

            await _paymentService.PerformTransaction(
                fromPaymentMethodId: paymentMethodId,
                toPaymentMethodId: organizerPaymentMethodId,
                amount: totalPrice
            );

            var tickets = ticketOptionId.Select(id => new Ticket
            {
                Id = Guid.Empty,
                Attendee = new() { Id = userId },
                Event = null,
                TicketOption = new()
                {
                    Id = id,
                    Name = string.Empty,
                    AmountAvailable = 0,
                    AdditionalPrice = 0
                },
                Price = currentPrices[id],
                IsReviewed = false,
                HolderFullName = holderFullName,
                HolderEmail = holderEmail,
                HolderPhoneNumber = holderPhoneNumber
            });
            await _ticketService.CreateTicket(tickets);

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpPost(nameof(ReviewTicket))]
    [Authorize]
    public async Task<ActionResult> ReviewTicket(
        [FromForm(Name = "ticket")] Guid ticketId,
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        if (!await _accountService.IsValidOrganizer(userId)
            || !await _ticketService.IsTicketOrganizer(ticketId, userId))
        {
            return this.RedirectWithError(error: ErrorStrings.TicketNoAccess);
        }

        try
        {
            await _ticketService.ReviewTicket(ticketId);

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }
}
