using System.Diagnostics.CodeAnalysis;
using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class TicketService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async IAsyncEnumerable<Data.Model.Ticket> GetTickets(Guid userId)
    {
        using var dbContext = DbContext;
        var query = dbContext.Tickets
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
            .Where(t => t.Attendee.Account.Id == userId.ToString())
            .OrderByDescending(t => t.Timestamp);

        await foreach (var dbTicket in query.ToAsyncEnumerable())
        {
            yield return ToModel(dbTicket);
        }
    }

    public async Task<Data.Model.Ticket?> GetTicket(Guid ticketId)
    {
        using var dbContext = DbContext;
        var dbTicket = await dbContext.Tickets
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
            .SingleOrDefaultAsync(t => t.Id == ticketId);
        return ToModel(dbTicket);
    }

    public async Task<bool> CreateTicket(
        IEnumerable<Data.Model.Ticket> tickets,
        Func<ICollection<Data.Model.Ticket>, Task<bool>>? callback = null
    )
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var eventsToUpdate = new Dictionary<Guid, Data.Db.Event>();

        var dbTickets = await tickets.ToAsyncEnumerable().Select(async (ticket, _, _) =>
        {
            var dbTicketOption = await dbContext.TicketOptions
                .Include(to => to.Event)
                    .ThenInclude(to => to.Organizer)
                        .ThenInclude(o => o.Account)
                .SingleAsync(to => to.Id == ticket.TicketOption.Id);

            dbTicketOption.Event.Sold += 1;
            eventsToUpdate.TryAdd(dbTicketOption.Event.Id, dbTicketOption.Event);

            return new Data.Db.Ticket()
            {
                Timestamp = ticket.Timestamp,
                TicketOption = dbTicketOption,
                Attendee =
                    dbContext.Attendees
                        .Include(a => a.Account)
                        .Single(a => a.Account.Id == ticket.Attendee.Id.ToString()),
                Price = ticket.Price,
                IsReviewed = false,
                HolderFullName = ticket.HolderFullName,
                HolderEmail = ticket.HolderEmail,
                HolderPhoneNumber = ticket.HolderPhoneNumber
            };
        }).ToListAsync();

        await dbContext.Tickets.AddRangeAsync(dbTickets);
        dbContext.Events.UpdateRange(eventsToUpdate.Values);
        await dbContext.SaveChangesAsync();

        if (callback is not null && !await callback([.. dbTickets.Select(t => ToModel(t))]))
        {
            await transaction.RollbackAsync();
            return false;
        }

        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteTicket(
        Guid ticketId,
        Func<Data.Model.Ticket, Task<bool>>? callback = null
    )
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbTicket = await dbContext.Tickets
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .SingleAsync(t => t.Id == ticketId);

        var dbEvent = dbTicket.TicketOption.Event;
        dbEvent.Sold -= 1;

        var ticket = ToModel(dbTicket);

        dbContext.Tickets.Remove(dbTicket);
        dbContext.Events.Update(dbEvent);
        await dbContext.SaveChangesAsync();

        if (callback is not null && !await callback(ticket))
        {
            await transaction.RollbackAsync();
            return false;
        }

        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteTickets(
        Guid eventId,
        Func<Data.Model.Ticket, Task<bool>>? callback = null
    )
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var query = dbContext.Tickets
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .Where(t => t.TicketOption.Event.Id == eventId);

        foreach (var dbTicket in await query.ToListAsync())
        {
            var savepointId = Guid.NewGuid();
            await transaction.CreateSavepointAsync($"{savepointId}");

            var dbEvent = dbTicket.TicketOption.Event;
            dbEvent.Sold -= 1;

            var ticket = ToModel(dbTicket);

            dbContext.Tickets.Remove(dbTicket);
            dbContext.Events.Update(dbEvent);
            await dbContext.SaveChangesAsync();

            if (callback is not null && !await callback(ticket))
            {
                await transaction.RollbackToSavepointAsync($"{savepointId}");
                await transaction.CommitAsync();
                return false;
            }

            await transaction.ReleaseSavepointAsync($"{savepointId}");
        }

        await transaction.CommitAsync();
        return true;
    }

    public async Task UpdateTicket(Data.Model.Ticket ticket)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbTicket = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);

        var modified = false;
        if (dbTicket.HolderFullName != ticket.HolderFullName)
        {
            dbTicket.HolderFullName = ticket.HolderFullName;
            modified = true;
        }
        if (dbTicket.HolderEmail != ticket.HolderEmail)
        {
            dbTicket.HolderEmail = ticket.HolderEmail;
            modified = true;
        }
        if (dbTicket.HolderPhoneNumber != ticket.HolderPhoneNumber)
        {
            dbTicket.HolderPhoneNumber = ticket.HolderPhoneNumber;
            modified = true;
        }

        if (dbTicket.IsReviewed && modified)
        {
            dbTicket.IsReviewed = false;
        }

        if (modified)
        {
            dbContext.Tickets.Update(dbTicket);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ReviewTicket(Data.Model.Ticket ticket)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbTicket = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);

        var matching = dbTicket.HolderFullName == ticket.HolderFullName
            && dbTicket.HolderEmail == ticket.HolderEmail
            && dbTicket.HolderPhoneNumber == ticket.HolderPhoneNumber;

        if (!dbTicket.IsReviewed && matching)
        {
            dbTicket.IsReviewed = true;
            dbContext.Tickets.Update(dbTicket);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async IAsyncEnumerable<Data.Model.Ticket> GetAttendance(
        Guid organizerId,
        Guid? eventId
    )
    {
        using var dbContext = DbContext;

        var query = dbContext.Tickets
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(o => o.Account)
            .Where(t => t.TicketOption.Event.Organizer.Account.Id == $"{organizerId}");

        if (eventId.HasValue)
        {
            query = query.Where(t => t.TicketOption.Event.Id == eventId);
        }

        query = query.OrderByDescending(t => t.Timestamp);

        await foreach (var dbTicket in query.AsAsyncEnumerable())
        {
            yield return ToModel(dbTicket);
        }
    }

    public async Task<Data.Model.Statistics> GetStatistics(
        Guid organizerId,
        DateTime month
    )
    {
        DateTime start = month.Date.AddDays(-month.Day + 1).ToUniversalTime();
        DateTime end = start.AddMonths(1);

        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var totalEvents = await dbContext.Events
            .Include(e => e.Organizer)
                .ThenInclude(o => o.Account)
            .Where(e => e.Organizer.Account.Id == $"{organizerId}")
            .Where(e => e.StartDate < end && e.EndDate >= start)
            .CountAsync();

        var tickets = dbContext.Tickets
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(e => e.Organizer)
                        .ThenInclude(e => e.Account)
            .Where(t => t.TicketOption.Event.Organizer.Account.Id == $"{organizerId}")
            .Where(t => t.Timestamp < end && t.Timestamp >= start);

        var totalTickets = await tickets.CountAsync();
        var totalSales = await tickets.SumAsync(t => t.Price);
        var totalReviewed = await tickets.CountAsync(t => t.IsReviewed);

        var dailySales = await tickets.GroupBy(t => t.Timestamp.Day)
            .OrderByDescending(g => g.Key)
            .Select(g => new { Day = g.Key, Sum = g.Sum(t => t.Price) })
            .ToListAsync();

        var dailySalesArray = new decimal[dailySales.FirstOrDefault()?.Day ?? 0];
        foreach (var kvp in dailySales)
        {
            dailySalesArray[kvp.Day - 1] = kvp.Sum;
        }

        return new Data.Model.Statistics()
        {
            TotalEvents = totalEvents,
            TotalTickets = totalTickets,
            TotalSales = totalSales,
            TotalReviewed = totalReviewed,
            DailySales = dailySalesArray
        };
    }

    public async Task<bool> IsTicketOptionAvailable(ICollection<Guid> ticketOptionId)
    {
        using var dbContext = DbContext;

        var ticketOptionIdGroupedInput = ticketOptionId.CountBy(t => t)
            .ToDictionary(kvp => kvp.Key);

        var eventId = await dbContext.TicketOptions
            .Where(to => to.Id == ticketOptionId.First())
            .Select(to => to.Event.Id)
            .FirstOrDefaultAsync();

        if (eventId == Guid.Empty)
        {
            // First event is invalid.
            return false;
        }

        if (await dbContext.TicketOptions
            .Where(to => to.Event.Id == eventId)
            .Join(ticketOptionIdGroupedInput.Keys, to => to.Id, id => id, (to, _) => to)
            .CountAsync() != ticketOptionIdGroupedInput.Keys.Count)
        {
            // Second or other events are invalid.
            return false;
        }

        var ticketOptionIdDb = await dbContext.TicketOptions
            .Join(ticketOptionIdGroupedInput.Keys, to => to.Id, id => id, (to, _) => to)
            .ToDictionaryAsync(to => to.Id);

        var ticketOptionIdOrderedDb = await dbContext.Tickets
            .Include(t => t.TicketOption)
            .Join(ticketOptionIdGroupedInput.Keys, t => t.TicketOption.Id, id => id, (t, _) => t)
            .GroupBy(t => t.TicketOption.Id)
            .Select(g => new KeyValuePair<Guid, int>(g.Key, g.Count()))
            .ToDictionaryAsync(t => t.Key);

        foreach (var id in ticketOptionIdGroupedInput.Keys)
        {
            var requestedAmount = ticketOptionIdGroupedInput[id].Value;
            var totalAmount = ticketOptionIdDb[id].AmountAvailable;
            var bookedAmount = ticketOptionIdOrderedDb.GetValueOrDefault(id, new(default, 0)).Value;

            if (bookedAmount + requestedAmount > totalAmount)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> IsTicketOwner(Guid ticketId, Guid userId)
    {
        using var dbContext = DbContext;
        return await dbContext.Tickets
            .Include(t => t.Attendee)
                .ThenInclude(a => a.Account)
            .Where(t => t.Id == ticketId && t.Attendee.Account.Id == userId.ToString())
            .AnyAsync();
    }

    public async Task<bool> IsTicketOrganizer(Guid ticketId, Guid userId)
    {
        using var dbContext = DbContext;
        return await dbContext.Tickets
            .Include(t => t.TicketOption)
                .ThenInclude(to => to.Event)
                    .ThenInclude(to => to.Organizer)
                        .ThenInclude(to => to.Account)
            .Where(t =>
                t.Id == ticketId && t.TicketOption.Event.Organizer.Account.Id == userId.ToString())
            .AnyAsync();
    }

    [return: NotNullIfNotNull(nameof(dbTicket))]
    private static Data.Model.Ticket? ToModel(Data.Db.Ticket? dbTicket)
    {
        if (dbTicket is null)
        {
            return null;
        }

        var emptyEvent = Activator.CreateInstance<Data.Model.Event>();
        emptyEvent.Id = dbTicket.TicketOption.Event.Id;
        emptyEvent.Organizer = Activator.CreateInstance<Data.Model.Organizer>();
        emptyEvent.Organizer.Id = Guid.Parse(dbTicket.TicketOption.Event.Organizer.Account.Id);

        return new()
        {
            Id = dbTicket.Id,
            Timestamp = dbTicket.Timestamp,
            Attendee = new()
            {
                Id = Guid.Parse(dbTicket.Attendee.Account.Id),
            },
            Event = emptyEvent,
            TicketOption = new()
            {
                Id = dbTicket.TicketOption.Id,
                Name = dbTicket.TicketOption.Name,
                AdditionalPrice = dbTicket.TicketOption.AdditionalPrice,
                AmountAvailable = dbTicket.TicketOption.AmountAvailable
            },
            Price = dbTicket.Price,
            IsReviewed = dbTicket.IsReviewed,
            HolderFullName = dbTicket.HolderFullName,
            HolderEmail = dbTicket.HolderEmail,
            HolderPhoneNumber = dbTicket.HolderPhoneNumber
        };
    }
}
