using EventFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public abstract class DbService(DbContextOptions<ApplicationDbContext> dbContextOptions)
{
    protected ApplicationDbContext DbContext => new(dbContextOptions);
}
