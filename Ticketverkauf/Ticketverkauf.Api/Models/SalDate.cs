using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ticketverkauf.Api.Models;
[Table("transactions")]
public class SalDate
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public float? Sal { get; set; }
}