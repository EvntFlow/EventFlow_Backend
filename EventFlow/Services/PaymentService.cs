using EventFlow.Data;
using EventFlow.Data.Model;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Services;

public class PaymentService(DbContextOptions<ApplicationDbContext> dbContextOptions)
    : DbService(dbContextOptions)
{
    public async IAsyncEnumerable<PaymentMethod> GetPaymentMethods(Guid userId)
    {
        var userIdString = userId.ToString();

        using var dbContext = DbContext;
        var query = dbContext.PaymentMethods
            .Where(n => n.Account.Id == userIdString)
            .AsAsyncEnumerable();

        await foreach (var paymentMethod in query)
        {
            yield return paymentMethod switch
            {
                Data.Db.CardPaymentMethod cardPaymentMethod => new CardPaymentMethod()
                {
                    Id = paymentMethod.Id,
                    DisplayName = paymentMethod.DisplayName,
                    Number = cardPaymentMethod.Number
                },
                _ => new PaymentMethod()
                {
                    Id = paymentMethod.Id,
                    DisplayName = paymentMethod.DisplayName,
                }
            };
        }
    }

    public async Task<bool> IsValidPaymentMethod(Guid paymentMethodId, Guid userId)
    {
        using var dbContext = DbContext;
        return await dbContext.PaymentMethods
            .Where(p => p.Id == paymentMethodId && p.Account.Id == userId.ToString())
            .AnyAsync();
    }

    public async Task PerformTransaction(Guid fromPaymentMethodId, Guid toPaymentMethodId)
    {
        using var dbContext = DbContext;
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        var fromPaymentMethod = await dbContext.PaymentMethods
            .SingleAsync(pm => pm.Id == fromPaymentMethodId);

        var toPaymentMethod = await dbContext.PaymentMethods
            .SingleAsync(pm => pm.Id == toPaymentMethodId);

        // TODO: Do some transactions?

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CardPaymentMethod> AddCard(Guid userId,
        string? displayName, string number, string expiry, string cvv, string name)
    {
        var userIdString = userId.ToString();

        using var dbContext = DbContext;
        var account = await dbContext.Users.SingleAsync(a => a.Id == userIdString);

        var paymentMethodEntry = await dbContext.AddAsync(new Data.Db.CardPaymentMethod()
        {
            Account = account,
            Type = nameof(CardPaymentMethod),
            DisplayName = displayName,
            Number = number,
            Expiry = expiry,
            Name = name,
            Cvv = cvv
        });

        var paymentMethod = paymentMethodEntry.Entity;

        await dbContext.SaveChangesAsync();

        return new CardPaymentMethod()
        {
            Id = paymentMethod.Id,
            DisplayName = paymentMethod.DisplayName,
            Number = paymentMethod.Number
        };
    }
}
