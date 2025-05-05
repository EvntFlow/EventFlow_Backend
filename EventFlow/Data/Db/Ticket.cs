using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data.Db;

[Index(nameof(TicketOption))]
[Index(nameof(Attendee))]
public class Ticket
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required TicketOption TicketOption { get; set; }

    [Required]
    public required Attendee Attendee { get; set; }

    [Required]
    public required decimal Price { get; set; }

    [Required]
    public required string HolderFullName { get; set; }

    [Required]
    public required string HolderEmail { get; set; }

    [Required]
    public required string HolderPhoneNumber { get; set; }

    public bool IsReviewed { get; set; } = false;
}
