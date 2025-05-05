using EventFlow.Data;
using EventFlow.Data.Model;
using EventFlow.Services;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class Given_TicketService : BaseTest
{
    [Test]
    public async Task When_CreateTicket()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var @event = await dbContext.AddEventAsync(
            organizer,
            startDate: DateTime.Today.AddDays(1),
            endDate: DateTime.Today.AddDays(2)
        );
        var ticketOption1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);

        var tickets = new List<Guid>() { ticketOption1.Id, ticketOption2.Id, ticketOption1.Id  }
            .Select(id =>
            {
                var ticket = Activator.CreateInstance<Ticket>();
                ticket.Attendee = new() { Id = accountId };
                ticket.TicketOption = Activator.CreateInstance<TicketOption>();
                ticket.TicketOption.Id = id;
                ticket.Price = 0.1m; // Deal: 2 normal tickets + 1 premium tickets for 0.1 each.
                ticket.HolderEmail = "admin@ef.trungnt2910.com";
                ticket.HolderFullName = "EventFlow Administrator";
                ticket.HolderPhoneNumber = "0123456789";
                return ticket;
            });
        await ticketService.CreateTicket(tickets);

        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(3));

        var retrievedTickets = await ticketService.GetTickets(accountId).ToListAsync();
        // Ticket price is preserved, regardless of option price.
        Assert.That(retrievedTickets.All(t => t.Price == 0.1m));
    }

    [Test]
    public async Task When_GetTicket()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption = await dbContext.AddTicketOptionAsync(@event);
        var ticket = await dbContext.AddTicketAsync(attendee, ticketOption, 0.5m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);
        var retrievedTicket = await ticketService.GetTicket(ticket.Id);
        var retrievedNull = await ticketService.GetTicket(Guid.Empty);
        var retrievedBad = await ticketService.GetTicket(Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(retrievedNull, Is.Null);
            Assert.That(retrievedBad, Is.Null);
            Assert.That(retrievedTicket, Is.Not.Null);
            Assert.That(retrievedTicket!.Price, Is.EqualTo(0.5m));
        });
    }

    [Test]
    public async Task When_DeleteTicket()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);
        var ticket1 = await dbContext.AddTicketAsync(attendee, ticketOption1, 0.0m);
        var ticket2 = await dbContext.AddTicketAsync(attendee, ticketOption2, 0.5m);

        await dbContext.SaveChangesAsync();

        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(2));

        var ticketService = new TicketService(_dbOptions);
        await ticketService.DeleteTicket(ticket2.Id);
        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task When_ReviewTicket()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);
        var ticket1 = await dbContext.AddTicketAsync(attendee, ticketOption1, 0.0m);
        var ticket2 = await dbContext.AddTicketAsync(attendee, ticketOption2, 0.5m);

        await dbContext.SaveChangesAsync();

        Assert.That(await dbContext.Tickets.Where(t => t.IsReviewed).CountAsync(), Is.EqualTo(0));

        var ticketService = new TicketService(_dbOptions);
        await ticketService.ReviewTicket(ticket2.Id);
        Assert.That(
            (await dbContext.Tickets.Where(t => t.IsReviewed).SingleAsync()).Id,
            Is.EqualTo(ticket2.Id)
        );
        var retrievedTicket = await ticketService.GetTicket(ticket2.Id);
        Assert.That(retrievedTicket?.IsReviewed, Is.EqualTo(true));
    }

    [Test]
    public async Task When_CheckTicketOptionAvailable()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var event1 = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(event1);
        var ticketOption2 = await dbContext.AddTicketOptionAsync(
            event1, name: "Premium", additionalPrice: 1.0m, amountAvailable: 2
        );

        var event2 = await dbContext.AddEventAsync(organizer);
        var ticketOption3 = await dbContext.AddTicketOptionAsync(event2, amountAvailable: 1);
        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);
        await Assert.MultipleAsync(async () =>
        {
            // No tickets.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ ticketOption1.Id ]), Is.False
            );
            // Two tickets.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ ticketOption2.Id ]), Is.True
            );
            // One ticket.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ ticketOption3.Id ]), Is.True
            );
            // Bad ticket.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ Guid.NewGuid() ]), Is.False
            );
            // One is not available.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ticketOption1.Id, ticketOption2.Id]),
                Is.False
            );
            // Available but from different events.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ticketOption2.Id, ticketOption3.Id]),
                Is.False
            );
            // OK, 2 tickets available.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ticketOption2.Id, ticketOption2.Id]),
                Is.True
            );
            // One is not available.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([
                    ticketOption2.Id, ticketOption1.Id, ticketOption2.Id
                ]), Is.False
            );
            // One is invalid.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([
                    ticketOption2.Id, Guid.NewGuid(), ticketOption2.Id
                ]), Is.False
            );
        });

        var ticket = await dbContext.AddTicketAsync(attendee, ticketOption2);
        await dbContext.SaveChangesAsync();

        await Assert.MultipleAsync(async () =>
        {
            // One remaining ticket.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ ticketOption2.Id ]), Is.True
            );
            // Fail, only one ticket left.
            Assert.That(
                await ticketService.IsTicketOptionAvailable([ticketOption2.Id, ticketOption2.Id]),
                Is.False
            );
        });
    }

    [Test]
    public async Task When_CheckTicketOwner()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var account1Id = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var account2Id = Guid.Parse(account2.Id);

        var organizer = await dbContext.AddOrganizerAsync(account1);
        var attendee1 = await dbContext.AddAttendeeAsync(account1);
        var attendee2 = await dbContext.AddAttendeeAsync(account2);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);
        var ticket1 = await dbContext.AddTicketAsync(attendee1, ticketOption1, 0.0m);
        var ticket2 = await dbContext.AddTicketAsync(attendee2, ticketOption2, 0.5m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await ticketService.IsTicketOwner(ticket1.Id, account1Id), Is.True);
            Assert.That(await ticketService.IsTicketOwner(ticket1.Id, account2Id), Is.False);
            Assert.That(await ticketService.IsTicketOwner(ticket2.Id, account1Id), Is.False);
            Assert.That(await ticketService.IsTicketOwner(ticket2.Id, account2Id), Is.True);

            Assert.That(await ticketService.IsTicketOwner(Guid.Empty, account1Id), Is.False);
            Assert.That(await ticketService.IsTicketOwner(Guid.Empty, account2Id), Is.False);
            Assert.That(await ticketService.IsTicketOwner(Guid.NewGuid(), account1Id), Is.False);
            Assert.That(await ticketService.IsTicketOwner(Guid.NewGuid(), account2Id), Is.False);

            Assert.That(await ticketService.IsTicketOwner(ticket1.Id, Guid.Empty), Is.False);
            Assert.That(await ticketService.IsTicketOwner(ticket1.Id, Guid.NewGuid()), Is.False);
            Assert.That(await ticketService.IsTicketOwner(ticket2.Id, Guid.Empty), Is.False);
            Assert.That(await ticketService.IsTicketOwner(ticket2.Id, Guid.NewGuid()), Is.False);
        });
    }

    [Test]
    public async Task When_CheckTicketOrganizer()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var account1Id = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var account2Id = Guid.Parse(account2.Id);

        var organizer1 = await dbContext.AddOrganizerAsync(account1);
        var organizer2 = await dbContext.AddOrganizerAsync(account2);
        var attendee = await dbContext.AddAttendeeAsync(account1);

        var event1 = await dbContext.AddEventAsync(organizer1);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(event1);
        var ticket1 = await dbContext.AddTicketAsync(attendee, ticketOption1, 0.0m);

        var event2 = await dbContext.AddEventAsync(organizer2);
        var ticketOption2 = await dbContext.AddTicketOptionAsync(event2);
        var ticket2 = await dbContext.AddTicketAsync(attendee, ticketOption2, 0.0m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await ticketService.IsTicketOrganizer(ticket1.Id, account1Id), Is.True);
            Assert.That(await ticketService.IsTicketOrganizer(ticket1.Id, account2Id), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(ticket2.Id, account1Id), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(ticket2.Id, account2Id), Is.True);

            Assert.That(await ticketService.IsTicketOrganizer(Guid.Empty, account1Id), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(Guid.Empty, account2Id), Is.False);
            Assert.That(
                await ticketService.IsTicketOrganizer(Guid.NewGuid(), account1Id), Is.False
            );
            Assert.That(
                await ticketService.IsTicketOrganizer(Guid.NewGuid(), account2Id), Is.False
            );

            Assert.That(await ticketService.IsTicketOrganizer(ticket1.Id, Guid.Empty), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(ticket1.Id, Guid.NewGuid()), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(ticket2.Id, Guid.Empty), Is.False);
            Assert.That(await ticketService.IsTicketOrganizer(ticket2.Id, Guid.NewGuid()), Is.False);
        });
    }
}
