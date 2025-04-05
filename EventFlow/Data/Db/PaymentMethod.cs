using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Data.Db;

public class PaymentMethod
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required Account Account { get; set; }

    [Required]
    public required string Type { get; set; }

    public string? DisplayName { get; set; }
}
