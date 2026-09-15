using Microsoft.EntityFrameworkCore;
using Ticketverkauf.Api.Models;

namespace Ticketverkauf.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<SalDate> SalDates => Set<SalDate>();
}