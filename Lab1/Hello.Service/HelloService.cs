using Hello.Contracts;

namespace Hello.Service;

public class HelloService : IHelloService
{
    public string SayHello(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = "stranger";

        return $"Hello, {name}! Greetings from the WCF service.";
    }

    public string GetServerTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
