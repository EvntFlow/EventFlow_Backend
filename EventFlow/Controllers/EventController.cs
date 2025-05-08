using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Controllers;

[Route("/api/[controller]")]
public class EventController : ControllerBase
{
    private readonly IImageService _imageService;
    private readonly EventService _eventService;
    private readonly AccountService _accountService;

    public EventController(
        EventService eventService,
        AccountService accountService,
        IImageService imageService
    )
    {
        _eventService = eventService;
        _accountService = accountService;
        _imageService = imageService;
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
        IFormFile? bannerFile,
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
            Organizer = new() { Id = userId, Name = string.Empty, Email = string.Empty },
            Name = name,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            BannerUri = bannerUri,
            Location = location,
            Price = price,
            Interested = 0,
            Sold = 0,
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
            ],
        };

        Guid? toRollback = null;

        try
        {
            if (bannerUri is null && bannerFile is not null)
            {
                using var stream = bannerFile.OpenReadStream();
                var id = await _imageService.UploadImageAsync(stream);
                if (!id.HasValue)
                {
                    return this.RedirectWithError(error: ErrorStrings.ImageUploadFailed);
                }
                @event.BannerFile = id;
                toRollback = id;
            }

            await _eventService.AddOrUpdateEvent(@event);
            toRollback = null;

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
        finally
        {
            if (toRollback.HasValue)
            {
                await _imageService.DeleteImageAsync(toRollback.Value);
            }
        }
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
        IFormFile? bannerFile,
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

        Guid? toDestroy = null;
        Guid? toRollback = null;

        try
        {
            if (@event.BannerFile.HasValue)
            {
                var url = await _imageService.GetImageAsync(@event.BannerFile.Value);
                if (bannerFile is null && (bannerUri is null || bannerUri == url))
                {
                    // Ignore, resubmission of the same url
                }
                else
                {
                    // Destroy previous image
                    toDestroy = @event.BannerFile.Value;
                }
            }

            if (bannerFile is not null)
            {
                using var stream = bannerFile.OpenReadStream();
                @event.BannerFile = await _imageService.UploadImageAsync(stream);
                toRollback = @event.BannerFile;
            }

            await _eventService.AddOrUpdateEvent(@event);
            toRollback = null;

            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
        finally
        {
            if (toDestroy.HasValue)
            {
                await _imageService.DeleteImageAsync(toDestroy.Value);
            }
            if (toRollback.HasValue)
            {
                await _imageService.DeleteImageAsync(toRollback.Value);
            }
        }
    }

    [HttpGet]
    public async Task<ActionResult> GetEvents(
        [FromQuery(Name = "event")] Guid? eventId
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        if (eventId.HasValue)
        {
            // Get specific event.
            var @event = await _eventService.GetEvent(eventId.Value);
            if (@event is null)
            {
                return NotFound();
            }

            await ProcessEventBanner(@event);

            var userId = this.TryGetAccountId();
            if (!await _accountService.IsValidAttendee(userId))
            {
                return Ok(@event);
            }

            var savedStatus = await _eventService
                .CheckSavedEvents(userId, [@event.Id])
                .SingleOrDefaultAsync();

            if (savedStatus.Key == @event.Id)
            {
                @event.SavedEvent = new()
                {
                    Id = savedStatus.Value
                };
            }

            return Ok(@event);
        }
        else
        {
            // Get events owned by current organizer.
            var userId = this.TryGetAccountId();
            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }
            if (!await _accountService.IsValidOrganizer(userId))
            {
                return Unauthorized();
            }

            return Ok(_eventService.GetEvents(userId).Select(
                async (Event e, CancellationToken ct) => { await ProcessEventBanner(e); return e; }
            ));
        }
    }

    [HttpGet(nameof(FindEvents))]
    public async Task<ActionResult<IAsyncEnumerable<Event>>> FindEvents(
        [FromQuery] ICollection<Guid>? category,
        [FromQuery] DateTime? minDate,
        [FromQuery] DateTime? maxDate,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] ICollection<string>? location,
        [FromQuery] string? keywords
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        if (category is not null && category.Count > 0)
        {
            if (!await _eventService.IsValidCategory(category))
            {
                return BadRequest();
            }
        }

        var query = _eventService.FindEvents(
            category: category,
            minDate: minDate,
            maxDate: maxDate,
            minPrice: minPrice,
            maxPrice: maxPrice,
            location: location,
            keywords: keywords
        );

        query = query.Select(
            async (Event e, CancellationToken ct) => { await ProcessEventBanner(e); return e; }
        );

        var userId = this.TryGetAccountId();
        if (!await _accountService.IsValidAttendee(userId))
        {
            return Ok(query);
        }

        var events = await query.ToArrayAsync();
        var savedStatus = await _eventService
            .CheckSavedEvents(userId, events.Select(e => e.Id).ToHashSet())
            .ToDictionaryAsync();

        foreach (var @event in events)
        {
            if (savedStatus.TryGetValue(@event.Id, out var savedEventId))
            {
                @event.SavedEvent = new SavedEvent() { Id = savedEventId };
            }
        }

        return Ok(events.ToAsyncEnumerable());
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

    [HttpPut("Saved")]
    [Authorize]
    public async Task<ActionResult<SavedEvent>> SaveEvent(
        [FromBody] Guid eventId
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

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
            var @event = await _eventService.GetEvent(eventId);
            if (@event is null)
            {
                return NotFound();
            }
            await _eventService.SaveEvent(userId, eventId);

            var savedEvents = await _eventService.CheckSavedEvents(userId, [eventId])
                .ToDictionaryAsync();

            return Ok(new SavedEvent()
            {
                Id = savedEvents.Single().Value,
                Event = @event
            });
        }
        catch
        {
            return BadRequest();
        }
    }

    [HttpDelete("Saved")]
    [Authorize]
    public async Task<ActionResult<SavedEvent>> UnsaveEvent(
        [FromQuery(Name = "savedEvent")] Guid savedEventId
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

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
            await _eventService.UnsaveEvent(userId, savedEventId);
            return Ok();
        }
        catch
        {
            return BadRequest();
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

    private async Task ProcessEventBanner(Event @event)
    {
        if (@event.BannerFile.HasValue)
        {
            @event.BannerUri = await _imageService.GetImageAsync(@event.BannerFile.Value);
            @event.BannerFile = null;
        }
    }
}
