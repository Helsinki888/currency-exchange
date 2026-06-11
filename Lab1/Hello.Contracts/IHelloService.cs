using System.ServiceModel;

namespace Hello.Contracts;

/// <summary>
/// Lab 1 — a simple WCF service contract shared by the service host and the console client.
/// </summary>
[ServiceContract]
public interface IHelloService
{
    [OperationContract]
    string SayHello(string name);

    [OperationContract]
    string GetServerTime();
}
