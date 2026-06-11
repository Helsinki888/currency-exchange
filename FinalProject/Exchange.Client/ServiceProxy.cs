using System.ServiceModel;
using Exchange.Contracts;

namespace Exchange.Client;

/// <summary>
/// Creates WCF channels to the exchange office service. Every call runs on a
/// background thread so the WPF UI stays responsive.
/// </summary>
public static class ServiceProxy
{
    public const string ServiceUrl = "http://localhost:8083/ExchangeService.svc";

    private static readonly ChannelFactory<IExchangeService> Factory = new(
        new BasicHttpBinding
        {
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            OpenTimeout = TimeSpan.FromSeconds(10),
            SendTimeout = TimeSpan.FromSeconds(30),
            ReceiveTimeout = TimeSpan.FromSeconds(30)
        },
        new EndpointAddress(ServiceUrl));

    public static Task<T> CallAsync<T>(Func<IExchangeService, T> call)
    {
        return Task.Run(() =>
        {
            var channel = Factory.CreateChannel();
            try
            {
                var result = call(channel);
                ((IClientChannel)channel).Close();
                return result;
            }
            catch
            {
                ((IClientChannel)channel).Abort();
                throw;
            }
        });
    }
}
