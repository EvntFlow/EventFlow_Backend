using EventFlow.Data;
using EventFlow.Services;

namespace EventFlow.Tests;

public class Given_NotificationService : BaseTest
{
    [Test]
    public async Task When_GetNotifications()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        await dbContext.SaveChangesAsync();

        using var notificationService = new NotificationService(_dbOptions);

        // No notifications yet.
        Assert.That(
            await notificationService.GetNotificationsAsync(accountId).CountAsync(),
            Is.EqualTo(0)
        );
    }
}
