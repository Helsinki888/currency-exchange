using System.Globalization;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Exchange.Contracts;

namespace Exchange.Client;

public partial class MainWindow : Window
{
    private string _username = "";
    private string _password = "";

    public MainWindow()
    {
        InitializeComponent();
        HistStartDate.SelectedDate = DateTime.Today.AddDays(-30);
        HistEndDate.SelectedDate = DateTime.Today;
    }

    // ---------- Login / register ----------

    private async void LoginButton_Click(object sender, RoutedEventArgs e) => await SignInAsync();

    private async void LoginPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await SignInAsync();
    }

    private async Task SignInAsync()
    {
        var username = LoginUsername.Text.Trim();
        var password = LoginPassword.Password;
        LoginStatus.Text = "Signing in...";

        try
        {
            var result = await ServiceProxy.CallAsync(s => s.Login(username, password));
            if (!result.Success)
            {
                LoginStatus.Text = result.Message;
                return;
            }

            _username = username;
            _password = password;
            HeaderUser.Text = $"Currency Exchange Office — signed in as {_username}";
            LoginStatus.Text = "";
            LoginPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;

            await LoadRatesAsync();
            await LoadBalancesAsync();
            await LoadTransactionsAsync();
        }
        catch (Exception ex)
        {
            LoginStatus.Text = ConnectionError(ex);
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var username = LoginUsername.Text.Trim();
        var password = LoginPassword.Password;
        LoginStatus.Text = "Creating account...";

        try
        {
            var result = await ServiceProxy.CallAsync(s => s.RegisterUser(username, password));
            LoginStatus.Text = result.Message + (result.Success ? " You can sign in now." : "");
        }
        catch (Exception ex)
        {
            LoginStatus.Text = ConnectionError(ex);
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _username = "";
        _password = "";
        LoginPassword.Password = "";
        MainPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
    }

    // ---------- Exchange rates ----------

    private async void RefreshRates_Click(object sender, RoutedEventArgs e) => await LoadRatesAsync();

    private async Task LoadRatesAsync()
    {
        try
        {
            StatusText.Text = "Loading current NBP rates...";
            var rates = await ServiceProxy.CallAsync(s => s.GetAllCurrentRates());
            AllRatesGrid.ItemsSource = rates;

            var codes = rates.Select(r => r.Code).ToList();
            FillCombo(RateCurrencyCombo, codes);
            FillCombo(HistCurrencyCombo, codes);
            FillCombo(ExchangeCurrencyCombo, codes);

            StatusText.Text = $"Loaded {rates.Count} rates (NBP table A, {rates.FirstOrDefault()?.EffectiveDate:yyyy-MM-dd}).";
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    private static void FillCombo(ComboBox combo, List<string> codes)
    {
        var selected = combo.SelectedItem as string;
        combo.ItemsSource = codes;
        combo.SelectedItem = selected is not null && codes.Contains(selected)
            ? selected
            : (codes.Contains("USD") ? "USD" : codes.FirstOrDefault());
    }

    private async void CheckOfficeRate_Click(object sender, RoutedEventArgs e)
    {
        if (RateCurrencyCombo.SelectedItem is not string code)
            return;

        try
        {
            StatusText.Text = $"Checking office rate for {code}...";
            var rate = await ServiceProxy.CallAsync(s => s.GetOfficeRate(code));
            OfficeRateText.Text =
                $"{rate.Currency} ({rate.Code}), NBP mid rate {rate.NbpMid:N4} PLN ({rate.EffectiveDate:yyyy-MM-dd}).  " +
                $"We sell at {rate.BuyPrice:N4} PLN, we buy at {rate.SellPrice:N4} PLN.";
            StatusText.Text = "Ready.";
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    // ---------- Historical rates ----------

    private async void LoadHistory_Click(object sender, RoutedEventArgs e)
    {
        if (HistCurrencyCombo.SelectedItem is not string code)
            return;
        if (HistStartDate.SelectedDate is not DateTime start || HistEndDate.SelectedDate is not DateTime end)
        {
            StatusText.Text = "Select both start and end dates.";
            return;
        }

        try
        {
            StatusText.Text = $"Loading historical rates for {code}...";
            var rates = await ServiceProxy.CallAsync(s => s.GetHistoricalRates(code, start, end));
            HistoryGrid.ItemsSource = rates;
            StatusText.Text = $"Loaded {rates.Count} historical quotations for {code}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    // ---------- Account ----------

    private async void RefreshBalances_Click(object sender, RoutedEventArgs e) => await LoadBalancesAsync();

    private async Task LoadBalancesAsync()
    {
        try
        {
            var balances = await ServiceProxy.CallAsync(s => s.GetBalances(_username, _password));
            BalancesGrid.ItemsSource = balances;
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    private async void Deposit_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAmount(DepositAmount.Text, out var amount))
        {
            StatusText.Text = "Enter a valid deposit amount, e.g. 1000 or 250.50.";
            return;
        }

        try
        {
            var result = await ServiceProxy.CallAsync(s => s.DepositPln(_username, _password, amount));
            StatusText.Text = result.Message;
            if (result.Success)
            {
                DepositAmount.Text = "";
                await LoadBalancesAsync();
                await LoadTransactionsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    // ---------- Buy / sell ----------

    private async void ExchangeCurrencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExchangeCurrencyCombo.SelectedItem is not string code || MainPanel.Visibility != Visibility.Visible)
            return;

        try
        {
            var rate = await ServiceProxy.CallAsync(s => s.GetOfficeRate(code));
            ExchangeRateInfo.Text =
                $"{rate.Currency} ({rate.Code}):  buy from us at {rate.BuyPrice:N4} PLN,  " +
                $"sell to us at {rate.SellPrice:N4} PLN  (NBP mid {rate.NbpMid:N4}).";
        }
        catch (Exception ex)
        {
            ExchangeRateInfo.Text = Error(ex);
        }
    }

    private async void Buy_Click(object sender, RoutedEventArgs e) => await ExchangeAsync(buy: true);

    private async void Sell_Click(object sender, RoutedEventArgs e) => await ExchangeAsync(buy: false);

    private async Task ExchangeAsync(bool buy)
    {
        if (ExchangeCurrencyCombo.SelectedItem is not string code)
            return;
        if (!TryParseAmount(ExchangeAmount.Text, out var amount))
        {
            ExchangeResult.Text = "Enter a valid amount, e.g. 100 or 49.99.";
            return;
        }

        BuyButton.IsEnabled = SellButton.IsEnabled = false;
        try
        {
            var result = buy
                ? await ServiceProxy.CallAsync(s => s.BuyCurrency(_username, _password, code, amount))
                : await ServiceProxy.CallAsync(s => s.SellCurrency(_username, _password, code, amount));

            ExchangeResult.Text = result.Message;
            if (result.Success)
            {
                await LoadBalancesAsync();
                await LoadTransactionsAsync();
            }
        }
        catch (Exception ex)
        {
            ExchangeResult.Text = Error(ex);
        }
        finally
        {
            BuyButton.IsEnabled = SellButton.IsEnabled = true;
        }
    }

    // ---------- Transactions ----------

    private async void RefreshTransactions_Click(object sender, RoutedEventArgs e) => await LoadTransactionsAsync();

    private async Task LoadTransactionsAsync()
    {
        try
        {
            var transactions = await ServiceProxy.CallAsync(s => s.GetTransactionHistory(_username, _password));
            TransactionsGrid.ItemsSource = transactions;
        }
        catch (Exception ex)
        {
            StatusText.Text = Error(ex);
        }
    }

    // ---------- Helpers ----------

    private static bool TryParseAmount(string text, out decimal amount)
    {
        text = (text ?? "").Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
               && amount > 0;
    }

    private static string Error(Exception ex) => ex switch
    {
        FaultException fault => "Service error: " + fault.Message,
        _ => ConnectionError(ex)
    };

    private static string ConnectionError(Exception ex) =>
        ex is EndpointNotFoundException or CommunicationException or TimeoutException
            ? $"Cannot reach the service at {ServiceProxy.ServiceUrl}. Make sure Exchange.Service is running."
            : "Unexpected error: " + ex.Message;
}
