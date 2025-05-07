using System.Text.Json.Serialization;

namespace EventFlow.Data.Model;

public class Event
{
    public required Guid Id { get; set; }

    public required Organizer Organizer { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required DateTime StartDate { get; set; }

    public required DateTime EndDate { get; set; }

    public Uri? BannerUri { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? BannerFile { get; set; }

    public required string Location { get; set; }

    public required decimal Price { get; set; }

    public required int Interested { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<Category>? Categories { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<TicketOption>? TicketOptions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SavedEvent? SavedEvent { get; set; }
}
