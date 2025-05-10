using System.Globalization;
using Bogus;
using CountryData.Bogus;
using EventFlow.Data;
using EventFlow.Data.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<Account>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddDataProtection();

var app = builder.Build();

using var userManager = app.Services.GetRequiredService<UserManager<Account>>();

using var dbContext = app.Services.GetRequiredService<ApplicationDbContext>();
using var transaction = await dbContext.Database.BeginTransactionAsync();

// Generate all the required event categories.
string[] categoryNames = [
    "Music", "Nightlife", "Performing", "Dating", "Marketing", "Business", "Food and Drinks"
];
foreach (var categoryName in categoryNames)
{
    var dbCategory = await dbContext.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
    dbCategory ??= new()
    {
        Name = categoryName,
        ImageUri = null
    };
    dbContext.Categories.Update(dbCategory);
}
await dbContext.SaveChangesAsync();

// Generate at least 100 accounts. Passwords will be in a determined format.
var testAccount = new Faker<Task<Account>>(locale: "en_AU")
    .StrictMode(false)
    .CustomInstantiator(async (f) =>
    {
        var l = CountryDataSet.Australia().Place();
        var a = new Account()
        {
            FirstName = f.Person.FirstName,
            LastName = f.Person.LastName,
            Website = $"https://{f.Person.LastName}.{f.Internet.DomainSuffix()}".ToLowerInvariant(),
            Company = $"{f.Person.LastName} {f.Company.CompanySuffix()}",

            Country = l.Community.Province.State.Country.Name,
            City = l.Name,
            Postcode = l.PostCode
        };

        await userManager.SetEmailAsync(a, f.Internet.Email(a.FirstName, a.LastName));
        await userManager.SetUserNameAsync(a, await userManager.GetEmailAsync(a));
        await userManager.SetPhoneNumberAsync(a, f.Phone.PhoneNumber());

        return a;
    });

while (await dbContext.Users.CountAsync() < 100)
{
    var account = await testAccount.Generate();
    var password = $"Passw0rd_{account.Email}";

    var result = await userManager.CreateAsync(account);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(string.Join("\n", result.Errors));
    }

    result = await userManager.AddPasswordAsync(account, password);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(string.Join("\n", result.Errors));
    }

    var token = await userManager.GenerateEmailConfirmationTokenAsync(account);
    result = await userManager.ConfirmEmailAsync(account, token);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(string.Join("\n", result.Errors));
    }
    await dbContext.SaveChangesAsync();
}

// Generate at least 10 organizers, sampled from these accounts.
var testOrganizer = new Faker<Task<Organizer>>(locale: "en_AU")
    .StrictMode(false)
    .CustomInstantiator(async f =>
    {
        var o = new Organizer()
        {
            Account = f.PickRandom(await dbContext.Users
                .OrderBy(u => u.UserName)
                .Where(a => !dbContext.Organizers.Where(o => o.Account == a).Any())
                .ToListAsync())
        };
        return o;
    });

while (await dbContext.Organizers.CountAsync() < 10)
{
    await dbContext.Organizers.AddAsync(await testOrganizer.Generate());
    await dbContext.SaveChangesAsync();
}

// Generate at least 50 events. 20% will be online.
// Each event will have 2 - 4 ticket options (with a very high ticket limit).
var testEvent = new Faker<Task<Event>>(locale: "en_AU")
    .StrictMode(false)
    .CustomInstantiator(async f =>
    {
        var l = CountryDataSet.Australia().Place();

        var e = new Event()
        {
            Organizer = f.PickRandom(await dbContext.Organizers.ToArrayAsync()),
            Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                $"{f.Hacker.Adjective()} {f.Hacker.Noun()} {f.Hacker.IngVerb()}"
            ),
            Description = f.Hacker.Phrase(),
            StartDate = f.Date.Soon(30),
            EndDate = DateTime.MinValue,
            BannerUri = f.Random.Number(0, 9) == 0 ?
                null : new Uri(f.Image.PicsumUrl()),
            Location = f.Random.Number(0, 4) == 0 ?
                "Online" : $"{l.Name}, {l.Community.Province.State.Code}",
            Price = f.Random.Number(0, 1) * Math.Round(f.Random.Decimal(0, 100) * 20) / 20,
            Interested = 0,
            Sold = 0
        };

        e.StartDate = e.StartDate.Date.AddHours(e.StartDate.Hour);
        e.EndDate = e.StartDate.AddHours(f.Random.Number(1, 24) * f.Random.Number(1, 4) / 4.0);

        e.StartDate = e.StartDate.ToUniversalTime();
        e.EndDate = e.EndDate.ToUniversalTime();

        return e;
    });

