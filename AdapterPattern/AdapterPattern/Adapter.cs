namespace AdapterPattern;

public class Adapter : ITarget
{
    private readonly LegacyPaymentGateway _legacyGateway;

    public Adapter(LegacyPaymentGateway legacyGateway)
    {
        _legacyGateway = legacyGateway;
    }

    public void ProcessPayment(decimal amount)
    {
        // Translate: decimal euro -> string cents, and supply a currency code
        string amountInCents = ((int)(amount * 100)).ToString();
        _legacyGateway.MakeTransaction(amountInCents, "EUR");
    }
}