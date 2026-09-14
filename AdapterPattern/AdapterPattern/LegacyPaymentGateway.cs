namespace AdapterPattern;

public class LegacyPaymentGateway
{
    public void MakeTransaction(string amountInCents, string currencyCode)
    {
        Console.WriteLine($"Legacy gateway processed {amountInCents} cents in {currencyCode}");
    }
}