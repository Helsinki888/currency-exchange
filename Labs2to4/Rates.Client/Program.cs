using System.ServiceModel;
using Rates.Contracts;

Console.WriteLine("Labs 2-4 — console client for the ExchangeRateService");
Console.WriteLine("Connecting to http://localhost:8082/ExchangeRateService.svc ...");
Console.WriteLine();

var binding = new BasicHttpBinding();
var endpoint = new EndpointAddress("http://localhost:8082/ExchangeRateService.svc");
var factory = new ChannelFactory<IExchangeRateService>(binding, endpoint);

// Allow a one-shot mode for scripted testing: Rates.Client.exe USD
if (args.Length > 0)
{
    var service = factory.CreateChannel();
    var info = service.GetExchangeRate(args[0]);
    Console.WriteLine($"{info.Code} ({info.Currency}): {info.Rate} PLN  [table {info.TableNumber}, {info.EffectiveDate}]");
    ((IClientChannel)service).Close();
    factory.Close();
    return;
}

while (true)
{
    Console.Write("Enter a currency code (e.g. USD, EUR, GBP) or 'q' to quit: ");
    var code = Console.ReadLine()?.Trim() ?? "";
    if (string.Equals(code, "q", StringComparison.OrdinalIgnoreCase))
        break;
    if (code.Length == 0)
        continue;

    var service = factory.CreateChannel();
    try
    {
        var info = service.GetExchangeRate(code);
        Console.WriteLine();
        Console.WriteLine($"  Currency:       {info.Currency} ({info.Code})");
        Console.WriteLine($"  Average rate:   {info.Rate} PLN");
        Console.WriteLine($"  Effective date: {info.EffectiveDate}");
        Console.WriteLine($"  NBP table:      {info.TableNumber}");
        Console.WriteLine();
        ((IClientChannel)service).Close();
    }
    catch (FaultException ex)
    {
        Console.WriteLine("Service error: " + ex.Message);
        ((IClientChannel)service).Abort();
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERROR: could not reach the service. Is Rates.Service running?");
        Console.WriteLine(ex.Message);
        ((IClientChannel)service).Abort();
    }
}

factory.Close();
