using Microsoft.EntityFrameworkCore;
using TaskUser.Api;
using TaskUser.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Controllers + Swagger
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core InMemory
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseInMemoryDatabase("demo"));

builder.Services.AddScoped<IRandomizer, DefaultRandomizer>();
builder.Services.AddScoped<ITaskReassigner, TaskReassignerService>();
builder.Services.AddHostedService<PollingBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
