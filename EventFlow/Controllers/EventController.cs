using System.ComponentModel.DataAnnotations;
using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class EventController : ControllerBase
{
    private readonly EventService _eventService;
    private readonly AccountService _accountService;

    public EventController(EventService eventService, AccountService accountService)
    {
        _eventService = eventService;
        _accountService = accountService;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> CreateEvent(
        [FromForm, Required] string name,
        [FromForm] ICollection<Guid> category,
        [FromForm, Required] DateTime startDate,
        [FromForm, Required] DateTime endDate,
        [FromForm, Required] string location,
        [FromForm, Required] string description,
        [FromForm] Uri? bannerUri,
        [FromForm, Required] decimal price,
        [FromForm] ICollection<string> ticketName,
        [FromForm] ICollection<decimal> ticketPrice,
        [FromForm] ICollection<int> ticketCount,
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
        if (!await _accountService.IsValidOrganizer(userId))
        {
            return this.RedirectWithError(error: ErrorStrings.NotAnOrganizer);
        }

        if (!await _eventService.IsValidCategory(category))
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidCategory);
        }
        if (startDate >= endDate)
        {
            return this.RedirectWithError(error: ErrorStrings.EventStartAfterEnd);
        }
        if (price < 0)
        {
            return this.RedirectWithError(error: ErrorStrings.EventPriceNegative);
        }
        if (ticketName.Count != ticketPrice.Count || ticketName.Count != ticketCount.Count)
        {
            return this.RedirectWithError(error: ErrorStrings.ListLengthMismatch);
        }

        var @event = new Event
        {
            Id = Guid.Empty,
            Organizer = new() { Id = userId, Name = string.Empty },
            Name = name,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            BannerUri = bannerUri,
            Location = location,
            Price = price,
            Categories = [.. category.Select(id => new Category { Id = id, Name = string.Empty })],
            TicketOptions = [..
                ticketName.Zip(ticketPrice, ticketCount).Select(
                    ((string n, decimal p, int c) t) =>
                        new TicketOption
                        {
                            Id = Guid.Empty,
                            Name = t.n,
                            AdditionalPrice = t.p,
                            AmountAvailable = t.c
                        }
                )
            ]
        };

        try
        {
            await _eventService.AddOrUpdateEvent(@event);
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }

        return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
    }

    [HttpPatch]
    [Authorize]
    public async Task<ActionResult> ModifyEvent(
        [FromForm(Name = "event")][Required] Guid eventId,
        [FromForm] string? name,
        [FromForm] ICollection<Guid>? category,
        [FromForm] DateTime? startDate,
        [FromForm] DateTime? endDate,
        [FromForm] string? location,
        [FromForm] string? description,
        [FromForm] Uri? bannerUri,
        [FromForm] decimal? price,
        [FromForm] IList<Guid>? ticketId,
        [FromForm] IList<string>? ticketName,
        [FromForm] IList<decimal>? ticketPrice,
        [FromForm] IList<int>? ticketCount,
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
        if (!await _accountService.IsValidOrganizer(userId))
        {
            return this.RedirectWithError(error: ErrorStrings.NotAnOrganizer);
        }

        var @event = await _eventService.GetEvent(eventId);
        if (@event is null)
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidEvent);
        }
        if (@event.Organizer.Id != userId)
        {
            return this.RedirectWithError(error: ErrorStrings.InvalidEvent);
        }

        if (!string.IsNullOrEmpty(name))
        {
            @event.Name = name;
        }

        if (category is not null && category.Count > 0)
        {
            if (!await _eventService.IsValidCategory(category))
            {
                return this.RedirectWithError(error: ErrorStrings.InvalidCategory);
            }
            @event.Categories = [..
                category.Select(id => new Category { Id = id, Name = string.Empty })
            ];
        }

        if (startDate.HasValue)
        {
            @event.StartDate = startDate.Value;
        }
        if (endDate.HasValue)
        {
            @event.EndDate = endDate.Value;
        }
        if (@event.StartDate >= @event.EndDate)
        {
            return this.RedirectWithError(error: ErrorStrings.EventStartAfterEnd);
        }

        if (!string.IsNullOrEmpty(location))
        {
            @event.Location = location;
        }

        if (!string.IsNullOrEmpty(description))
        {
            @event.Description = description;
        }

        if (bannerUri is not null)
        {
            @event.BannerUri = bannerUri;
        }

        if (price.HasValue)
        {
            if (price.Value < 0)
            {
                return this.RedirectWithError(error: ErrorStrings.EventPriceNegative);
            }
            @event.Price = price.Value;
        }

        if (ticketId is not null)
        {
            if (ticketName is null || ticketPrice is null || ticketCount is null)
            {
                return this.RedirectWithError(error: ErrorStrings.ListLengthMismatch);
            }

            if (ticketId.Count != ticketName.Count
                || ticketId.Count != ticketPrice.Count
                || ticketId.Count != ticketCount.Count)
            {
                return this.RedirectWithError(error: ErrorStrings.ListLengthMismatch);
            }

            @event.TicketOptions = [];
            for (int i = 0; i < ticketId.Count; ++i)
            {
                @event.TicketOptions.Add(new()
                {
                    Id = ticketId[i],
                    Name = ticketName[i],
                    AdditionalPrice = ticketPrice[i],
                    AmountAvailable = ticketCount[i]
                });
            }
        }

        try
        {
            await _eventService.AddOrUpdateEvent(@event);
            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult> GetEvents(
        [FromQuery(Name = "event")] Guid? eventId
    )
    {
        if (eventId.HasValue)
        {
            // Get specific event.
            var @event = await _eventService.GetEvent(eventId.Value);
            if (@event is null)
            {
                return NotFound();
            }
            return Ok(@event);
        }
        else
        {
            // Get events owned by current organizer.
            var userId = this.TryGetAccountId();
            if (userId == Guid.Empty)
            {
                return BadRequest();
            }
            if (!await _accountService.IsValidOrganizer(userId))
            {
                return Unauthorized();
            }

            return Ok(_eventService.GetEvents(userId));
        }
    }

    [HttpGet(nameof(FindEvents))]
    public async Task<ActionResult<IAsyncEnumerable<Event>>> FindEvents(
        [FromQuery] ICollection<Guid>? category,
        [FromQuery] DateTime? minDate,
        [FromQuery] DateTime? maxDate,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? keywords
    )
    {
        if (category is not null && category.Count > 0)
        {
            if (!await _eventService.IsValidCategory(category))
            {
                return BadRequest();
            }
        }

        return Ok(
            _eventService.FindEvents(category, minDate, maxDate, minPrice, maxPrice, keywords)
        );
    }

    [HttpPost("Saved")]
    [Authorize]
    public async Task<ActionResult> SaveEvent(
        [FromForm(Name = "event")] Guid eventId,
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

        try
        {
            await _eventService.SaveEvent(userId, eventId);
            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet("Saved")]
    public async Task<ActionResult<IAsyncEnumerable<Event>>> GetSavedEvents()
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        if (!await _accountService.IsValidAttendee(userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(_eventService.GetSavedEvents(userId));
        }
        catch
        {
            return BadRequest();
        }
    }

    [HttpGet(nameof(Category))]
    public ActionResult<IAsyncEnumerable<Category>> GetCategories()
    {
        return Ok(_eventService.GetCategories());
    }
}
