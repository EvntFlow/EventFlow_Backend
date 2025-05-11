using System.ComponentModel.DataAnnotations;
using System.Net;
using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Controllers;

[Route("/api/[controller]")]
public class TicketController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IImageService _imageService;
    private readonly TicketService _ticketService;
    private readonly AccountService _accountService;
    private readonly EventService _eventService;
    private readonly NotificationService _notificationService;
    private readonly PaymentService _paymentService;

    public TicketController(
        IEmailService emailService,
        IImageService imageService,
        TicketService ticketService,
        AccountService accountService,
        EventService eventService,
        NotificationService notificationService,
        PaymentService paymentService
    )
    {
        _emailService = emailService;
        _imageService = imageService;
        _ticketService = ticketService;
        _accountService = accountService;
        _eventService = eventService;
        _notificationService = notificationService;
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
        var enumerable = tickets.ToAsyncEnumerable().GroupBy(t => t.Event!.Id)
            .SelectMany(
                async (group, ct) =>
                {
                    var @event = await _eventService.GetEvent(group.Key);
                    if (@event is not null && @event.BannerFile.HasValue)
                    {
                        @event.BannerUri =
                            await _imageService.GetImageAsync(@event.BannerFile.Value);
                        @event.BannerFile = null;
                    }
                    return group.Select(t => { t.Event = @event; return t; });
                }
        );

        return Ok(enumerable);
    }

    [HttpPost(nameof(CancelTicket))]
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

            bool success = false;

            if (ticket.Price == 0)
            {
                success = await _ticketService.DeleteTicket(ticketId, async (_) =>
                {
                    return await SendCancelNotification(
                        (await _eventService.GetEvent(ticket.Event!.Id))!,
                        ticket,
                        isCancelFromOrganizer
                    );
                });
            }
            else
            {
                var attendeeId = ticket.Attendee.Id;
                var organizerId = ticket.Event!.Organizer.Id;
                var attendeePayment =
                    await _paymentService.GetPaymentMethods(attendeeId).FirstAsync();
                var organizerPayment =
                    await _paymentService.GetPaymentMethods(organizerId).FirstAsync();

                success = await _ticketService.DeleteTicket(ticketId, async (_) =>
                {
                    await _paymentService.PerformTransaction(
                        fromPaymentMethodId: organizerPayment.Id,
                        toPaymentMethodId: attendeePayment.Id,
                        amount: ticket.Price
                    );

                    try
                    {
                        await SendCancelNotification(
                            (await _eventService.GetEvent(ticket.Event!.Id))!,
                            ticket,
                            isCancelFromOrganizer
                        );
                    }
                    catch
                    {
                        // Cannot fail here since the payment has been made!
                    }

                    return true;
                });
            }

            if (!success)
            {
                return this.RedirectWithError(error: ErrorStrings.TransactionFailed);
            }

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
        var totalPrice = ticketOptionId.Aggregate(0.0m, (sum, id) => sum + prices[id]);

        if (totalPrice == 0)
        {
            var @event = await _eventService.GetEventFromTicketOption(ticketOptionId);

            // Do the actual purchase.
            var tickets = ticketOptionId.Select(id => new Ticket
            {
                Id = Guid.Empty,
                Timestamp = DateTime.UtcNow,
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

            await _ticketService.CreateTicket(tickets, async (createdTickets) =>
            {
                return await SendCreationNotification(
                    userId, holderFullName, holderEmail,
                    @event.Organizer.Id, @event.Organizer.Email, @event,
                    createdTickets
                );
            });

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
        var currentTotalPrice = ticketOptionId
            .Aggregate(0.0m, (sum, id) => sum + currentPrices[id]);

        if (currentTotalPrice != totalPrice)
        {
            return this.RedirectWithError(error: ErrorStrings.TicketGone);
        }

        try
        {
            var @event = await _eventService.GetEventFromTicketOption(ticketOptionId);
            var organizerPaymentMethodId =
                (await _paymentService.GetPaymentMethods(@event.Organizer.Id).FirstAsync()).Id;

            var tickets = ticketOptionId.Select(id => new Ticket
            {
                Id = Guid.Empty,
                Timestamp = DateTime.UtcNow,
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

            bool success = await _ticketService.CreateTicket(tickets, async (createdTickets) =>
            {
                var actualTotalPrice = createdTickets.Sum(t => t.Price);

                if (actualTotalPrice != totalPrice)
                {
                    return false;
                }

                await _paymentService.PerformTransaction(
                    fromPaymentMethodId: paymentMethodId,
                    toPaymentMethodId: organizerPaymentMethodId,
                    amount: totalPrice
                );

                try
                {
                    await SendCreationNotification(
                        userId, holderFullName, holderEmail,
                        @event.Organizer.Id, @event.Organizer.Email, @event,
                        createdTickets
                    );
                }
                catch
                {
                    // Cannot fail here since the payment has been made!
                }

                return true;
            });

            if (!success)
            {
                return this.RedirectWithError(error: ErrorStrings.TransactionFailed);
            }

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpPost(nameof(UpdateTicket))]
    [Authorize]
    public async Task<ActionResult> UpdateTicket(
        [FromForm(Name = "ticket"), Required] Guid ticketId,
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

        if (!await _accountService.IsValidAttendee(userId)
            || !await _ticketService.IsTicketOwner(ticketId, userId))
        {
            return this.RedirectWithError(error: ErrorStrings.TicketNoAccess);
        }

        try
        {
            var ticket = await _ticketService.GetTicket(ticketId);
            if (ticket is null)
            {
                return this.RedirectWithError(error: ErrorStrings.InvalidTicket);
            }

            ticket.HolderFullName = holderFullName;
            ticket.HolderEmail = holderEmail;
            ticket.HolderPhoneNumber = holderPhoneNumber;

            await _ticketService.UpdateTicket(ticket);
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

        if (!await _accountService.IsValidOrganizer(userId)
            || !await _ticketService.IsTicketOrganizer(ticketId, userId))
        {
            return this.RedirectWithError(error: ErrorStrings.TicketNoAccess);
        }

        try
        {
            var ticket = await _ticketService.GetTicket(ticketId);
            if (ticket is null)
            {
                return this.RedirectWithError(error: ErrorStrings.InvalidTicket);
            }

            var @event = await _eventService.GetEvent(ticket.Event!.Id);
            if (@event is null)
            {
                return this.RedirectWithError(error: ErrorStrings.InvalidEvent);
            }

            if (ticket.HolderFullName != holderFullName
                || ticket.HolderEmail != holderEmail
                || ticket.HolderPhoneNumber != holderPhoneNumber)
            {
                return this.RedirectWithError(error: ErrorStrings.TicketChanged);
            }

            await _ticketService.ReviewTicket(ticket);

            await _notificationService.SendNotificationAsync(
                ticket.Attendee.Id, new()
                {
                    Id = Guid.Empty,
                    Timestamp = DateTime.UtcNow,
                    Topic = "Tickets",
                    Message = $"A \"{ticket.TicketOption.Name}\" ticket " +
                        $"for \"{@event.Name}\" has been approved."
                }
            );

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet(nameof(GetAttendance))]
    [Authorize]
    public async Task<ActionResult<IAsyncEnumerable<Ticket>>> GetAttendance(
        [FromQuery(Name = "event")] Guid? eventId
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest(error: ErrorStrings.SessionExpired);
        }

        if (!await _accountService.IsValidOrganizer(userId))
        {
            return Unauthorized(ErrorStrings.NotAnOrganizer);
        }

        try
        {
            var tickets = await _ticketService.GetAttendance(userId, eventId).ToListAsync();
            var enumerable = tickets.ToAsyncEnumerable().GroupBy(t => t.Event!.Id)
                .SelectMany(
                    async (group, ct) =>
                    {
                        var @event =
                            await _eventService.GetEvent(group.Key, includeCollections: false);
                        return group.Select(t => { t.Event = @event; return t; });
                    }
            );

            return Ok(enumerable);
        }
        catch
        {
            return BadRequest(ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet(nameof(Statistics))]
    [Authorize]
    public async Task<ActionResult<Statistics>> GetStatistics(
        [FromQuery] DateTime? month
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest(error: ErrorStrings.SessionExpired);
        }

        if (!await _accountService.IsValidOrganizer(userId))
        {
            return Unauthorized(ErrorStrings.NotAnOrganizer);
        }

        month ??= DateTime.UtcNow;

        try
        {
            var normalizedMonth = month.Value.Date.Subtract(TimeSpan.FromDays(month.Value.Day - 1));

            return Ok(await _ticketService.GetStatistics(userId, normalizedMonth));
        }
        catch
        {
            return BadRequest(ErrorStrings.ErrorTryAgain);
        }
    }

    private async Task<bool> SendCreationNotification(
        Guid holderId,
        string holderFullName,
        string holderEmail,
        Guid organizerId,
        string organizerEmail,
        Event @event,
        ICollection<Ticket> tickets
    )
    {
        // New Tickets bought
        await _notificationService.SendNotificationAsync(holderId,
            new()
            {
                Id = Guid.Empty,
                Timestamp = DateTime.UtcNow,
                Topic = "Tickets",
                Message =
                    $"Your {tickets.Count} ticket(s) " +
                    $"for \"{@event.Name}\" are ready."
            }
        );

        var e = (Func<string, string>)WebUtility.HtmlEncode;

        await _emailService.SendEmailAsync(
            holderEmail,
            subject: $"EventFlow | Tickets for {@event.Name}",
            body: $"Your ticket(s) for {@event.Name} are ready. " +
                "Please use the links below to view your digital ticket(s).\n" +
                string.Join("\n", tickets.Select((c, index) =>
                    $"Ticket #{index + 1} ({c.TicketOption.Name}): " +
                    this.GetRedirectUrl(
                        path: "/Ticket",
                        args: [ new KeyValuePair<string, object?>("view", c.Id) ]
                    )
                )),
            htmlBody: $"Your ticket(s) for <b>{e(@event.Name)}</b> are ready. " +
                "Please use the links below to view your digital ticket(s).<br/>" +
                "<ul>" +
                string.Join("", tickets.Select((c, index) =>
                    $"<li><a href=\"" +
                    this.GetRedirectUrl(
                        path: "/Ticket",
                        args: [ new KeyValuePair<string, object?>("view", c.Id) ]
                    ) +
                    $"\">Ticket #{index + 1} (<b>{e(c.TicketOption.Name)}</b>)</a></li>"
                )) +
                "</ul>"
        );

        // New Ticket sold
        await _notificationService.SendNotificationAsync(organizerId,
            new()
            {
                Id = Guid.Empty,
                Timestamp = DateTime.UtcNow,
                Topic = "Attendance",
                Message = $"\"{holderFullName}\" bought {tickets.Count} ticket(s) " +
                    $"for \"{@event.Name}\"."
            }
        );
        await _emailService.SendEmailAsync(
            organizerEmail,
            subject: $"EventFlow Organizers | {@event.Name}",
            body: $"{holderFullName} just bought {tickets.Count} ticket(s) " +
                $"for {@event.Name}. " +
                "Please use the links below to review the ticket(s).\n" +
                string.Join("\n", tickets.Select((c, index) =>
                    $"Ticket #{index + 1} ({c.TicketOption.Name}): " +
                    this.GetRedirectUrl(
                        path: "/Ticket/Attendance",
                        args: [
                            new KeyValuePair<string, object?>("event", @event.Id),
                            new KeyValuePair<string, object?>("review", c.Id)
                        ]
                    )
                )),
            htmlBody: $"<b>{e(holderFullName)}</b> just bought <b>{tickets.Count}</b> ticket(s) " +
                $"for <b>{e(@event.Name)}</b>. " +
                "Please use the links below to review the ticket(s).<br/>" +
                "<ul>" +
                string.Join("", tickets.Select((c, index) =>
                    $"<li><a href=\"" +
                    this.GetRedirectUrl(
                        path: "/Ticket/Attendance",
                        args: [
                            new KeyValuePair<string, object?>("event", @event.Id),
                            new KeyValuePair<string, object?>("review", c.Id)
                        ]
                    ) +
                    $"\">Ticket #{index + 1} (<b>{e(c.TicketOption.Name)}</b>)</a></li>"
                )) +
                "</ul>"
        );

        return true;
    }

    private async Task<bool> SendCancelNotification(
        Event @event,
        Ticket ticket,
        bool isReject
    )
    {
        var e = (Func<string, string>)WebUtility.HtmlEncode;

        if (!isReject)
        {
            await _notificationService.SendNotificationAsync(@event.Organizer.Id,
                new()
                {
                    Id = Guid.Empty,
                    Timestamp = DateTime.UtcNow,
                    Topic = "Attendance",
                    Message = $"\"{ticket.HolderFullName}\" canceled a " +
                        $"\"{ticket.TicketOption.Name}\" ticket for \"{@event.Name}\"."
                }
            );
            await _emailService.SendEmailAsync(
                @event.Organizer.Email,
                subject: $"EventFlow Organizers | {@event.Name}",
                body: $"{ticket.HolderFullName} just canceled a " +
                    $"{ticket.TicketOption.Name} ticket for {@event.Name}. " +
                    "A refund has been automatically processed.",
                htmlBody: $"<b>{e(ticket.HolderFullName)}</b> just canceled a " +
                    $"<b>{e(ticket.TicketOption.Name)}</b> ticket " +
                    $"for <b>{e(@event.Name)}</b>.<br/>" +
                    "A refund has been automatically processed."
            );
            return true;
        }
        else
        {
            await _notificationService.SendNotificationAsync(ticket.Attendee.Id,
                new()
                {
                    Id = Guid.Empty,
                    Timestamp = DateTime.UtcNow,
                    Topic = "Tickets",
                    Message = $"Your \"{ticket.TicketOption.Name}\" ticket for " +
                        $"\"{@event.Name}\" was rejected."
                }
            );
            await _emailService.SendEmailAsync(
                ticket.HolderEmail,
                subject: $"EventFlow | Tickets for {@event.Name}",
                body: $"Your {ticket.TicketOption.Name} ticket for {@event.Name} was rejected. " +
                    "A refund has been automatically processed.",
                htmlBody: $"Your <b>{e(ticket.TicketOption.Name)}</b> ticket for " +
                    $"<b>{e(@event.Name)}</b> was rejected.<br/>" +
                    "A refund has been automatically processed."
            );
            return true;
        }
    }
}
