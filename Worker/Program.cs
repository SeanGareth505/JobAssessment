using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHostedService<Worker.Worker>();

var host = builder.Build();
host.Run();
