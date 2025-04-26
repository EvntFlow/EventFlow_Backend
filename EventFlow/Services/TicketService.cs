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
            .Where(t => t.Attendee.Account.Id == userId.ToString());

        await foreach (var dbTicket in query.ToAsyncEnumerable())
        {
            var emptyEvent = Activator.CreateInstance<Data.Model.Event>();
            emptyEvent.Id = dbTicket.TicketOption.Event.Id;

            yield return new()
            {
                Id = dbTicket.Id,
                Attendee = new()
                {
                    Id = Guid.Parse(dbTicket.Attendee.Account.Id)
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
                IsReviewed = dbTicket.IsReviewed
            };
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
        if (dbTicket is null)
        {
            return null;
        }

        var emptyEvent = Activator.CreateInstance<Data.Model.Event>();
        emptyEvent.Id = dbTicket.TicketOption.Event.Id;
        emptyEvent.Organizer = new()
        {
            Id = Guid.Parse(dbTicket.TicketOption.Event.Organizer.Account.Id),
            Name = string.Empty
        };

        return new()
        {
            Id = dbTicket.Id,
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
            IsReviewed = dbTicket.IsReviewed
        };
    }

    public async Task CreateTicket(IEnumerable<Data.Model.Ticket> tickets)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbTickets = tickets.Select(ticket => new Data.Db.Ticket()
        {
            TicketOption =
                dbContext.TicketOptions.Single(to => to.Id == ticket.TicketOption.Id),
            Attendee =
                dbContext.Attendees.Single(a => a.Account.Id == ticket.Attendee.Id.ToString()),
            Price = ticket.Price
        });
        await dbContext.Tickets.AddRangeAsync(dbTickets);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteTicket(Guid ticketId)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Tickets
            .Where(t => t.Id == ticketId)
            .ExecuteDeleteAsync();
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ReviewTicket(Guid ticketId)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var dbTicket = await dbContext.Tickets.SingleAsync(t => t.Id == ticketId);
        if (!dbTicket.IsReviewed)
        {
            dbTicket.IsReviewed = true;
            dbContext.Tickets.Update(dbTicket);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
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
}