var testTicketOption = new Faker<Task<TicketOption>>(locale: "en_AU")
    .RuleSet("Normal", r =>
    {
        r
            .StrictMode(false)
            .CustomInstantiator(f =>
                Task.FromResult(new TicketOption()
                {
                    Event = null!,
                    Name = f.PickRandom("Default", "Normal", "General Admission", "Basic"),
                    Description = f.Hacker.Phrase(),
                    AdditionalPrice = 0.0m,
                    AmountAvailable = f.Random.Number(1, 100) * 10
                })
            );
    })
    .RuleSet("VIP", r =>
    {
        r
            .StrictMode(false)
            .CustomInstantiator(f =>
                Task.FromResult(new TicketOption()
                {
                    Event = null!,
                    Name = f.PickRandom("Premium", "VIP", "Pro"),
                    Description = f.Hacker.Phrase(),
                    AdditionalPrice = Math.Round(f.Random.Decimal(1, 100) * 20) / 20,
                    AmountAvailable = f.Random.Number(1, 100) * 10
                })
            );
    });

var testEventCategory = new Faker<Task<EventCategory[]>>(locale: "en_AU")
    .StrictMode(false)
    .CustomInstantiator(async f => [
        ..f.PickRandom(await dbContext.Categories.ToArrayAsync(), f.Random.Number(0, 5))
            .Select(c => new EventCategory() { Event = null!, Category = c })
    ]);

while (await dbContext.Events.CountAsync() < 50)
{
    var @event = (await dbContext.Events.AddAsync(await testEvent.Generate())).Entity;

    var baseOption = await testTicketOption.Generate("Normal");
    baseOption.Event = @event;

    var vipOptions = await Task.WhenEach(testTicketOption.GenerateBetween(0, 2, "VIP"))
        .Select(async (t, _, _) => { var to = await t; to.Event = @event; return to;  })
        .ToArrayAsync();

    await dbContext.TicketOptions.AddAsync(baseOption);
    await dbContext.TicketOptions.AddRangeAsync(vipOptions);

    await dbContext.EventCategories.AddRangeAsync(
        (await testEventCategory.Generate()).Select(ec => { ec.Event = @event; return ec; })
    );

    await dbContext.SaveChangesAsync();
}

// Generate at least 1000 tickets. It will be assigned to a random TicketOption and Account.
// Accounts without an underlying Attendee will have one profile created.
// Ticket purchase date will be random dates in the past two months.
// Ticket purchase price will have random discounts of up to 50%.

var testTicket = new Faker<Task<Ticket>>(locale: "en_AU")
    .CustomInstantiator(async f =>
    {
        var account = f.PickRandom(await dbContext.Users.ToArrayAsync());
        var attendee = await dbContext.Attendees.SingleOrDefaultAsync(a => a.Account == account);
        if (attendee is null)
        {
            attendee = new Attendee()
            {
                Account = account
            };
            await dbContext.Attendees.AddAsync(attendee);
            await dbContext.SaveChangesAsync();
        }

        var ticketOption = f.PickRandom(await dbContext.TicketOptions
            .Include(to => to.Event)
            .OrderBy(to => to.Event.Organizer.Account.Email)
                .ThenBy(to => to.Event.Name)
                    .ThenBy(to => to.AdditionalPrice)
            .ToArrayAsync()
        );

        return new Ticket()
        {
            Timestamp = f.Date.Recent(60).ToUniversalTime(),
            TicketOption = ticketOption,
            Attendee = attendee,
            Price = Math.Round(
                (ticketOption.Event.Price + ticketOption.AdditionalPrice)
                    * f.Random.Decimal(0.50m, 1.00m) // 50 - 100% discount
                    * 20
            ) / 20,
            HolderFullName = $"{account.FirstName} {account.LastName}",
            HolderEmail =
                f.PickRandom(account.Email!, f.Internet.Email(account.FirstName, account.LastName)),
            HolderPhoneNumber =
                f.PickRandom(account.PhoneNumber ?? f.Phone.PhoneNumber()!, f.Phone.PhoneNumber()),
            IsReviewed = f.Random.Bool()
        };
    });

