using EventFlow.Data;
using EventFlow.Services;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class Given_AccountService : BaseTest
{
    [Test]
    public async Task When_CreateAttendee()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        await dbContext.SaveChangesAsync();

        var accountService = new AccountService(_dbOptions);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await accountService.IsValidAttendee(accountId), Is.False);
            Assert.That(await accountService.GetAttendee(accountId), Is.Null);
        });

        // New attendee created.
        await accountService.CreateAttendee(accountId);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await dbContext.Attendees.CountAsync(), Is.EqualTo(1));
            Assert.That(await accountService.IsValidAttendee(accountId), Is.True);
            Assert.That((await accountService.GetAttendee(accountId))?.Id, Is.EqualTo(accountId));
        });

        // Profile already created.
        await accountService.CreateAttendee(accountId);
        Assert.That(await dbContext.Attendees.CountAsync(), Is.EqualTo(1));

        // Account does not exist.
        var invalidId = Guid.NewGuid();
        try
        {
            await accountService.CreateAttendee(invalidId);
        }
        catch
        {
            // This may throw, but things should still be alright.
        }
        Assert.That(await dbContext.Attendees.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task When_CreateOrganizer()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        await dbContext.SaveChangesAsync();

        var accountService = new AccountService(_dbOptions);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await accountService.IsValidOrganizer(accountId), Is.False);
            Assert.That(await accountService.GetOrganizer(accountId), Is.Null);
        });

        // New organizer created.
        await accountService.CreateOrganizer(accountId);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await dbContext.Organizers.CountAsync(), Is.EqualTo(1));
            Assert.That(await accountService.IsValidOrganizer(accountId), Is.True);
            Assert.That((await accountService.GetOrganizer(accountId))?.Id, Is.EqualTo(accountId));
        });

        // Profile already created.
        await accountService.CreateOrganizer(accountId);
        Assert.That(await dbContext.Organizers.CountAsync(), Is.EqualTo(1));

        // Account does not exist.
        var invalidId = Guid.NewGuid();
        try
        {
            await accountService.CreateOrganizer(invalidId);
        }
        catch
        {
            // This may throw, but things should still be alright.
        }
        Assert.That(await dbContext.Organizers.CountAsync(), Is.EqualTo(1));

        // Organizer may also be an attendee.
        await accountService.CreateAttendee(accountId);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await dbContext.Attendees.CountAsync(), Is.EqualTo(1));
            Assert.That(await dbContext.Organizers.CountAsync(), Is.EqualTo(1));

            Assert.That(await accountService.IsValidAttendee(accountId), Is.True);
            Assert.That(await accountService.IsValidOrganizer(accountId), Is.True);

            Assert.That((await accountService.GetAttendee(accountId))?.Id, Is.EqualTo(accountId));
            Assert.That((await accountService.GetOrganizer(accountId))?.Id, Is.EqualTo(accountId));
        });
    }
}
