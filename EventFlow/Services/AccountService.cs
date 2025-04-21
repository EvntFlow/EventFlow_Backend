using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class AccountService : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public AccountService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    {
        _dbContext = new(dbContextOptions);
    }

    public void Dispose() => _dbContext.Dispose();

    public async Task CreateAttendee(Guid userId)
    {
        var userIdString = userId.ToString();

        await using var trasaction = await _dbContext.Database.BeginTransactionAsync();

        if (!await _dbContext.Attendees.AnyAsync(a => a.Account.Id == userIdString))
        {
            var account = await _dbContext.Users.SingleAsync(a => a.Id == userIdString);

            await _dbContext.Attendees.AddAsync(new Data.Db.Attendee
            {
                Account = account
            });

            await _dbContext.SaveChangesAsync();

            await trasaction.CommitAsync();
        }
    }

    public async Task CreateOrganizer(Guid userId)
    {
        var userIdString = userId.ToString();

        await using var trasaction = await _dbContext.Database.BeginTransactionAsync();

        if (!await _dbContext.Organizers.AnyAsync(a => a.Account.Id == userIdString))
        {
            var account = await _dbContext.Users.SingleAsync(a => a.Id == userIdString);

            await _dbContext.Organizers.AddAsync(new Data.Db.Organizer
            {
                Account = account
            });

            await _dbContext.SaveChangesAsync();

            await trasaction.CommitAsync();
        }
    }

    public async Task<bool> IsValidAttendee(Guid userId)
    {
        return await _dbContext.Attendees.AnyAsync(a => a.Account.Id == userId.ToString());
    }

    public async Task<bool> IsValidOrganizer(Guid userId)
    {
        return await _dbContext.Organizers.AnyAsync(a => a.Account.Id == userId.ToString());
    }
}
