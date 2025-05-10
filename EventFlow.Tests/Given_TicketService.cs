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
        var ticketOption1_1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption1_2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);

        // Ticket option from different event.
        var event2 = await dbContext.AddEventAsync(organizer);
        var ticketOption2 = await dbContext.AddTicketOptionAsync(event2);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);

        var tickets = new Guid[]
            { ticketOption1_1.Id, ticketOption1_2.Id, ticketOption1_1.Id, ticketOption2.Id }
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
            })
            .ToList();

        // Attempt 1: Blocked by ticket option from different event.
        await ticketService.CreateTicket(tickets);
        Assert.That(await dbContext.Tickets.AnyAsync(), Is.False);

        // Remove the ticket at fault.
        tickets.RemoveAt(tickets.Count - 1);

        // Attempt 2: Blocked by false callback.
        await ticketService.CreateTicket(tickets, (_) => Task.FromResult(false));
        Assert.That(await dbContext.Tickets.AnyAsync(), Is.False);

        // Attemp 3: Allow creation.
        await ticketService.CreateTicket(tickets);
        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(3));

        var retrievedTickets = await ticketService.GetTickets(accountId).ToListAsync();
        // Ticket price is preserved, regardless of option price.
        Assert.That(retrievedTickets.All(t => t.Price == 0.1m));

        await dbContext.Entry(@event).ReloadAsync();
        Assert.That(@event.Sold, Is.EqualTo(3));

        // Special: Empty should return a fail result
        Assert.That(await ticketService.CreateTicket([]), Is.False);
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

        var account1 = await dbContext.AddAccountAsync();
        var accountId1 = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var accountId2 = Guid.Parse(account2.Id);

        var organizer = await dbContext.AddOrganizerAsync(account1);
        var attendee1 = await dbContext.AddAttendeeAsync(account1);
        var attendee2 = await dbContext.AddAttendeeAsync(account2);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(@event);
        var ticketOption2 = await dbContext
            .AddTicketOptionAsync(@event, name: "Premium", additionalPrice: 1.0m);
        var ticket1_1 = await dbContext.AddTicketAsync(attendee1, ticketOption1, 0.0m);
        var ticket1_2 = await dbContext.AddTicketAsync(attendee1, ticketOption2, 0.0m);
        var ticket2_1 = await dbContext.AddTicketAsync(attendee2, ticketOption1, 0.5m);
        var ticket2_2 = await dbContext.AddTicketAsync(attendee2, ticketOption2, 0.5m);

        await dbContext.SaveChangesAsync();

        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(4));

        var ticketService = new TicketService(_dbOptions);

        // Simulate a payment failure
        await ticketService.DeleteTicket(ticket1_2.Id, (_) => Task.FromResult(false));
        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(4));

        // Simulate a success
        await ticketService.DeleteTicket(ticket1_2.Id, (_) => Task.FromResult(true));
        Assert.That(await dbContext.Tickets.CountAsync(), Is.EqualTo(3));

        // Delete 1 ticket, so the count should be down by 1.
        await dbContext.Entry(@event).ReloadAsync();
        Assert.That(@event.Sold, Is.EqualTo(-1));

        // Delete all tickets of an event.
        // Simulate a payment failure.
        await ticketService.DeleteTickets(@event.Id,
            (t) => Task.FromResult(t.First().Attendee.Id != accountId2)
        );
        // At least two tickets should linger.
        Assert.That(await dbContext.Tickets.CountAsync(), Is.AtLeast(2));

        // Should be clean now.
        await ticketService.DeleteTickets(@event.Id, (t) => Task.FromResult(true));
        Assert.That(await dbContext.Tickets.CountAsync(), Is.Zero);

        // Delete 3 more tickets, so the count should be down by 3.
        await dbContext.Entry(@event).ReloadAsync();
        Assert.That(@event.Sold, Is.EqualTo(-4));
    }

    [Test]
    public async Task When_UpdateTicket()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        var organizer = await dbContext.AddOrganizerAsync(account);
        var attendee = await dbContext.AddAttendeeAsync(account);

        var @event = await dbContext.AddEventAsync(organizer);
        var ticketOption = await dbContext.AddTicketOptionAsync(@event);
        var ticket = await dbContext.AddTicketAsync(attendee, ticketOption, 0.0m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);
        var retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.That(retrievedEvent, Is.Not.Null);

        // Update Full Name
        const string NEW_FULL_NAME = "Chill Guy";
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.That(retrievedEvent, Is.Not.Null);
        retrievedEvent.HolderFullName = NEW_FULL_NAME;
        await ticketService.UpdateTicket(retrievedEvent);
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent, Is.Not.Null);
            Assert.That(retrievedEvent?.HolderFullName, Is.EqualTo(NEW_FULL_NAME));
            Assert.That(retrievedEvent?.HolderEmail, Is.EqualTo(ticket.HolderEmail));
            Assert.That(retrievedEvent?.HolderPhoneNumber, Is.EqualTo(ticket.HolderPhoneNumber));
        });
        retrievedEvent.HolderFullName = ticket.HolderFullName;
        await ticketService.UpdateTicket(retrievedEvent);

        // Update Email
        const string NEW_EMAIL = "chill@dev.ef.trungnt2910.com";
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.That(retrievedEvent, Is.Not.Null);
        retrievedEvent.HolderEmail = NEW_EMAIL;
        await ticketService.UpdateTicket(retrievedEvent);
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent, Is.Not.Null);
            Assert.That(retrievedEvent?.HolderFullName, Is.EqualTo(ticket.HolderFullName));
            Assert.That(retrievedEvent?.HolderEmail, Is.EqualTo(NEW_EMAIL));
            Assert.That(retrievedEvent?.HolderPhoneNumber, Is.EqualTo(ticket.HolderPhoneNumber));
        });
        retrievedEvent.HolderEmail = ticket.HolderEmail;
        await ticketService.UpdateTicket(retrievedEvent);

        // Update Phone
        const string NEW_PHONE = "0420420420";
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.That(retrievedEvent, Is.Not.Null);
        retrievedEvent.HolderPhoneNumber = NEW_PHONE;
        await ticketService.UpdateTicket(retrievedEvent);
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent, Is.Not.Null);
            Assert.That(retrievedEvent?.HolderFullName, Is.EqualTo(ticket.HolderFullName));
            Assert.That(retrievedEvent?.HolderEmail, Is.EqualTo(ticket.HolderEmail));
            Assert.That(retrievedEvent?.HolderPhoneNumber, Is.EqualTo(NEW_PHONE));
        });
        retrievedEvent.HolderPhoneNumber = ticket.HolderPhoneNumber;
        await ticketService.UpdateTicket(retrievedEvent);

        // Update multiple
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.That(retrievedEvent, Is.Not.Null);
        retrievedEvent.HolderFullName = NEW_FULL_NAME;
        retrievedEvent.HolderPhoneNumber = NEW_PHONE;
        await ticketService.UpdateTicket(retrievedEvent);
        retrievedEvent = await ticketService.GetTicket(ticket.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent, Is.Not.Null);
            Assert.That(retrievedEvent?.HolderFullName, Is.EqualTo(NEW_FULL_NAME));
            Assert.That(retrievedEvent?.HolderEmail, Is.EqualTo(ticket.HolderEmail));
            Assert.That(retrievedEvent?.HolderPhoneNumber, Is.EqualTo(NEW_PHONE));
        });
        retrievedEvent.HolderPhoneNumber = ticket.HolderPhoneNumber;
        await ticketService.UpdateTicket(retrievedEvent);

        // Update invalid
        retrievedEvent.Id = Guid.NewGuid();
        Assert.CatchAsync(async () =>
        {
            await ticketService.UpdateTicket(retrievedEvent);
        });

        // Update empty
        retrievedEvent.Id = Guid.Empty;
        Assert.CatchAsync(async () =>
        {
            await ticketService.UpdateTicket(retrievedEvent);
        });
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
        var retrievedTicket = await ticketService.GetTicket(ticket2.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedTicket, Is.Not.Null);
            Assert.That(retrievedTicket?.IsReviewed, Is.False);
        });

        // Corrupt the name, then review. This attempt should fail.
        retrievedTicket.HolderFullName = "Evil Guy";
        await ticketService.ReviewTicket(retrievedTicket);
        retrievedTicket = await ticketService.GetTicket(ticket2.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedTicket, Is.Not.Null);
            Assert.That(retrievedTicket?.HolderFullName, Is.EqualTo(ticket2.HolderFullName));
            Assert.That(retrievedTicket?.IsReviewed, Is.False);
        });

        // Do the real review.
        await ticketService.ReviewTicket(retrievedTicket);
        Assert.That(
            (await dbContext.Tickets.Where(t => t.IsReviewed).SingleAsync()).Id,
            Is.EqualTo(ticket2.Id)
        );
        retrievedTicket = await ticketService.GetTicket(ticket2.Id);
        Assert.That(retrievedTicket?.IsReviewed, Is.True);

        // Update the ticket. The status should get back to unreviewed.
        retrievedTicket.HolderEmail = "sus@unverified.trungnt2910.com";
        await ticketService.UpdateTicket(retrievedTicket);
        retrievedTicket = await ticketService.GetTicket(ticket2.Id);
        Assert.Multiple(() =>
        {
            Assert.That(retrievedTicket, Is.Not.Null);
            Assert.That(retrievedTicket?.IsReviewed, Is.False);
        });
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

    [Test]
    public async Task When_GetAttendance()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var account1Id = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var account2Id = Guid.Parse(account2.Id);

        var organizer = await dbContext.AddOrganizerAsync(account1);
        var attendee1 = await dbContext.AddAttendeeAsync(account1);
        var attendee2 = await dbContext.AddAttendeeAsync(account2);

        var event1 = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(event1);
        var ticket1 = await dbContext.AddTicketAsync(attendee1, ticketOption1, 0.0m);

        var event2 = await dbContext.AddEventAsync(organizer);
        var ticketOption2 = await dbContext.AddTicketOptionAsync(event2);
        var ticket2_1_1 = await dbContext.AddTicketAsync(attendee1, ticketOption2, 0.0m);
        var ticket2_1_2 = await dbContext.AddTicketAsync(attendee1, ticketOption2, 0.0m);
        var ticket2_2_2 = await dbContext.AddTicketAsync(attendee2, ticketOption2, 0.0m);

        var event3 = await dbContext.AddEventAsync(organizer);
        var ticketOption3 = await dbContext.AddTicketOptionAsync(event3);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);

        var attendanceAll = await ticketService.GetAttendance(account1Id, null).ToListAsync();
        Assert.That(attendanceAll, Has.Count.EqualTo(4));

        var attendanceNone = await ticketService.GetAttendance(account2Id, null).ToListAsync();
        Assert.That(attendanceNone, Is.Empty);

        var attendanceInvalid =
            await ticketService.GetAttendance(Guid.NewGuid(), null).ToListAsync();
        Assert.That(attendanceInvalid, Is.Empty);

        var attendanceEvent1 =
            await ticketService.GetAttendance(account1Id, event1.Id).ToListAsync();
        Assert.That(attendanceEvent1, Has.Count.EqualTo(1));
        var attendanceEvent1_BadAccount =
            await ticketService.GetAttendance(account2Id, event1.Id).ToListAsync();
        Assert.That(attendanceEvent1_BadAccount, Is.Empty);
        var attendanceEvent1_Invalid =
            await ticketService.GetAttendance(Guid.NewGuid(), event1.Id).ToListAsync();
        Assert.That(attendanceEvent1_Invalid, Is.Empty);

        var attendanceEvent2 =
            await ticketService.GetAttendance(account1Id, event2.Id).ToListAsync();
        Assert.That(attendanceEvent2, Has.Count.EqualTo(3));

        var attendanceEvent3 =
            await ticketService.GetAttendance(account1Id, event3.Id).ToListAsync();
        Assert.That(attendanceEvent3, Is.Empty);

        var attendanceEventInvalid =
            await ticketService.GetAttendance(account1Id, Guid.NewGuid()).ToListAsync();
        Assert.That(attendanceEventInvalid, Is.Empty);
    }

    [Test]
    public async Task When_GetStatistics()
    {
        var baseDate =
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 3, 0, 0, 0, DateTimeKind.Utc);

        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var account1Id = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var account2Id = Guid.Parse(account2.Id);

        var organizer = await dbContext.AddOrganizerAsync(account1);
        var attendee1 = await dbContext.AddAttendeeAsync(account1);
        var attendee2 = await dbContext.AddAttendeeAsync(account2);

        var event1 = await dbContext.AddEventAsync(organizer);
        var ticketOption1 = await dbContext.AddTicketOptionAsync(event1);
        var ticket1 = await dbContext.AddTicketAsync(attendee1, ticketOption1, 0.0m);

        var event2 = await dbContext.AddEventAsync(organizer);
        var ticketOption2 = await dbContext.AddTicketOptionAsync(event2);
        var ticket2_1_1 = await dbContext.AddTicketAsync(attendee1, ticketOption2, 0.0m);
        var ticket2_1_2 = await dbContext.AddTicketAsync(
            attendee1, ticketOption2, 1.0m,
            timestamp: baseDate.Date.AddMonths(-1),
            isReviewed: true
        );
        var ticket2_1_3 = await dbContext.AddTicketAsync(
            attendee1, ticketOption2, 1.0m,
            timestamp: baseDate.Date.AddDays(-1),
            isReviewed: true
        );
        var ticket2_1_4 = await dbContext.AddTicketAsync(
            attendee1, ticketOption2, 2.0m,
            timestamp: baseDate.Date
        );
        var ticket2_2_2 = await dbContext.AddTicketAsync(attendee2, ticketOption2, 0.0m);

        var event3 = await dbContext.AddEventAsync(
            organizer,
            startDate: baseDate.Date.AddMonths(-1),
            endDate: baseDate.Date.AddMonths(-1).AddDays(1)
        );

        var event4 = await dbContext.AddEventAsync(
            organizer,
            startDate: baseDate.Date.AddMonths(1),
            endDate: baseDate.Date.AddMonths(1).AddDays(1)
        );
        var ticketOption4 = await dbContext.AddTicketOptionAsync(event4);
        var ticket4 = await dbContext.AddTicketAsync(attendee2, ticketOption4, 0.0m);

        await dbContext.SaveChangesAsync();

        var ticketService = new TicketService(_dbOptions);
        var statsNow = await ticketService.GetStatistics(account1Id, baseDate);

        Assert.Multiple(() =>
        {
            // event1, event2
            Assert.That(statsNow.TotalEvents, Is.EqualTo(2));
            // event1: 1, event2: 4, event4: 1
            Assert.That(statsNow.TotalTickets, Is.EqualTo(6));
            // ticket2_1_3, ticket2_1_4
            Assert.That(statsNow.TotalSales, Is.EqualTo(3.0m));
            // ticket2_1_3
            Assert.That(statsNow.TotalReviewed, Is.EqualTo(1));

            Assert.That(statsNow.DailySales[baseDate.Day - 2], Is.EqualTo(1.0m));
            Assert.That(statsNow.DailySales[baseDate.Day - 1], Is.EqualTo(2.0m));
        });

        var statsEmpty =
            await ticketService.GetStatistics(account1Id, baseDate.AddYears(1));

        Assert.Multiple(() =>
        {
            Assert.That(statsEmpty.TotalEvents, Is.EqualTo(0));
            Assert.That(statsEmpty.TotalTickets, Is.EqualTo(0));
            Assert.That(statsEmpty.TotalSales, Is.EqualTo(0m));
            Assert.That(statsEmpty.TotalReviewed, Is.EqualTo(0));
            Assert.That(statsEmpty.DailySales, Has.All.EqualTo(0.0));
        });

        var statsInvalid = await ticketService.GetStatistics(account2Id, baseDate);
        Assert.Multiple(() =>
        {
            Assert.That(statsEmpty.TotalEvents, Is.EqualTo(0));
            Assert.That(statsEmpty.TotalTickets, Is.EqualTo(0));
            Assert.That(statsEmpty.TotalSales, Is.EqualTo(0m));
            Assert.That(statsEmpty.TotalReviewed, Is.EqualTo(0));
            Assert.That(statsEmpty.DailySales, Has.All.EqualTo(0.0));
        });
    }
}
