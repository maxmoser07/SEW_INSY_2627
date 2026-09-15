using System.ComponentModel.DataAnnotations;

namespace Ticketverkauf.Api.Models;

public class SalDate
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public float? Sal { get; set; }
}