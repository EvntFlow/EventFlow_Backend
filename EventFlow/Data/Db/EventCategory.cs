using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data.Db;

[Index(nameof(Event) + "Id", nameof(Category) + "Id", IsUnique = true)]
public class EventCategory
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required Event Event { get; set; }

    [Required]
    public required Category Category { get; set; }
}
