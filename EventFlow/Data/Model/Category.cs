namespace EventFlow.Data.Model;

public class Category
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public Uri? ImageUri { get; set; }
}
