using System.Text.Json.Serialization;

namespace EventFlow.Data.Model;

public class SavedEvent
{
    public Guid Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Event? Event { get; set; }
}
