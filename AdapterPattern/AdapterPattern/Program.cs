using AdapterPattern;

ITarget payment = new Adapter(new LegacyPaymentGateway()); //create new Adapter
payment.ProcessPayment(49.99m); //m so it is a decimal instead of a double