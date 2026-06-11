using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Exchange.Contracts;
using Exchange.Service;
using Exchange.Service.Data;
using Exchange.Service.Nbp;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8083");

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

// SQLite database stored next to the service executable.
var dbPath = Path.Combine(AppContext.BaseDirectory, "exchange.db");
builder.Services.AddDbContextFactory<ExchangeDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<NbpApiClient>();
builder.Services.AddTransient<ExchangeService>();

var app = builder.Build();

// Create the database schema on first run.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ExchangeDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<ExchangeService>();
    serviceBuilder.AddServiceEndpoint<ExchangeService, IExchangeService>(
        new BasicHttpBinding(), "/ExchangeService.svc");

    var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadata.HttpGetEnabled = true;
});

Console.WriteLine("Final project — Currency Exchange Office service is running.");
Console.WriteLine("Endpoint: http://localhost:8083/ExchangeService.svc");
Console.WriteLine("WSDL:     http://localhost:8083/ExchangeService.svc?wsdl");
Console.WriteLine($"Database: {dbPath}");

app.Run();
