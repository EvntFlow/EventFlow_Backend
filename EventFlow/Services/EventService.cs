using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class EventService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async Task AddOrUpdateEvent(Data.Model.Event @event)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbOrganizer = await dbContext.Organizers.SingleAsync(
            o => o.Account.Id == @event.Organizer.Id.ToString()
        );

        var dbEvent = dbContext.Events.Update(new()
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

        if (@event.TicketOptions is not null)
        {
            // We can only add/update, not delete ticket options.
            dbContext.TicketOptions.UpdateRange(@event.TicketOptions.Select(
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
        }

        if (@event.Categories is not null)
        {
            if (dbEvent.Id != Guid.Empty)
            {
                var query = dbContext.EventCategories
                    .Where(ec => ec.Event.Id == dbEvent.Id);
                dbContext.EventCategories.RemoveRange(query);
            }

            var eventCategories = dbContext.Categories.Join(
                @event.Categories.Select(c => c.Id), c => c.Id, c => c,
                (c, _) => new Data.Db.EventCategory() { Event = dbEvent, Category = c }
            );
            dbContext.UpdateRange(eventCategories);
        }

        await dbContext.SaveChangesAsync();
        await dbContext.Database.CommitTransactionAsync();
    }

    public async Task<Data.Model.Event?> GetEvent(Guid eventId)
    {
        using var dbContext = DbContext;
        var dbEvent = await dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .SingleOrDefaultAsync(e => e.Id == eventId);
        if (dbEvent is null)
        {
            return null;
        }

        var @event = ToModel(dbEvent);
        // For the single event endpoint, fill in categories and ticket options.
        @event.Categories = await dbContext.EventCategories
            .Include(ec => ec.Event)
            .Include(ec => ec.Category)
            .Where(ec => ec.Event == dbEvent)
            .AsAsyncEnumerable()
            .Select(ec => ToModel(ec.Category))
            .ToListAsync();
        @event.TicketOptions = await dbContext.TicketOptions.Where(t => t.Event == dbEvent)
            .AsAsyncEnumerable()
            .Select(t => ToModel(t))
            .ToListAsync();

        return @event;
    }

    public async IAsyncEnumerable<Data.Model.Event> GetEvents(Guid organizerId)
    {
        using var dbContext = DbContext;
        var query = dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .Where(e => e.Organizer.Account.Id == organizerId.ToString());
        await foreach (var dbEvent in query.AsAsyncEnumerable())
        {
            yield return ToModel(dbEvent);
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
        using var dbContext = DbContext;
        var query = dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .AsQueryable();

        if (category is not null && category.Count > 0)
        {
            var categorySet = category.ToHashSet();
            query = dbContext.EventCategories
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

            yield return ToModel(dbEvent);
        }
    }

    public async Task SaveEvent(Guid attendeeId, Guid eventId)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbAttendee = await dbContext.Attendees.SingleAsync(
            o => o.Account.Id == attendeeId.ToString()
        );

        var dbEvent = await dbContext.Events.SingleAsync(e => e.Id == eventId);
        await dbContext.SavedEvents.AddAsync(new()
        {
            Attendee = dbAttendee,
            Event = dbEvent
        });

        await dbContext.SaveChangesAsync();
        await dbContext.Database.CommitTransactionAsync();
    }

    public async IAsyncEnumerable<Data.Model.Category> GetCategories()
    {
        using var dbContext = DbContext;
        await foreach (var category in dbContext.Categories.AsAsyncEnumerable())
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
        using var dbContext = DbContext;
        var query = dbContext.Categories.Join(category, c => c.Id, id => id, (a, b) => true);
        return await query.CountAsync() == category.Count;
    }

    public async IAsyncEnumerable<KeyValuePair<Guid, decimal>>GetPrice(
        ICollection<Guid> ticketOptionId
    )
    {
        using var dbContext = DbContext;

        var query = dbContext.TicketOptions
            .Join(ticketOptionId, to => to.Id, id => id, (to, _) => to)
            .Select(to => new KeyValuePair<Guid, decimal>(
                to.Id, to.AdditionalPrice + to.Event.Price
            ));

        // Keep the dbContext in scope.
        await foreach (var kvp in query.AsAsyncEnumerable())
        {
            yield return kvp;
        }
    }

    public async Task<Guid> GetOrganizerFromTicketOption(ICollection<Guid> ticketOptionId)
    {
        using var dbContext = DbContext;
        var organizerIdString = await dbContext.TicketOptions
            .Include(to => to.Event)
                .ThenInclude(e => e.Organizer)
                    .ThenInclude(o => o.Account)
            .Join(ticketOptionId, to => to.Id, id => id, (to, _) => to)
            .Select(to => to.Event.Organizer.Account.Id)
            .Distinct()
            .SingleAsync();
        return Guid.Parse(organizerIdString);
    }

    private static Data.Model.Event ToModel(Data.Db.Event dbEvent)
    {
        return new()
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
        };
    }

    private static Data.Model.Category ToModel(Data.Db.Category dbCategory)
    {
        return new()
        {
            Id = dbCategory.Id,
            Name = dbCategory.Name
        };
    }

    private static Data.Model.TicketOption ToModel(Data.Db.TicketOption dbTicketOption)
    {
        return new()
        {
            Id = dbTicketOption.Id,
            Name = dbTicketOption.Name,
            AdditionalPrice = dbTicketOption.AdditionalPrice,
            AmountAvailable = dbTicketOption.AmountAvailable
        };
    }
}
