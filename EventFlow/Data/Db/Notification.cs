using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data.Db;

[Index($"{nameof(Account)}Id", nameof(Timestamp), AllDescending = true)]
public class Notification
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required DateTime Timestamp { get; set; }

    [Required]
    public required Account Account { get; set; }

    [Required]
    public required string Topic { get; set; }

    [Required]
    public required string Message { get; set; }
}
