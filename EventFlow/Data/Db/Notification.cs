using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Data.Db;

public class Notification
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required Account Account { get; set; }

    [Required]
    public required string Topic { get; set; }

    [Required]
    public required string Message { get; set; }
}
