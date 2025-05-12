using System.Text.Json.Serialization;

namespace EventFlow.Data.Model;

public class Notification
{
    public required Guid Id { get; set; }

    public required DateTime Timestamp { get; set; }

    public required string Topic { get; set; }

    public required string Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsRead { get; set; }
}
