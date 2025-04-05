using System.ComponentModel.DataAnnotations;

namespace EventFlow.Data.Db;

public class CardPaymentMethod : PaymentMethod
{
    [Required]
    [CreditCard]
    public required string Number { get; set; }

    [Required]
    public required string Expiry { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Cvv { get; set; }
}
