using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data.Db;

[Index(nameof(Attendee) + "Id", nameof(Event) + "Id", IsUnique = true)]
public class SavedEvent
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required Attendee Attendee { get; set; }

    [Required]
    public required Event Event { get; set; }
}
