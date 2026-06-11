using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Rates.Contracts;
using Rates.Service;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8082");

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

builder.Services.AddSingleton<NbpApiClient>();
builder.Services.AddTransient<ExchangeRateService>();

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<ExchangeRateService>();
    serviceBuilder.AddServiceEndpoint<ExchangeRateService, IExchangeRateService>(
        new BasicHttpBinding(), "/ExchangeRateService.svc");

    var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadata.HttpGetEnabled = true;
});

Console.WriteLine("Labs 2-4 — ExchangeRateService is running.");
Console.WriteLine("Endpoint: http://localhost:8082/ExchangeRateService.svc");
Console.WriteLine("WSDL:     http://localhost:8082/ExchangeRateService.svc?wsdl");

app.Run();
