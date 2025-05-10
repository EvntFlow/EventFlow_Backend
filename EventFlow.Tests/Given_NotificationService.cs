using EventFlow.Data;
using EventFlow.Services;

namespace EventFlow.Tests;

public class Given_NotificationService : BaseTest
{
    [Test]
    public async Task When_SendNotification()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        await dbContext.SaveChangesAsync();

        var notificationService = new NotificationService(_dbOptions);

        // No notifications yet.
        Assert.That(
            await notificationService.GetNotificationsAsync(accountId).CountAsync(),
            Is.EqualTo(0)
        );

        var notification = new Data.Model.Notification()
        {
            Id = Guid.Empty,
            Timestamp = DateTime.UtcNow,
            Topic = "Test",
            Message = "Test Notification"
        };

        await notificationService.SendNotificationAsync(accountId, notification);
        var retrievedNotifications =
            await notificationService.GetNotificationsAsync(accountId).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(retrievedNotifications, Has.Count.EqualTo(1));
            Assert.That(retrievedNotifications[0].Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(retrievedNotifications[0].Timestamp, Is.EqualTo(notification.Timestamp));
            Assert.That(retrievedNotifications[0].Topic, Is.EqualTo(notification.Topic));
            Assert.That(retrievedNotifications[0].Message, Is.EqualTo(notification.Message));
        });

        // Send notification to invalid account
        try
        {
            await notificationService.SendNotificationAsync(Guid.NewGuid(), notification);
            await notificationService.SendNotificationAsync(Guid.NewGuid(), notification);
        }
        catch
        {
            // These may throw
        }
        Assert.That(
            await notificationService.GetNotificationsAsync(accountId).CountAsync(),
            Is.EqualTo(1)
        );

        // Send duplicate notification
        await notificationService.SendNotificationAsync(accountId, notification);
        Assert.That(
            await notificationService.GetNotificationsAsync(accountId).CountAsync(),
            Is.EqualTo(2)
        );
    }
}
