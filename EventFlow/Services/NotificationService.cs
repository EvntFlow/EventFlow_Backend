using EventFlow.Data;
using EventFlow.Data.Model;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class NotificationService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async IAsyncEnumerable<Notification> GetNotificationsAsync(Guid userId)
    {
        var userIdString = userId.ToString();

        using var dbContext = DbContext;
        var query = dbContext.Notifications
            .Where(n => n.Account.Id == userIdString)
            .AsAsyncEnumerable();

        await foreach (var notification in query)
        {
            yield return new Notification
            {
                Id = notification.Id,
                Topic = notification.Topic,
                Message = notification.Message,
            };
        }
    }
}
