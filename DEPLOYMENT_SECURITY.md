# 🔐 Deployment Security Checklist

## ⚠️ CRITICAL - Before Deploying to Production

### 1. **Paymongo API Keys**
- [ ] **REVOKE** the exposed key: `sk_live_KPjB2wBUEnMqwsLT5ppAZ8NPpk_live_5UE5xU5VjfLeNGtcwAYATTD3`
  - Go to https://dashboard.paymongo.com
  - Settings → API Keys
  - Delete the compromised key
- [ ] Generate **NEW production keys** from Paymongo
- [ ] Copy the new `pk_live_*` and `sk_live_*` keys
- [ ] **DO NOT** commit these keys to git

### 2. **Email Credentials**
- [ ] Generate a **new Gmail App Password**
  - Go to https://myaccount.google.com/apppasswords
  - Select Mail and Windows Computer
  - Generate new password
- [ ] Update only in deployment environment (never in code)

### 3. **Database Password**
- [ ] Set a **strong database password** (minimum 12 characters)
- [ ] Use special characters, numbers, uppercase, lowercase
- [ ] Store securely in your hosting provider's secrets manager

### 4. **Environment Variables Setup**

#### For Azure App Service:
```bash
az keyvault secret set --vault-name your-vault --name PayMongoPublicKey --value "pk_live_xxxxx"
az keyvault secret set --vault-name your-vault --name PayMongoSecretKey --value "sk_live_xxxxx"
az keyvault secret set --vault-name your-vault --name PayMongoWebhookSecret --value "whsec_xxxxx"
az keyvault secret set --vault-name your-vault --name EmailUser --value "your-email@gmail.com"
az keyvault secret set --vault-name your-vault --name EmailPass --value "your-app-password"
az keyvault secret set --vault-name your-vault --name DbPassword --value "strong-password"
```

#### For AWS (EC2/Elastic Beanstalk):
```bash
export PAYMONGO_PUBLIC_KEY="pk_live_xxxxx"
export PAYMONGO_SECRET_KEY="sk_live_xxxxx"
export PAYMONGO_WEBHOOK_SECRET="whsec_xxxxx"
export EMAIL_USER="your-email@gmail.com"
export EMAIL_PASS="your-app-password"
export DB_PASSWORD="strong-password"
```

#### For Docker/VPS:
Create `.env` file (add to .gitignore):
```
PAYMONGO_PUBLIC_KEY=pk_live_xxxxx
PAYMONGO_SECRET_KEY=sk_live_xxxxx
PAYMONGO_WEBHOOK_SECRET=whsec_xxxxx
EMAIL_USER=your-email@gmail.com
EMAIL_PASS=your-app-password
DB_PASSWORD=strong-password
```

### 5. **Code Configuration**
- [ ] Verify `appsettings.json` uses environment variable placeholders: `${VARIABLE_NAME}`
- [ ] Verify `appsettings.Production.json` exists
- [ ] Verify `.gitignore` includes `appsettings.json` and `appsettings.*.json`

### 6. **Git Security**
- [ ] Run `git log --all --full-history -- appsettings.json` to check for exposed keys
- [ ] If keys were committed, use `git filter-branch` or `BFG Repo-Cleaner` to remove them
- [ ] Force push to remove history: `git push --force-with-lease`

### 7. **HTTPS & Security Headers**
- [ ] Enable HTTPS on your domain
- [ ] Verify `UseHttpsRedirection()` is enabled in production (Program.cs)
- [ ] Verify security headers middleware is active

### 8. **Database**
- [ ] Backup production database before first deployment
- [ ] Verify connection string uses strong password
- [ ] Enable SSL for database connections if possible
- [ ] Restrict database access to application server only

### 9. **Logging & Monitoring**
- [ ] Set logging level to "Warning" or "Error" in production
- [ ] Enable application monitoring (Application Insights, DataDog, etc.)
- [ ] Set up alerts for failed transactions and errors

### 10. **Final Checks**
- [ ] Test payment flow with Paymongo test mode first
- [ ] Verify email notifications work
- [ ] Test user authentication and authorization
- [ ] Check all API endpoints respond correctly
- [ ] Verify database migrations run successfully

## 📋 Deployment Checklist

- [ ] All security items above completed
- [ ] Code reviewed and tested
- [ ] Database backups created
- [ ] Rollback plan documented
- [ ] Team notified of deployment
- [ ] Monitoring alerts configured
- [ ] Post-deployment testing plan ready

## 🚨 If Breach Occurs

1. **Immediately revoke** the compromised key
2. **Generate new keys** from Paymongo
3. **Update** all deployment environments
4. **Monitor** for unauthorized transactions
5. **Contact Paymongo support** if suspicious activity detected
6. **Review git history** for other exposed secrets

## 📚 References

- [Paymongo API Documentation](https://developers.paymongo.com)
- [OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [Azure Key Vault](https://docs.microsoft.com/en-us/azure/key-vault/)
- [AWS Secrets Manager](https://docs.aws.amazon.com/secretsmanager/)
