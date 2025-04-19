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
            await paymentService.GetPaymentMethodsAsync(accountId).CountAsync(),
            Is.EqualTo(0)
        );

        // Add card payment
        const string cardDisplayName = "Mr. President's Card";
        Assert.DoesNotThrowAsync(async () =>
        {
            await paymentService.AddCardAsync(
                accountId,
                cardDisplayName, "4242 4242 4242 4242",
                "04/75", "304", "NGUYEN VAN THIEU"
            );
        });
        var payments = await paymentService.GetPaymentMethodsAsync(accountId).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(payments, Has.Count.EqualTo(1));
            Assert.That(payments.Single().DisplayName, Is.EqualTo(cardDisplayName));
        });
    }
}
