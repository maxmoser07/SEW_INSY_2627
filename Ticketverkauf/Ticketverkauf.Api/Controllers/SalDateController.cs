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
    public async Task<IActionResult> UpdateSal(int id, [FromBody] float? sal)
    {
        var entity = await _db.SalDates.FindAsync(id);
        if (entity is null) return NotFound();

        entity.Sal = sal;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}