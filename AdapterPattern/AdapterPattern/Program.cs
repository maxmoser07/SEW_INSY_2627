using AdapterPattern;

ITarget payment = new Adapter(new LegacyPaymentGateway());
payment.ProcessPayment(49.99m); //m so it is a decimal instead of a double