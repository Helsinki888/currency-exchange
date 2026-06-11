using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Hello.Contracts;
using Hello.Service;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8081");

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<HelloService>();
    serviceBuilder.AddServiceEndpoint<HelloService, IHelloService>(
        new BasicHttpBinding(), "/HelloService.svc");

    var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    metadata.HttpGetEnabled = true;
});

Console.WriteLine("Lab 1 — HelloService is running.");
Console.WriteLine("Endpoint: http://localhost:8081/HelloService.svc");
Console.WriteLine("WSDL:     http://localhost:8081/HelloService.svc?wsdl");

app.Run();
