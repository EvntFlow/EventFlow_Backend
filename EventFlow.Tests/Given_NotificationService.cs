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
            await notificationService.GetNotificationsAsync(accountId, includeRead: false)
                .CountAsync(),
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
            await notificationService.GetNotificationsAsync(accountId, includeRead: false)
                .ToListAsync();
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
            await notificationService.GetNotificationsAsync(accountId, includeRead: false)
                .CountAsync(),
            Is.EqualTo(1)
        );

        // Send duplicate notification
        await notificationService.SendNotificationAsync(accountId, notification);
        Assert.That(
            await notificationService.GetNotificationsAsync(accountId, includeRead: false)
                .CountAsync(),
            Is.EqualTo(2)
        );
    }

    [Test]
    public async Task When_ReadNotifications()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = await dbContext.AddAccountAsync();
        var accountId1 = Guid.Parse(account1.Id);

        var account2 = await dbContext.AddAccountAsync();
        var accountId2 = Guid.Parse(account2.Id);

        await dbContext.SaveChangesAsync();

        var notificationService = new NotificationService(_dbOptions);

        var notification = new Data.Model.Notification()
        {
            Id = Guid.Empty,
            Timestamp = DateTime.UtcNow,
            Topic = "Test",
            Message = "Test Notification"
        };

        // 2 notifications to account 1, 1 to account 2
        await notificationService.SendNotificationAsync(accountId1, notification);
        await notificationService.SendNotificationAsync(accountId1, notification);

        await notificationService.SendNotificationAsync(accountId2, notification);

        // Account 1 should have 2 notifications
        var retrievedNotifications1 =
            await notificationService.GetNotificationsAsync(accountId1, includeRead: false)
                .ToListAsync();
        Assert.That(retrievedNotifications1, Has.Count.EqualTo(2));

        // Account 2 should have 1 notification
        var retrievedNotifications2 =
            await notificationService.GetNotificationsAsync(accountId2, includeRead: false)
                .ToListAsync();
        Assert.That(retrievedNotifications2, Has.Count.EqualTo(1));

        // Account 1 reads all notifications
        await notificationService.ReadNotificationsAsync(accountId1);

        // Notifications in account 1 are cleared.
        retrievedNotifications1 =
            await notificationService.GetNotificationsAsync(accountId1, includeRead: false)
                .ToListAsync();
        Assert.That(retrievedNotifications1, Has.Count.EqualTo(0));

        // Notifications in account 2 remain.
        retrievedNotifications2 =
            await notificationService.GetNotificationsAsync(accountId2, includeRead: false)
                .ToListAsync();
        Assert.That(retrievedNotifications2, Has.Count.EqualTo(1));

        // Account 1 receives a new notification.
        await notificationService.SendNotificationAsync(accountId1, notification);
        retrievedNotifications1 =
            await notificationService.GetNotificationsAsync(accountId1, includeRead: false)
                .ToListAsync();
        Assert.That(retrievedNotifications1, Has.Count.EqualTo(1));

        // Account 1 gets all notifications, including read ones.
        var retrievedNotificationsAll1 =
            await notificationService.GetNotificationsAsync(accountId1, includeRead: true)
                .ToListAsync();
        Assert.That(retrievedNotificationsAll1, Has.Count.EqualTo(3));
    }
}
