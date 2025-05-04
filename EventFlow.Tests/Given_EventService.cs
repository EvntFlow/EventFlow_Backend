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
        Interested = 0,
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

        var account = await dbContext.AddAccountAsync();
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
        foreach (var category in @event.Categories!)
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
            Assert.That(eventRetrieved.Interested, Is.Zero);
            Assert.That(eventRetrieved.Categories, Has.Count.EqualTo(2));
            Assert.That(eventRetrieved.Categories!.All(c => c.Id != Guid.Empty), Is.True);
            Assert.That(eventRetrieved.TicketOptions, Has.Count.EqualTo(1));
            Assert.That(eventRetrieved.TicketOptions!.All(c => c.Id != Guid.Empty), Is.True);
        });

        // Update event details, then retrieve again
        var newName = @event.Name + " (Updated)";
        eventRetrieved.Name = newName;
        eventRetrieved.Interested = 420;
        eventRetrieved.Categories.Remove(eventRetrieved.Categories.First());
        await eventService.AddOrUpdateEvent(eventRetrieved);
        var eventRetrievedAgain = (await eventService.GetEvent(eventId))!;
        Assert.Multiple(() =>
        {
            Assert.That(eventRetrievedAgain, Is.Not.Null);
            Assert.That(eventRetrievedAgain.Id, Is.EqualTo(eventId));
            Assert.That(eventRetrievedAgain.Name, Is.EqualTo(newName));
            // Database should enforce this one.
            Assert.That(eventRetrievedAgain.Interested, Is.Zero);
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

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        await dbContext.Attendees.AddAsync(new Data.Db.Attendee{ Account = account });
        await dbContext.Organizers.AddAsync(new Data.Db.Organizer{ Account = account });

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        // Add events
        const int EVENT_COUNT = 3;
        var eventService = new EventService(_dbOptions);
        var baseDate = DateTime.Now.Date.AddHours(12);
        for (int i = 1; i <= EVENT_COUNT; ++i)
        {
            var @event = TestEvent;
            @event.Description += $" (Test{i})";
            @event.Organizer.Id = accountId;
            foreach (var category in @event.Categories!)
            {
                category.Id = dbContext.Categories.Single(c => c.Name == category.Name).Id;
            }
            @event.StartDate = baseDate.AddDays(i);
            @event.EndDate = baseDate.AddDays(i).AddHours(2);
            @event.Location = $"Location{i}";
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
        Assert.That(resultIds1_1, Has.Count.EqualTo(EVENT_COUNT));

        var resultIds1_2 = await eventService.FindEvents(
            category: [ (await dbContext.Categories.SingleAsync(c => c.Name == "Test2")).Id ]
        ).Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds1_2, Has.Count.EqualTo(0));

        // [REGRESSION] Find by category, empty set
        var resultIds1_Empty = await eventService.FindEvents(category: [ ])
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds1_Empty, Has.Count.EqualTo(EVENT_COUNT));

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

        // Find by location
        var resultIds4_1 = await eventService.FindEvents(location: ["Location1"])
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds4_1, Has.Count.EqualTo(1));    // One matching

        var resultIds4_2 = await eventService.FindEvents(location: ["location2", "Location3"])
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds4_2, Has.Count.EqualTo(2));    // Two matching

        var resultIds4_3 = await eventService.FindEvents(location: ["Location2", "Location4"])
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds4_3, Has.Count.EqualTo(1));    // One real location, one fake

        var resultIds4_Empty = await eventService.FindEvents(location: [])
            .Select(e => e.Id).ToHashSetAsync();
        Assert.That(resultIds4_Empty, Has.Count.EqualTo(3));    // All matching

        // Find by keywords
        var resultIds5 = await eventService.FindEvents(keywords: "(test1) (test2)")
            .Select(e => e.Id).ToHashSetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(resultIds5, Has.Member(eventIds[0]));
            Assert.That(resultIds5, Has.Member(eventIds[1]));
            Assert.That(resultIds5, Has.No.Member(eventIds[2]));
        });
    }

    [Test]
    public async Task When_SaveEvent()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
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
        @event.Categories!.Clear();
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

        // Fetch from DB
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

        // Fetch from Service/API
        var retrievedEvent = await eventService.GetSavedEvents(accountId).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent.Id, Is.EqualTo(eventId));
            Assert.That(retrievedEvent.Interested, Is.EqualTo(1));
        });

        // CheckSavedEvents
        var savedEventsDictionary = await eventService.CheckSavedEvents(
            accountId, [eventId, Guid.NewGuid()]
        ).ToDictionaryAsync();
        Assert.Multiple(() =>
        {
            Assert.That(savedEventsDictionary, Has.Count.EqualTo(1));
            Assert.That(savedEventsDictionary[eventId], Is.EqualTo(savedEvent.Id));
        });

        // Unsave event
        await eventService.UnsaveEvent(accountId, savedEventId: savedEvent.Id);
        Assert.That(await eventService.GetSavedEvents(accountId).CountAsync(), Is.EqualTo(0));

        // Interested count should reduce
        retrievedEvent = (await eventService.GetEvent(eventId))!;
        Assert.That(retrievedEvent.Interested, Is.Zero);
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

    [Test]
    public async Task When_GetPrice()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = await dbContext.AddAccountAsync();
        var accountId = Guid.Parse(account.Id);

        await dbContext.Organizers.AddAsync(new Data.Db.Organizer{ Account = account });

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        var eventService = new EventService(_dbOptions);

        // Add free event
        var @event = TestEvent;
        @event.Id = Guid.Empty;
        @event.Organizer.Id = accountId;
        @event.Categories!.Clear();
        @event.Price = 0.0m;
        @event.TicketOptions = [
            new()
            {
                Id = Guid.Empty,
                Name = "Test0",
                AdditionalPrice = 0.0m,
                AmountAvailable = 1,
            },
            new()
            {
                Id = Guid.Empty,
                Name = "Test1",
                AdditionalPrice = 1.11m,
                AmountAvailable = 1
            }
        ];
        await eventService.AddOrUpdateEvent(@event);

        var retrievedEvent1 = await eventService.GetEvents(accountId).SingleAsync();
        await CheckPrice(retrievedEvent1);

        // Add paid event
        @event.Id = Guid.Empty;
        @event.Price = 2.0m;
        await eventService.AddOrUpdateEvent(@event);

        var retrievedEvent2 = await eventService.FindEvents(minPrice: 1.5m).SingleAsync();
        await CheckPrice(retrievedEvent2);

        async Task CheckPrice(Event retrievedEvent)
        {
            retrievedEvent = (await eventService.GetEvent(retrievedEvent.Id))!;
            Assert.That(retrievedEvent, Is.Not.Null);
            Assert.That(retrievedEvent.TicketOptions, Is.Not.Null);

            var retrievedIds = retrievedEvent.TicketOptions.Select(t => t.Id).ToList();
            var retrievedPrice = await eventService.GetPrice(retrievedIds).ToDictionaryAsync();

            foreach (var ticketOption in retrievedEvent.TicketOptions)
            {
                var id = ticketOption.Id;
                var original = @event.TicketOptions.Single(to => to.Name == ticketOption.Name);
                var price = @event.Price;
                var additionalPrice = original.AdditionalPrice;
                Assert.That(retrievedPrice[id], Is.EqualTo(price + additionalPrice));
            }
        }
    }

    [Test]
    public async Task When_GetOrganizerFromTicketOption()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var account1Id = Guid.Parse(account1.Id);
        var account2 = await dbContext.AddAccountAsync();
        var account2Id = Guid.Parse(account2.Id);

        await dbContext.Organizers.AddRangeAsync([
            new Data.Db.Organizer{ Account = account1 },
            new Data.Db.Organizer{ Account = account2 }
        ]);

        await dbContext.Categories.AddRangeAsync(
            _categories.Select(c => new Data.Db.Category { Id = c.Id, Name = c.Name })
        );

        await dbContext.SaveChangesAsync();

        var eventService = new EventService(_dbOptions);

        var event1 = TestEvent;
        event1.Organizer.Id = account1Id;
        event1.Categories!.Clear();
        event1.TicketOptions = [
            new()
            {
                Id = Guid.Empty,
                Name = "Test0",
                AdditionalPrice = 0.0m,
                AmountAvailable = 1,
            },
            new()
            {
                Id = Guid.Empty,
                Name = "Test1",
                AdditionalPrice = 1.11m,
                AmountAvailable = 1
            }
        ];
        var event2 = TestEvent;
        event2.Organizer.Id = account2Id;
        event2.Categories!.Clear();

        await eventService.AddOrUpdateEvent(event1);
        await eventService.AddOrUpdateEvent(event2);

        var retrievedEvent1 = await eventService.GetEvents(account1Id).SingleAsync();
        var retrievedEvent2 = await eventService.GetEvents(account2Id).SingleAsync();

        // Fetch again using the single event API to get ticket options.
        retrievedEvent1 = await eventService.GetEvent(retrievedEvent1.Id);
        retrievedEvent2 = await eventService.GetEvent(retrievedEvent2.Id);

        Assert.Multiple(() =>
        {
            Assert.That(retrievedEvent1, Is.Not.Null);
            Assert.That(retrievedEvent2, Is.Not.Null);
            Assert.That(retrievedEvent1!.TicketOptions, Is.Not.Null);
            Assert.That(retrievedEvent2!.TicketOptions, Is.Not.Null);
        });

        var retrievedTicketOptions1 = retrievedEvent1.TicketOptions.Select(t => t.Id).ToList();
        var retrievedTicketOptions2 = retrievedEvent2.TicketOptions.Select(t => t.Id).ToList();

        var organizerId1 = await eventService.GetOrganizerFromTicketOption(retrievedTicketOptions1);
        var organizerId2 = await eventService.GetOrganizerFromTicketOption(retrievedTicketOptions2);
        Assert.Multiple(() =>
        {
            Assert.That(organizerId1, Is.EqualTo(account1Id));
            Assert.That(organizerId2, Is.EqualTo(account2Id));
        });

        Assert.CatchAsync(async () =>
        {
            await eventService.GetOrganizerFromTicketOption(
                retrievedTicketOptions1.Concat(retrievedTicketOptions2).ToList()
            );
        });
    }
}
