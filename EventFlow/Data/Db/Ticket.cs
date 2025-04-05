using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Data.Db;

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
}
