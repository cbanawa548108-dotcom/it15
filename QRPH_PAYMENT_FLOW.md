# QRPH Payment Flow Documentation

## Overview
QRPH (QR Philippine) is a digital payment method integrated with PayMongo that allows customers to scan a QR code and complete payments using their mobile wallets (GCash, Maya, etc.).

## How It Works

### User Flow (Cashier Perspective)
1. **POS Screen**: Cashier selects items and clicks "QRPH" payment method
2. **Checkout**: Cashier clicks "Scan QR Code" button
3. **PayMongo Redirect**: Customer is redirected to PayMongo checkout page
4. **QR Display**: PayMongo displays a QR code on the checkout page
5. **Mobile Scan**: Customer scans QR with their phone
6. **Payment**: Customer completes payment in their mobile wallet app
7. **Auto-Redirect**: After payment, customer is automatically redirected to Payment Success page
8. **Receipt**: Sale is completed and receipt is displayed

### Technical Flow

```
POS.cshtml (selectMethod: 'qrph')
    ↓
processCheckout() → processDigitalPayment()
    ↓
POST /Cashier/CreateDigitalPayment
    ├─ Validates amount
    ├─ Creates PaymentTransaction record (status: pending)
    ├─ Calls PayMongoService.CreateCheckoutSessionAsync()
    │  └─ Creates PayMongo Checkout Session with payment_method_types: ["gcash"]
    ├─ Returns checkout URL to frontend
    └─ Stores checkout URL in database
    ↓
Frontend redirects to PayMongo checkout URL
    ↓
PayMongo Checkout Page
    ├─ Displays QR code
    ├─ Customer scans with phone
    ├─ Customer completes payment in wallet app
    └─ PayMongo redirects to success_url
    ↓
GET /Cashier/PaymentSuccess?txnId={id}&method=gcash
    ├─ Loads pending transaction from database
    ├─ Retrieves cart data from RawPayMongoResponse
    ├─ Creates Sale record
    ├─ Creates SaleItems and deducts inventory
    ├─ Creates Revenue record
    ├─ Marks transaction as paid
    └─ Redirects to Receipt page
    ↓
Receipt.cshtml - Transaction Complete
```

## Implementation Details

### Payment Method Mapping
- **QRPH** → Maps to `gcash` in PayMongo backend
- **GCash** → Maps to `gcash` in PayMongo backend
- **Maya** → Maps to `paymaya` in PayMongo backend

### Key Files
- **POS.cshtml**: Payment method selection UI
- **CashierController.cs**: 
  - `CreateDigitalPayment()`: Creates PayMongo checkout session
  - `PaymentSuccess()`: Processes successful payment
  - `PaymentFailed()`: Handles payment cancellation
- **PayMongoService.cs**: PayMongo API integration

### Database Records
1. **PaymentTransaction**: Stores payment attempt details
   - `Method`: Payment method (gcash, paymaya)
   - `Status`: pending → paid/failed
   - `CheckoutUrl`: PayMongo checkout URL
   - `RawPayMongoResponse`: Cart data (JSON)
   - `SaleId`: Linked sale after payment

2. **Sale**: Completed transaction
   - `CashierId`: Cashier who processed
   - `TotalAmount`: Sale total
   - `Status`: Completed/Voided

3. **SaleItems**: Individual items in sale
4. **Inventory**: Stock deducted after payment
5. **Revenue**: Financial record for CFO module

## Testing QRPH Payment

### Test Mode Credentials
When using PayMongo test keys (sk_test_*):
- **Test GCash Number**: 09123456789
- **Test OTP**: 123456
- **Test Card**: 4343 4343 4343 4345
- **Exp**: 12/25
- **CVV**: 123
- **Card OTP**: 123456

### Test Flow
1. Go to POS page
2. Add items to cart
3. Select "QRPH" payment method
4. Click "Scan QR Code"
5. You'll be redirected to PayMongo test checkout
6. Use test credentials above to complete payment
7. You'll be redirected back to receipt page

### Production Mode
When using PayMongo live keys (sk_live_*):
- Real QR codes are generated
- Real payments are processed
- Customers use their actual mobile wallets

## Error Handling

### Common Issues

**Issue**: "Payment gateway error: qr_ph is invalid payment_method"
- **Cause**: Trying to use "qr_ph" directly with PayMongo
- **Solution**: QRPH maps to "gcash" internally

**Issue**: Payment redirects but doesn't complete
- **Cause**: PayMongo checkout URL is invalid or expired
- **Solution**: Check PayMongo API key and network connectivity

**Issue**: Cart data is lost after payment
- **Cause**: RawPayMongoResponse not properly stored
- **Solution**: Verify database connection and transaction handling

## Security Considerations

1. **API Keys**: Store PayMongo keys in environment variables, not in code
2. **Webhook Verification**: Verify PayMongo webhook signatures
3. **CSRF Protection**: All payment endpoints use [ValidateAntiForgeryToken]
4. **Session Handling**: Payment can complete even if session expires (uses ProcessedBy user ID)
5. **Double-Processing**: Prevents duplicate sales if PayMongo calls success URL twice

## Future Enhancements

1. **Webhook Integration**: Listen for PayMongo payment.paid events
2. **Real-time Status**: Show payment status in real-time
3. **Retry Logic**: Automatic retry for failed payments
4. **Analytics**: Track QRPH vs GCash vs Maya usage
5. **Refunds**: Support partial/full refunds via PayMongo API

## Support

For issues or questions:
1. Check PayMongo API documentation: https://developers.paymongo.com
2. Review error logs in application
3. Verify database connection and PaymentTransaction records
4. Test with PayMongo test mode first before going live
