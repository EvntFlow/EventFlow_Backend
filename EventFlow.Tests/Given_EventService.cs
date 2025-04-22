using EventFlow.Data;
using EventFlow.Data.Model;
using EventFlow.Services;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class Given_EventService : BaseTest
{
    const string ORGANIZER_NAME = "trungnt2910";

    private readonly Category[] _categories = [ ..
        (new string[]{ "Test1", "Test2", "Test3" })
            .Select(name => new Category { Id = Guid.Empty, Name = name })
    ];

    private Event TestEvent => new Event
    {
        Id = Guid.Empty,
        Organizer = new()
        {
            Id = Guid.Empty,
            Name = string.Empty
        },
        Name = "Test Event",
        Description = "This is a test event",
        StartDate = DateTime.Now.AddHours(12),
        EndDate = DateTime.Now.AddHours(14),
        BannerUri = null,
        Location = "Hanoi",
        Price = 69.42m,
        Categories = [
            new() { Id = Guid.Empty, Name = "Test1" },
            new() { Id = Guid.Empty, Name = "Test3" }
        ],
        TicketOptions = [
            new()
            {
                Id = Guid.Empty,
                Name = "Default",
                AdditionalPrice = 0.00m,
                AmountAvailable = 0
            }
        ]
    };

    [Test]
    public async Task When_AddOrUpdateEvent()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        account.UserName = ORGANIZER_NAME;

        await dbContext.Organizers.AddAsync(new Data.Db.Organizer{ Account = account });

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        // Add event
        var @event = TestEvent;
        var eventService = new EventService(_dbOptions);
        @event.Organizer.Id = accountId;
        foreach (var category in @event.Categories)
        {
            category.Id = dbContext.Categories.Single(c => c.Name == category.Name).Id;
        }
        await eventService.AddOrUpdateEvent(@event);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await dbContext.Events.CountAsync(), Is.EqualTo(1));
            Assert.That(await eventService.GetEvents(accountId).CountAsync(), Is.EqualTo(1));
        });

        // Retrieve that event
        var eventId = (await dbContext.Events.SingleAsync()).Id;
        var eventRetrieved = (await eventService.GetEvent(eventId))!;
        Assert.Multiple(() =>
        {
            Assert.That(eventRetrieved, Is.Not.Null);
            Assert.That(eventRetrieved.Id, Is.EqualTo(eventId));
            Assert.That(eventRetrieved.Organizer.Id, Is.EqualTo(accountId));
            Assert.That(eventRetrieved.Organizer.Name, Is.EqualTo(ORGANIZER_NAME));
            Assert.That(eventRetrieved.Name, Is.EqualTo(@event.Name));
            Assert.That(eventRetrieved.Description, Is.EqualTo(@event.Description));
            Assert.That(eventRetrieved.StartDate, Is.EqualTo(@event.StartDate));
            Assert.That(eventRetrieved.EndDate, Is.EqualTo(@event.EndDate));
            Assert.That(eventRetrieved.BannerUri, Is.EqualTo(@event.BannerUri));
            Assert.That(eventRetrieved.Location, Is.EqualTo(@event.Location));
            Assert.That(eventRetrieved.Price, Is.EqualTo(@event.Price));
            Assert.That(eventRetrieved.Categories, Has.Count.EqualTo(2));
            Assert.That(eventRetrieved.Categories.All(c => c.Id != Guid.Empty), Is.True);
            Assert.That(eventRetrieved.TicketOptions, Has.Count.EqualTo(1));
            Assert.That(eventRetrieved.TicketOptions.All(c => c.Id != Guid.Empty), Is.True);
        });

        // Update event details, then retrieve again
        var newName = @event.Name + " (Updated)";
        eventRetrieved.Name = newName;
        eventRetrieved.Categories.Remove(eventRetrieved.Categories.First());
        await eventService.AddOrUpdateEvent(eventRetrieved);
        var eventRetrievedAgain = (await eventService.GetEvent(eventId))!;
        Assert.Multiple(() =>
        {
            Assert.That(eventRetrievedAgain, Is.Not.Null);
            Assert.That(eventRetrievedAgain.Id, Is.EqualTo(eventId));
            Assert.That(eventRetrievedAgain.Name, Is.EqualTo(newName));
            Assert.That(eventRetrievedAgain.Categories, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task When_GetBadEvent()
    {
        var eventService = new EventService(_dbOptions);
        var @event = await eventService.GetEvent(Guid.NewGuid());
        Assert.That(@event, Is.Null);
    }

    [Test]
    public async Task When_FindEvent()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);

        await dbContext.Attendees.AddAsync(new Data.Db.Attendee{ Account = account });
        await dbContext.Organizers.AddAsync(new Data.Db.Organizer{ Account = account });

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        // Add events
        var eventService = new EventService(_dbOptions);
        var baseDate = DateTime.Now.Date.AddHours(12);
        for (int i = 1; i <= 3; ++i)
        {
            var @event = TestEvent;
            @event.Description += $" (Test{i})";
            @event.Organizer.Id = accountId;
            foreach (var category in @event.Categories)
            {
                category.Id = dbContext.Categories.Single(c => c.Name == category.Name).Id;
            }
            @event.StartDate = baseDate.AddDays(i);
            @event.EndDate = baseDate.AddDays(i).AddHours(2);
            await eventService.AddOrUpdateEvent(@event);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await dbContext.Events.CountAsync(), Is.EqualTo(i));
                Assert.That(
                    await eventService.GetEvents(accountId).CountAsync(), Is.EqualTo(i)
                );
            });
        }

        // Save event
        var eventIds = await dbContext.Events
            .OrderBy(e => e.StartDate)
            .Select(e => e.Id)
            .ToListAsync();

        // Find by category
        var resultIds1_1 = await eventService.FindEvents(
            category: [ (await dbContext.Categories.SingleAsync(c => c.Name == "Test1")).Id ]
        ).Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds1_1, Has.Count.EqualTo(3));

        var resultIds1_2 = await eventService.FindEvents(
            category: [ (await dbContext.Categories.SingleAsync(c => c.Name == "Test2")).Id ]
        ).Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds1_2, Has.Count.EqualTo(0));

        // Find by date
        var resultIds2 = await eventService.FindEvents(
            minDate: baseDate.AddDays(2), maxDate: baseDate.AddDays(10)
        ).Select(e => e.Id).ToHashSetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(resultIds2, Has.No.Member(eventIds[0]));
            Assert.That(resultIds2, Has.Member(eventIds[1]));
            Assert.That(resultIds2, Has.Member(eventIds[2]));
        });

        // Find by price
        var resultIds3_1 = await eventService.FindEvents(minPrice: 0, maxPrice: 100)
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds3_1, Has.Count.EqualTo(3));

        var resultIds3_2 = await eventService.FindEvents(minPrice: 100)
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds3_2, Has.Count.EqualTo(0));

        // Find by keywords
        var resultIds4 = await eventService.FindEvents(keywords: "(test1) (test2)")
            .Select(e => e.Id).ToHashSetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(resultIds4, Has.Member(eventIds[0]));
            Assert.That(resultIds4, Has.Member(eventIds[1]));
            Assert.That(resultIds4, Has.No.Member(eventIds[2]));
        });
    }

    [Test]
    public async Task When_SaveEvent()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);

        await dbContext.Attendees.AddAsync(new Data.Db.Attendee{ Account = account });
        await dbContext.Organizers.AddAsync(new Data.Db.Organizer{ Account = account });

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        // Add event
        var eventService = new EventService(_dbOptions);
        var @event = TestEvent;
        @event.Organizer.Id = accountId;
        @event.Categories.Clear();
        await eventService.AddOrUpdateEvent(@event);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await dbContext.Events.CountAsync(), Is.EqualTo(1));
            Assert.That(await eventService.GetEvents(accountId).CountAsync(), Is.EqualTo(1));
        });

        // Save event
        var eventId = (await dbContext.Events.SingleAsync()).Id;
        try
        {
            await eventService.SaveEvent(accountId, eventId);   // Should succeed
            await eventService.SaveEvent(accountId, eventId);   // Should fail
        }
        catch
        {
            // Ignore
        }
        Assert.That(await dbContext.SavedEvents.CountAsync(), Is.EqualTo(1));

        var savedEvent = await dbContext.SavedEvents
            .Include(se => se.Event)
            .Include(se => se.Attendee)
                .ThenInclude(a => a.Account)
            .SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(Guid.Parse(savedEvent.Attendee.Account.Id), Is.EqualTo(accountId));
            Assert.That(savedEvent.Event.Id, Is.EqualTo(eventId));
        });
    }

    [Test]
    public async Task When_GetCategories()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);
        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );
        await dbContext.SaveChangesAsync();

        var eventService = new EventService(_dbOptions);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(
                await eventService.GetCategories().CountAsync(), Is.EqualTo(_categories.Length)
            );
            Assert.That(
                await eventService.GetCategories().AllAsync(c => c.Id != Guid.Empty), Is.True
            );
        });
    }

    [Test]
    public async Task When_ValidateCategory()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);
        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );
        await dbContext.SaveChangesAsync();

        var validCategories = await dbContext.Categories.Select(c => c.Id).ToArrayAsync();

        var eventService = new EventService(_dbOptions);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await eventService.IsValidCategory(validCategories), Is.True);
            Assert.That(await eventService.IsValidCategory([validCategories[0]]), Is.True);

            Assert.That(await eventService.IsValidCategory([
               ..validCategories, Guid.NewGuid()
            ]), Is.False);
            Assert.That(await eventService.IsValidCategory([Guid.NewGuid()]), Is.False);
            Assert.That(await eventService.IsValidCategory([Guid.Empty]), Is.False);
        });
    }
}
