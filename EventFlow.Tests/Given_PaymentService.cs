using EventFlow.Data;
using EventFlow.Services;

namespace EventFlow.Tests;

public class Given_PaymentService : BaseTest
{
    [Test]
    public async Task When_AddCard()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var accountId = Guid.Parse(account.Id);
        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(_dbOptions);

        // No payment methods yet.
        Assert.That(
            await paymentService.GetPaymentMethods(accountId).CountAsync(),
            Is.EqualTo(0)
        );

        // Add card payment
        const string cardDisplayName = "Mr. President's Card";
        Assert.DoesNotThrowAsync(async () =>
        {
            await paymentService.AddCard(
                accountId,
                cardDisplayName, "4242 4242 4242 4242",
                "04/75", "304", "NGUYEN VAN THIEU"
            );
        });
        var payments = await paymentService.GetPaymentMethods(accountId).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(payments, Has.Count.EqualTo(1));
            Assert.That(payments.Single().DisplayName, Is.EqualTo(cardDisplayName));
        });
    }

    [Test]
    public async Task When_CheckValidPaymentMethod()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var account1Id = Guid.Parse(account1.Id);
        var account2 = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var account2Id = Guid.Parse(account2.Id);

        var payment1 = (await dbContext.PaymentMethods.AddAsync(new()
        {
            Account = account1,
            Type = "PaymentMethod",
            DisplayName = "Generic Payment"
        })).Entity;
        var payment2 = (await dbContext.PaymentMethods.AddAsync(new Data.Db.CardPaymentMethod()
        {
            Account = account2,
            Type = nameof(Data.Db.CardPaymentMethod),
            DisplayName = "Mr. President's Card",
            Number = "4242 4242 4242 4242",
            Expiry = "04/75",
            Name = "NGUYEN VAN THIEU",
            Cvv = "304"
        })).Entity;

        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(_dbOptions);

        foreach (var accountId in new Guid[] { account1Id, account2Id })
        {
            var otherAccountId = accountId == account1Id ? account2Id : account1Id;

            Assert.That(
                await paymentService.GetPaymentMethods(accountId).CountAsync(), Is.EqualTo(1)
            );

            foreach (var paymentMethod in
                await paymentService.GetPaymentMethods(accountId).ToListAsync())
            {
                await Assert.MultipleAsync(async () =>
                {
                    Assert.That(
                        await paymentService.IsValidPaymentMethod(paymentMethod.Id, accountId)
                    );
                    Assert.That(
                        await paymentService.IsValidPaymentMethod(paymentMethod.Id, otherAccountId),
                        Is.False
                    );
                    Assert.That(
                        await paymentService.IsValidPaymentMethod(Guid.NewGuid(), accountId),
                        Is.False
                    );
                    Assert.That(
                        await paymentService.IsValidPaymentMethod(Guid.Empty, accountId),
                        Is.False
                    );
                });
            }
        }
    }

    [Test]
    public async Task When_PerformTransaction()
    {
        using var dbContext = new ApplicationDbContext(_dbOptions);

        var account1 = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var account1Id = Guid.Parse(account1.Id);
        var account2 = (await dbContext.Users.AddAsync(new Account{})).Entity;
        var account2Id = Guid.Parse(account2.Id);

        var payment1 = (await dbContext.PaymentMethods.AddAsync(new()
        {
            Account = account1,
            Type = "PaymentMethod",
            DisplayName = "Generic Payment"
        })).Entity;
        var payment2 = (await dbContext.PaymentMethods.AddAsync(new Data.Db.CardPaymentMethod()
        {
            Account = account2,
            Type = nameof(Data.Db.CardPaymentMethod),
            DisplayName = "Mr. President's Card",
            Number = "4242 4242 4242 4242",
            Expiry = "04/75",
            Name = "NGUYEN VAN THIEU",
            Cvv = "304"
        })).Entity;

        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(_dbOptions);
        await paymentService.PerformTransaction(payment1.Id, payment2.Id, 42.0m);
        await paymentService.PerformTransaction(payment2.Id, payment1.Id, 42.0m);

        Assert.Pass();
    }
}
