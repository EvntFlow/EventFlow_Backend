using EventFlow.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Tests;

public class BaseTest
{
    protected DbContextOptions<ApplicationDbContext> _dbOptions = null!;

    [SetUp]
    public void DbOptionsSetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new ApplicationDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();
    }
}
