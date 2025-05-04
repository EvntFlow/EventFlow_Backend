using EventFlow.Data;
using EventFlow.Data.Db;

namespace EventFlow.Tests;

static class DbContextMockExtensions
{
    internal static async Task<Account> AddAccountAsync(this ApplicationDbContext dbContext)
    {
        return (await dbContext.Users.AddAsync(new())).Entity;
    }

    internal static async Task<Attendee> AddAttendeeAsync(
        this ApplicationDbContext dbContext,
        Account account
    )
    {
        return (await dbContext.Attendees.AddAsync(new() { Account = account })).Entity;
    }

    internal static async Task<Organizer> AddOrganizerAsync(
        this ApplicationDbContext dbContext,
        Account account
    )
    {
        return (await dbContext.Organizers.AddAsync(new() { Account = account })).Entity;
    }

    internal static async Task<Event> AddEventAsync(
        this ApplicationDbContext dbContext,
        Organizer organizer,
        string name = "Test",
        string description = "Test",
        DateTime? startDate = null,
        DateTime? endDate = null,
        Uri? bannerUri = null,
        string location = "Hanoi",
        decimal price = 0.0m
    )
    {
        var baseDate = DateTime.Today;
        var effectiveStartDate = startDate ?? baseDate.AddHours(12);
        var effectiveEndDate = endDate ?? baseDate.AddHours(14);

        return (await dbContext.Events.AddAsync(new()
        {
            Organizer = organizer,
            Name = name,
            Description = description,
            StartDate = effectiveStartDate,
            EndDate = effectiveEndDate,
            BannerUri = bannerUri,
            Location = location,
            Price = price,
            Interested = 0
        })).Entity;
    }

    internal static async Task<TicketOption> AddTicketOptionAsync(
        this ApplicationDbContext dbContext,
        Event @event,
        string name = "Default",
        string? description = null,
        decimal additionalPrice = 0.0m,
        int amountAvailable = 0
    )
    {
        return (await dbContext.TicketOptions.AddAsync(new()
        {
            Event = @event,
            Name = name,
            Description = description,
            AdditionalPrice = additionalPrice,
            AmountAvailable = amountAvailable
        })).Entity;
    }

    internal static async Task<Ticket> AddTicketAsync(
        this ApplicationDbContext dbContext,
        Attendee attendee,
        TicketOption ticketOption,
        decimal price = 0.0m
    )
    {
        return (await dbContext.Tickets.AddAsync(new()
        {
            Attendee = attendee,
            TicketOption = ticketOption,
            Price = price
        })).Entity;
    }
}
