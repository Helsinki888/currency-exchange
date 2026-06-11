using System.ServiceModel;
using Hello.Contracts;

Console.WriteLine("Lab 1 — console client for the HelloService WCF service");
Console.WriteLine("Connecting to http://localhost:8081/HelloService.svc ...");
Console.WriteLine();

var binding = new BasicHttpBinding();
var endpoint = new EndpointAddress("http://localhost:8081/HelloService.svc");
var factory = new ChannelFactory<IHelloService>(binding, endpoint);
var service = factory.CreateChannel();

try
{
    Console.Write("Enter your name: ");
    var name = Console.ReadLine() ?? "";

    Console.WriteLine();
    Console.WriteLine("Service response: " + service.SayHello(name));
    Console.WriteLine("Server time:      " + service.GetServerTime());

    ((IClientChannel)service).Close();
    factory.Close();
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: could not reach the service. Is Hello.Service running?");
    Console.WriteLine(ex.Message);
    factory.Abort();
}
