using EventFlow.Data;
using EventFlow.Data.Model;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class NotificationService : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    {
        _dbContext = new(dbContextOptions);
    }

    public void Dispose() => _dbContext.Dispose();

    public async IAsyncEnumerable<Notification> GetNotificationsAsync(Guid userId)
    {
        var userIdString = userId.ToString();

        var query = _dbContext.Notifications
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
