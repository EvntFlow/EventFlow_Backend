namespace EventFlow.Data.Model;

public class Notification
{
    public required Guid Id { get; set; }

    public required string Topic { get; set; }

    public required string Message { get; set; }
}
