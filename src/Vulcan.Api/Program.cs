using Microsoft.EntityFrameworkCore;
using Vulcan.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDatabase();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    // ensure the database is migrated
    var database = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
    database.Database.Migrate();
}

var app = ApiHost.Create(args);

app.Run();
