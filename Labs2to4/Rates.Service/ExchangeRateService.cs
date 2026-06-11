using CoreWCF;
using Rates.Contracts;

namespace Rates.Service;

public class ExchangeRateService : IExchangeRateService
{
    private readonly NbpApiClient _nbp;

    public ExchangeRateService(NbpApiClient nbp)
    {
        _nbp = nbp;
    }

    public ExchangeRateInfo GetExchangeRate(string currencyCode)
    {
        try
        {
            return _nbp.GetCurrentRate(currencyCode);
        }
        catch (ArgumentException ex)
        {
            // Surface a clean SOAP fault to the client instead of a server error.
            throw new FaultException(ex.Message);
        }
        catch (Exception)
        {
            throw new FaultException("The NBP API is currently unavailable. Please try again later.");
        }
    }
}
