using Microsoft.EntityFrameworkCore;
using Ticketverkauf.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
Console.WriteLine("### RUNNING FRESH BUILD - CANARY 1 ###");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWasm", policy =>
        policy.WithOrigins("http://localhost:5096") // <-- scheiß https hat mich gefickt
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("AllowWasm");
app.UseAuthorization();
app.MapControllers();
app.Run();