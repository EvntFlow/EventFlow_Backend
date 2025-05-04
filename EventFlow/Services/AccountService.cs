using System.Diagnostics.CodeAnalysis;
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

    public async Task<Data.Model.Attendee?> GetAttendee(Guid userId)
    {
        using var dbContext = DbContext;
        var dbAttendee = await dbContext.Attendees
            .Include(a => a.Account)
            .SingleOrDefaultAsync(a => a.Account.Id == $"{userId}");
        return ToModel(dbAttendee);
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

    public async Task<Data.Model.Organizer?> GetOrganizer(Guid userId)
    {
        using var dbContext = DbContext;
        var dbOrganizer = await dbContext.Organizers
            .Include(a => a.Account)
            .SingleOrDefaultAsync(a => a.Account.Id == $"{userId}");
        return ToModel(dbOrganizer);
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

    [return: NotNullIfNotNull(nameof(dbAttendee))]
    private static Data.Model.Attendee? ToModel(Data.Db.Attendee? dbAttendee)
    {
        return dbAttendee is null ? null : new()
        {
            Id = Guid.Parse(dbAttendee.Account.Id)
        };
    }

    [return: NotNullIfNotNull(nameof(dbOrganizer))]
    private static Data.Model.Organizer? ToModel(Data.Db.Organizer? dbOrganizer)
    {
        return dbOrganizer is null ? null : new()
        {
            Id = Guid.Parse(dbOrganizer.Account.Id),
            Name = dbOrganizer.Account.Company ?? dbOrganizer.Account.Email!,
            Email = dbOrganizer.Account.Email!
        };
    }
}
