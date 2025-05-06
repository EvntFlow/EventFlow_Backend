namespace EventFlow.Data.Model;

public class Ticket
{
    public required Guid Id { get; set; }

    public required DateTime Timestamp { get; set; }

    public required Attendee Attendee { get; set; }

    public required Event? Event { get; set; }

    public required TicketOption TicketOption { get; set; }

    public required decimal Price { get; set; }

    public required bool IsReviewed { get; set; }

    public required string HolderFullName { get; set; }

    public required string HolderEmail { get; set; }

    public required string HolderPhoneNumber { get; set; }
}
