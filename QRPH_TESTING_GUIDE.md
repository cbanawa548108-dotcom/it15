# QRPH Payment Testing Guide

## Quick Start

### Step 1: Verify Configuration
Check that your `appsettings.json` has PayMongo credentials:
```json
{
  "PayMongo": {
    "SecretKey": "sk_test_..." or "sk_live_...",
    "PublicKey": "pk_test_..." or "pk_live_..."
  }
}
```

### Step 2: Start the Application
```bash
dotnet run
```

### Step 3: Access POS
1. Navigate to `http://localhost:5000/Cashier/POS`
2. Log in with Cashier credentials

### Step 4: Test QRPH Payment

#### Test Mode (sk_test_*)
1. Add items to cart
2. Select "QRPH" payment method
3. Click "Scan QR Code" button
4. You'll be redirected to PayMongo test checkout
5. Use test credentials:
   - **Phone**: 09123456789
   - **OTP**: 123456
6. Complete payment
7. You'll be redirected to receipt page

#### Production Mode (sk_live_*)
1. Add items to cart
2. Select "QRPH" payment method
3. Click "Scan QR Code" button
4. PayMongo displays real QR code
5. Customer scans with their phone
6. Customer completes payment in their wallet app
7. Automatic redirect to receipt page

## Troubleshooting

### Issue: "Payment gateway error"
**Check**:
- PayMongo API key is correct
- Network connectivity to PayMongo API
- Database connection is working

**Solution**:
```bash
# Check logs
dotnet run 2>&1 | grep -i "paymongo\|error"
```

### Issue: Stuck on PayMongo checkout page
**Check**:
- Success URL is correct: `http://localhost:5000/Cashier/PaymentSuccess?txnId=...`
- Database can be reached from PayMongo servers
- Firewall allows PayMongo webhooks

**Solution**:
- Verify `appsettings.json` connection string
- Check database logs for connection errors

### Issue: Payment completes but receipt doesn't show
**Check**:
- PaymentTransaction record exists in database
- RawPayMongoResponse contains cart data
- Sale record was created

**Solution**:
```sql
-- Check PaymentTransaction
SELECT * FROM PaymentTransactions ORDER BY CreatedAt DESC LIMIT 1;

-- Check Sale
SELECT * FROM Sales ORDER BY SaleDate DESC LIMIT 1;
```

### Issue: Inventory not deducted
**Check**:
- Sale was marked as "Completed"
- SaleItems were created
- Inventory record exists

**Solution**:
```sql
-- Check inventory
SELECT p.Name, i.Quantity FROM Products p 
JOIN Inventory i ON p.Id = i.ProductId 
WHERE p.Name LIKE '%test%';
```

## Test Scenarios

### Scenario 1: Successful Payment
1. Add 2 items to cart
2. Select QRPH
3. Click "Scan QR Code"
4. Complete payment with test credentials
5. **Expected**: Receipt shows sale with QRPH payment method

### Scenario 2: Payment Cancellation
1. Add items to cart
2. Select QRPH
3. Click "Scan QR Code"
4. Click "Cancel" on PayMongo checkout
5. **Expected**: Redirected back to POS, cart preserved

### Scenario 3: Multiple Payments
1. Complete first QRPH payment
2. Add new items to cart
3. Complete second QRPH payment
4. **Expected**: Two separate sales in database

### Scenario 4: Large Amount
1. Add expensive items (total > ₱10,000)
2. Select QRPH
3. Complete payment
4. **Expected**: Payment processes normally, no amount limits

### Scenario 5: Session Timeout
1. Add items to cart
2. Select QRPH
3. Wait 30+ minutes
4. Complete payment
5. **Expected**: Payment still completes (uses ProcessedBy user ID)

## Database Verification

### Check Payment Transaction
```sql
SELECT 
    Id, 
    Method, 
    Status, 
    Amount, 
    CreatedAt, 
    PaidAt,
    SaleId
FROM PaymentTransactions 
WHERE Method = 'gcash' 
ORDER BY CreatedAt DESC 
LIMIT 10;
```

### Check Sale Details
```sql
SELECT 
    s.Id,
    s.CashierId,
    s.TotalAmount,
    s.Status,
    s.SaleDate,
    COUNT(si.Id) as ItemCount
FROM Sales s
LEFT JOIN SaleItems si ON s.Id = si.SaleId
WHERE s.SaleDate >= DATE_SUB(NOW(), INTERVAL 1 DAY)
GROUP BY s.Id
ORDER BY s.SaleDate DESC;
```

### Check Inventory Changes
```sql
SELECT 
    sm.Id,
    p.Name,
    sm.Type,
    sm.Quantity,
    sm.PreviousStock,
    sm.NewStock,
    sm.MovementDate
FROM StockMovements sm
JOIN Products p ON sm.ProductId = p.Id
WHERE sm.MovementDate >= DATE_SUB(NOW(), INTERVAL 1 DAY)
ORDER BY sm.MovementDate DESC;
```

## Performance Testing

### Load Test
1. Create 100 test items
2. Process 10 QRPH payments simultaneously
3. Monitor:
   - Response time
   - Database connections
   - PayMongo API rate limits

### Stress Test
1. Process 50 QRPH payments in 5 minutes
2. Monitor:
   - Memory usage
   - CPU usage
   - Database locks

## Deployment Checklist

Before deploying to production:

- [ ] PayMongo live API keys configured
- [ ] Database backups enabled
- [ ] Error logging configured
- [ ] HTTPS enabled
- [ ] Firewall allows PayMongo webhooks
- [ ] Success/Cancel URLs point to production domain
- [ ] Test payment processed successfully
- [ ] Receipt prints correctly
- [ ] Inventory deducted correctly
- [ ] Revenue record created
- [ ] Audit logs enabled

## Support Resources

- **PayMongo Docs**: https://developers.paymongo.com
- **PayMongo Test Mode**: https://developers.paymongo.com/docs/testing
- **PayMongo Webhooks**: https://developers.paymongo.com/docs/webhooks
- **Application Logs**: Check `bin/Debug/net8.0/` or `bin/Release/net8.0/`
- **Database Logs**: Check MySQL error log

## Next Steps

After successful testing:
1. Publish application: `dotnet publish -c Release`
2. Deploy to MonsterASP.net
3. Configure production PayMongo keys
4. Monitor payment transactions
5. Set up alerts for failed payments
