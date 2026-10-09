using PlantEncyclopedia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Local development: ASP.NET Core User Secrets. Other environments: ConnectionStrings__PlantEncyclopedia.
var connectionString = builder.Configuration.GetConnectionString("PlantEncyclopedia")
    ?? throw new InvalidOperationException(
        "Connection string 'PlantEncyclopedia' is not configured. " +
        "Set it with User Secrets or the ConnectionStrings__PlantEncyclopedia environment variable.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();
