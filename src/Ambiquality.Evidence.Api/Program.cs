var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Ambiquality Evidence API");

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
