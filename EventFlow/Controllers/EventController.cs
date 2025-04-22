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
        [FromForm] string name,
        [FromForm] ICollection<Guid> category,
        [FromForm] DateTime startDate,
        [FromForm] DateTime endDate,
        [FromForm] string location,
        [FromForm] string description,
        [FromForm] Uri? bannerUri,
        [FromForm] decimal price,
        [FromForm] ICollection<string> ticketName,
        [FromForm] ICollection<decimal> ticketPrice,
        [FromForm] ICollection<int> ticketCount,
        [FromQuery] Uri? returnUri
    )
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        if (await _accountService.IsValidOrganizer(userId))
        {
            return Unauthorized();
        }

        if (!await _eventService.IsValidCategory(category))
        {
            return BadRequest();
        }
        if (startDate >= endDate)
        {
            return BadRequest();
        }
        if (price < 0)
        {
            return BadRequest();
        }
        if (ticketName.Count != ticketPrice.Count || ticketName.Count != ticketCount.Count)
        {
            return BadRequest();
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
            return BadRequest();
        }

        return Redirect(returnUri?.ToString() ?? "/");
    }

    [HttpPatch]
    [Authorize]
    public async Task<ActionResult> ModifyEvent(
        [FromForm][Required] Guid id,
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
        [FromQuery] Uri? returnUri
    )
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        if (await _accountService.IsValidOrganizer(userId))
        {
            return Unauthorized();
        }

        var @event = await _eventService.GetEvent(id);
        if (@event is null)
        {
            return NotFound();
        }
        if (@event.Organizer.Id != userId)
        {
            return Unauthorized();
        }

        if (!string.IsNullOrEmpty(name))
        {
            @event.Name = name;
        }

        if (category is not null && category.Count > 0)
        {
            if (!await _eventService.IsValidCategory(category))
            {
                return BadRequest();
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
            return BadRequest();
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
                return BadRequest();
            }
            @event.Price = price.Value;
        }

        if (ticketId is not null)
        {
            if (ticketName is null || ticketPrice is null || ticketCount is null)
            {
                return BadRequest();
            }

            if (ticketId.Count != ticketName.Count
                || ticketId.Count != ticketPrice.Count
                || ticketId.Count != ticketCount.Count)
            {
                return BadRequest();
            }

            @event.TicketOptions.Clear();
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
        }
        catch
        {
            return BadRequest();
        }

        return Redirect(returnUri?.ToString() ?? "/");
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IAsyncEnumerable<Event>>> GetEvents()
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        if (await _accountService.IsValidOrganizer(userId))
        {
            return Unauthorized();
        }

        return Ok(_eventService.GetEvents(userId));
    }

    [HttpGet(nameof(FindEvents))]
    public async Task<ActionResult<IAsyncEnumerable<Event>>> FindEvents(
        [FromForm] ICollection<Guid>? category,
        [FromForm] DateTime? minDate,
        [FromForm] DateTime? maxDate,
        [FromForm] decimal? minPrice,
        [FromForm] decimal? maxPrice,
        [FromForm] string? keywords
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

    [HttpPost(nameof(SaveEvent))]
    [Authorize]
    public async Task<ActionResult> SaveEvent(
        [FromForm] Guid id,
        [FromQuery] Uri? returnUri
    )
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest();
        }
        if (await _accountService.IsValidAttendee(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _eventService.SaveEvent(userId, id);
        }
        catch
        {
            return BadRequest();
        }

        return Redirect(returnUri?.ToString() ?? "/");
    }

    [HttpGet(nameof(GetCategories))]
    public ActionResult<IAsyncEnumerable<Category>> GetCategories()
    {
        return Ok(_eventService.GetCategories());
    }
}