while (await dbContext.Tickets.CountAsync() < 1000)
{
    await dbContext.Tickets.AddAsync(await testTicket.Generate());
    await dbContext.SaveChangesAsync();
}

// Generate 100 random SavedEvents, assigned to a random Event and Account.
// Accounts without an underlying Attendee will have one profile created.

// Generate at least 1000 tickets. It will be assigned to a random TicketOption and Account.
// Accounts without an underlying Attendee will have one profile created.
// Ticket purchase date will be random dates in the past two months.
// Ticket purchase price will have random discounts of up to 50%.

var testSavedEvent = new Faker<Task<SavedEvent>>(locale: "en_AU")
    .CustomInstantiator(async f =>
    {
        var account = f.PickRandom(await dbContext.Users.ToArrayAsync());
        var attendee = await dbContext.Attendees.SingleOrDefaultAsync(a => a.Account == account);
        if (attendee is null)
        {
            attendee = new Attendee()
            {
                Account = account
            };
            await dbContext.Attendees.AddAsync(attendee);
            await dbContext.SaveChangesAsync();
        }

        var @event = f.PickRandom(await dbContext.Events
            .Where(e =>
                !dbContext.SavedEvents.Any(se => se.Attendee.Account == account && se.Event == e))
            .OrderBy(to => to.Organizer.Account.Email)
                .ThenBy(to => to.Name)
            .ToArrayAsync()
        );

        return new SavedEvent()
        {
            Attendee = attendee,
            Event = @event
        };
    });

while (await dbContext.SavedEvents.CountAsync() < 100)
{
    await dbContext.SavedEvents.AddAsync(await testSavedEvent.Generate());
    await dbContext.SaveChangesAsync();
}

// Enforce consistency.

var testPaymentMethod = new Faker<Task<CardPaymentMethod>>(locale: "en_AU")
    .StrictMode(false)
    .CustomInstantiator(f =>
        Task.FromResult(new CardPaymentMethod()
        {
            Account = null!,
            Type = nameof(CardPaymentMethod),
            Number = f.Finance.CreditCardNumber().Replace('-', ' '),
            Expiry = $"{f.Random.Number(0, 12).ToString().PadLeft(2, '0')}" + '/' +
                $"{f.Random.Number(0, 99).ToString().PadLeft(2, '0')}",
            Name = $"{f.Person.FirstName} {f.Person.LastName}",
            Cvv = f.Finance.CreditCardCvv()
        })
    );

// - Paid event hosts must have a payment method.
foreach (var organizer in await dbContext.Organizers
    .Include(o => o.Account)
    .OrderBy(o => o.Account.UserName)
    .ToListAsync())
{
    if (await dbContext.TicketOptions
        .Where(to =>
            to.Event.Organizer.Account == organizer.Account &&
            (to.AdditionalPrice != 0 || to.Event.Price != 0))
        .AnyAsync())
    {
        var paymentMethod = await dbContext.PaymentMethods
            .FirstOrDefaultAsync(p => p.Account == organizer.Account);
        paymentMethod ??= await testPaymentMethod.Generate();
        paymentMethod.Account = organizer.Account;
        dbContext.Update(paymentMethod);
        await dbContext.SaveChangesAsync();
    }
}

// - Paid ticket holders must have a payment method.
foreach (var attendee in await dbContext.Attendees
    .Include(a => a.Account)
    .OrderBy(a => a.Account.UserName)
    .ToListAsync())
{
    if (await dbContext.Tickets
        .Where(to =>
            to.Attendee.Account == attendee.Account &&
            to.Price != 0)
        .AnyAsync())
    {
        var paymentMethod = await dbContext.PaymentMethods
            .FirstOrDefaultAsync(p => p.Account == attendee.Account);
        paymentMethod ??= await testPaymentMethod.Generate();
        paymentMethod.Account = attendee.Account;
        dbContext.Update(paymentMethod);
        await dbContext.SaveChangesAsync();
    }
}

// - Interested must be the count of SavedEvents
// - Sold must be the count of tickets.
foreach (var @event in await dbContext.Events.ToArrayAsync())
{
    @event.Interested = await dbContext.SavedEvents.Where(se => se.Event == @event).CountAsync();
    @event.Sold = await dbContext.Tickets.Where(t => t.TicketOption.Event == @event).CountAsync();
    dbContext.Update(@event);
    await dbContext.SaveChangesAsync();
}

await transaction.CommitAsync();
