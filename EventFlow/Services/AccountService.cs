using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class AccountService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async Task CreateAttendee(Guid userId)
    {
        var userIdString = userId.ToString();

        using var dbContext = DbContext;
        await using var trasaction = await dbContext.Database.BeginTransactionAsync();

        if (!await dbContext.Attendees.AnyAsync(a => a.Account.Id == userIdString))
        {
            var account = await dbContext.Users.SingleAsync(a => a.Id == userIdString);

            await dbContext.Attendees.AddAsync(new Data.Db.Attendee
            {
                Account = account
            });

            await dbContext.SaveChangesAsync();
            await trasaction.CommitAsync();
        }
    }

    public async Task CreateOrganizer(Guid userId)
    {
        var userIdString = userId.ToString();

        using var dbContext = DbContext;
        await using var trasaction = await dbContext.Database.BeginTransactionAsync();

        if (!await dbContext.Organizers.AnyAsync(a => a.Account.Id == userIdString))
        {
            var account = await dbContext.Users.SingleAsync(a => a.Id == userIdString);

            await dbContext.Organizers.AddAsync(new Data.Db.Organizer
            {
                Account = account
            });

            await dbContext.SaveChangesAsync();
            await trasaction.CommitAsync();
        }
    }

    public async Task<bool> IsValidAttendee(Guid userId)
    {
        using var dbContext = DbContext;
        return await dbContext.Attendees.AnyAsync(a => a.Account.Id == userId.ToString());
    }

    public async Task<bool> IsValidOrganizer(Guid userId)
    {
        using var dbContext = DbContext;
        return await dbContext.Organizers.AnyAsync(a => a.Account.Id == userId.ToString());
    }
}
