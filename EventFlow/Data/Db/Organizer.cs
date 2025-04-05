using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventFlow.Data.Db;

public class Organizer
{
    [Key]
    [ForeignKey(nameof(Account.Id))]
    public required Account Account { get; set; }
}
