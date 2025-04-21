namespace EventFlow.Data.Model;

public class TicketOption
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required decimal AdditionalPrice { get; set; }

    public required int AmountAvailable { get; set; }
}
