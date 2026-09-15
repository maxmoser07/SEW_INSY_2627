using System.ComponentModel.DataAnnotations.Schema;

namespace Ticketverkauf.Api.Models;

public class SalDate
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public float? Sal { get; set; }
}