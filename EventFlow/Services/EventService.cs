using System.Diagnostics.CodeAnalysis;
using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class EventService : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public EventService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    {
        _dbContext = new(dbContextOptions);
    }

    public void Dispose() => _dbContext.Dispose();

    public async Task AddOrUpdateEvent(Data.Model.Event @event)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var dbOrganizer = await _dbContext.Organizers.SingleAsync(
            o => o.Account.Id == @event.Organizer.Id.ToString()
        );

        var dbEvent = _dbContext.Events.Update(new()
        {
            Id = @event.Id,
            Organizer = dbOrganizer,
            Name = @event.Name,
            Description = @event.Description,
            StartDate = @event.StartDate,
            EndDate = @event.EndDate,
            BannerUri = @event.BannerUri,
            Location = @event.Location,
            Price = @event.Price
        }).Entity;

        _dbContext.TicketOptions.UpdateRange(@event.TicketOptions.Select(
            t => new Data.Db.TicketOption()
            {
                Id = t.Id,
                Event = dbEvent,
                Name = t.Name,
                // Description = t.Description,
                AdditionalPrice = t.AdditionalPrice,
                AmountAvailable = t.AmountAvailable
            })
        );

        if (dbEvent.Id != Guid.Empty)
        {
            var query = _dbContext.EventCategories
                .Where(ec => ec.Event.Id == dbEvent.Id);
            _dbContext.EventCategories.RemoveRange(query);
        }

        var eventCategories = _dbContext.Categories
            .Join(
                @event.Categories.Select(c => c.Id), c => c.Id, c => c,
                (c, _) => new Data.Db.EventCategory() { Event = dbEvent, Category = c }
            );

        _dbContext.UpdateRange(eventCategories);

        await _dbContext.SaveChangesAsync();
        await _dbContext.Database.CommitTransactionAsync();
    }

    public async Task<Data.Model.Event?> GetEvent(Guid eventId)
    {
        var dbEvent = await _dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .SingleOrDefaultAsync(e => e.Id == eventId);
        if (dbEvent is null)
        {
            return null;
        }
        return await ToModelEventAsync(dbEvent);
    }

    public async IAsyncEnumerable<Data.Model.Event> GetEvents(Guid organizerId)
    {
        var query = _dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .Where(e => e.Organizer.Account.Id == organizerId.ToString());
        await foreach (var dbEvent in query.AsAsyncEnumerable())
        {
            yield return await ToModelEventAsync(dbEvent);
        }
    }

    public async IAsyncEnumerable<Data.Model.Event> FindEvents(
        ICollection<Guid>? category = null,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? keywords = null
    )
    {
        var query = _dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .AsQueryable();

        if (category is not null)
        {
            var categorySet = category.ToHashSet();
            query = _dbContext.EventCategories
                .Include(ec => ec.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
                .Where(ec => categorySet.Contains(ec.Category.Id))
                .Select(ec => ec.Event);
        }

        if (minDate.HasValue)
        {
            query = query.Where(e => e.StartDate >= minDate);
        }
        if (maxDate.HasValue)
        {
            query = query.Where(e => e.EndDate <= maxDate);
        }
        if (minPrice.HasValue)
        {
            query = query.Where(e => e.Price >= minPrice);
        }
        if (maxPrice.HasValue)
        {
            query = query.Where(e => e.Price <= maxPrice);
        }

        var keywordSet = keywords?.ToLowerInvariant()?.Split().ToHashSet();

        await foreach (var dbEvent in query.AsAsyncEnumerable())
        {
            if (keywordSet is not null)
            {
                bool hasMatchingWords = (dbEvent.Name + " " + dbEvent.Description)
                    .ToLowerInvariant()
                    .Split()
                    .Intersect(keywordSet)
                    .Any();

                if (!hasMatchingWords)
                {
                    continue;
                }
            }

            yield return await ToModelEventAsync(dbEvent);
        }
    }

    public async Task SaveEvent(Guid attendeeId, Guid eventId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var dbAttendee = await _dbContext.Attendees.SingleAsync(
            o => o.Account.Id == attendeeId.ToString()
        );

        var dbEvent = await _dbContext.Events.SingleAsync(e => e.Id == eventId);
        await _dbContext.SavedEvents.AddAsync(new()
        {
            Attendee = dbAttendee,
            Event = dbEvent
        });

        await _dbContext.SaveChangesAsync();
        await _dbContext.Database.CommitTransactionAsync();
    }

    public async IAsyncEnumerable<Data.Model.Category> GetCategories()
    {
        await foreach (var category in _dbContext.Categories.AsAsyncEnumerable())
        {
            yield return new Data.Model.Category()
            {
                Id = category.Id,
                Name = category.Name
            };
        }
    }

    public async Task<bool> IsValidCategory(ICollection<Guid> category)
    {
        var query = _dbContext.Categories.Join(category, c => c.Id, id => id, (a, b) => true);
        return await query.CountAsync() == category.Count;
    }

    private async Task<Data.Model.Event> ToModelEventAsync(Data.Db.Event dbEvent)
    {
        return new Data.Model.Event()
        {
            Id = dbEvent.Id,
            Organizer = new()
            {
                Id = Guid.Parse(dbEvent.Organizer.Account.Id),
                Name = dbEvent.Organizer.Account.UserName ?? "Unknown Organizer"
            },
            Name = dbEvent.Name,
            Description = dbEvent.Description,
            StartDate = dbEvent.StartDate,
            EndDate = dbEvent.EndDate,
            BannerUri = dbEvent.BannerUri,
            Location = dbEvent.Location,
            Price = dbEvent.Price,
            Categories = await _dbContext.EventCategories.Where(ec => ec.Event == dbEvent)
                .Select(ec => new Data.Model.Category()
                {
                    Id = ec.Category.Id,
                    Name = ec.Category.Name
                })
                .ToListAsync(),
            TicketOptions = await _dbContext.TicketOptions.Where(t => t.Event == dbEvent)
                .Select(t => new Data.Model.TicketOption()
                {
                    Id = t.Id,
                    Name = t.Name,
                    AdditionalPrice = t.AdditionalPrice,
                    AmountAvailable = t.AmountAvailable
                })
                .ToListAsync()
        };
    }
}
