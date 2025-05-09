namespace EventFlow.Data.Model;

public class Statistics
{
    public required int TotalEvents { get; set; }

    public required int TotalTickets { get; set; }

    public required decimal TotalSales { get; set; }

    public required int TotalReviewed { get; set; }

    public IList<decimal> DailySales { get; set; } = [];
}
