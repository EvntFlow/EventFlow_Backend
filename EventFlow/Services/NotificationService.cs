using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class NotificationService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async IAsyncEnumerable<Data.Model.Notification> GetNotificationsAsync(
        Guid userId,
        bool includeRead
    )
    {
        using var dbContext = DbContext;
        var query = dbContext.Notifications
            .Where(n => n.Account.Id == $"{userId}");
        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
            query = query.Where(n => n.Timestamp >= DateTime.UtcNow.AddDays(-7));
        }
        query = query.OrderByDescending(n => n.Timestamp);

        await foreach (var notification in query.AsAsyncEnumerable())
        {
            yield return new()
            {
                Id = notification.Id,
                Timestamp = notification.Timestamp,
                Topic = notification.Topic,
                Message = notification.Message,
                IsRead = includeRead ? notification.IsRead : null
            };
        }
    }

    public async Task ReadNotificationsAsync(Guid userId)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        await dbContext.Notifications
            .Where(n => n.Account.Id == $"{userId}")
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(n => n.SetProperty(n => n.IsRead, true));

        await transaction.CommitAsync();
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
            Message = notification.Message,
            IsRead = false
        });

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
