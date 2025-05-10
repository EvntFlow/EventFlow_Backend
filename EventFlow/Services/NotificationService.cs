using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class NotificationService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async IAsyncEnumerable<Data.Model.Notification> GetNotificationsAsync(Guid userId)
    {
        using var dbContext = DbContext;
        var query = dbContext.Notifications
            .Where(n => n.Account.Id == $"{userId}")
            .OrderByDescending(n => n.Timestamp)
            .AsAsyncEnumerable();

        await foreach (var notification in query)
        {
            yield return new()
            {
                Id = notification.Id,
                Timestamp = notification.Timestamp,
                Topic = notification.Topic,
                Message = notification.Message,
            };
        }
    }

    public async Task SendNotificationAsync(
        Guid userId,
        Data.Model.Notification notification
    )
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var account = await dbContext.Users.SingleAsync(u => u.Id == $"{userId}");

        await dbContext.Notifications.AddAsync(new ()
        {
            Timestamp = notification.Timestamp,
            Account = account,
            Topic = notification.Topic,
            Message = notification.Message
        });

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
