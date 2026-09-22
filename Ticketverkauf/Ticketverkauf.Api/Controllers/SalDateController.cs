using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketverkauf.Api.Data;
using Ticketverkauf.Api.Models;

namespace Ticketverkauf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalDateController : ControllerBase
{
    private readonly AppDbContext _db;
    public SalDateController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<SalDate>>> GetAll()
        => await _db.SalDates.AsNoTracking().ToListAsync();
    
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSal(int id, [FromBody] SalUpdateRequest request)
    {
        var entity = await _db.SalDates.FindAsync(id);
        if (entity is null) return NotFound();

        // Tell EF what value we assumed was there when we loaded the row
        _db.Entry(entity).Property(e => e.Sal).OriginalValue = request.OriginalSal;
        entity.Sal = request.Sal;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This row was changed by someone else since you loaded it. Reload and try again." });
        }

        return NoContent();
    }
}
