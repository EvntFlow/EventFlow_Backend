using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Data.Db;

public class Event
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }

    [Required]
    public required Organizer Organizer { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Description { get; set; }

    [Required]
    public required DateTime StartDate { get; set; }

    [Required]
    public required DateTime EndDate { get; set; }

    public Uri? BannerUri { get; set; }

    public Guid? BannerFile { get; set; }

    [Required]
    public required string Location { get; set; }

    [Required]
    public required decimal Price { get; set; }

    [Required]
    public required int Interested { get; set; }

    [Required]
    public required int Sold { get; set; }
}
