using System.Text.Json.Serialization;

namespace EventFlow.Data.Model;

[JsonDerivedType(typeof(CardPaymentMethod), nameof(CardPaymentMethod))]
public class PaymentMethod
{
    public required Guid Id { get; set; }

    public string? DisplayName { get; set; }
}
