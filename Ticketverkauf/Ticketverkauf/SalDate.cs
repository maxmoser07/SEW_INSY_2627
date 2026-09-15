using System.ComponentModel.DataAnnotations;

namespace Ticketverkauf;

public class SalDate
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [ConcurrencyCheck]
    public float? Sal { get; set; }
}