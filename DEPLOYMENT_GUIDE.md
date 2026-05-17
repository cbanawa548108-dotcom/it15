# 🚀 Deployment Guide - CRL Fruitstand ESS

## ✅ Now Ready to Deploy!

Your application is now configured to read all sensitive data from **environment variables** instead of hardcoded values.

## 📋 Environment Variables Required

Set these on your hosting platform:

```
DB_SERVER=your-database-server.com
DB_USER=your-db-username
DB_PASSWORD=your-strong-db-password
PAYMONGO_PUBLIC_KEY=pk_live_xxxxx
PAYMONGO_SECRET_KEY=sk_live_xxxxx
PAYMONGO_WEBHOOK_SECRET=whsec_xxxxx
EMAIL_USER=your-email@gmail.com
EMAIL_PASS=your-gmail-app-password
```

## 🌐 Deployment Platforms

### **Azure App Service**

1. Go to your App Service → Settings → Configuration
2. Add Application Settings:
   - `DB_SERVER` = your database server
   - `DB_USER` = database username
   - `DB_PASSWORD` = database password
   - `PAYMONGO_PUBLIC_KEY` = your Paymongo public key
   - `PAYMONGO_SECRET_KEY` = your Paymongo secret key
   - `PAYMONGO_WEBHOOK_SECRET` = your webhook secret
   - `EMAIL_USER` = your Gmail address
   - `EMAIL_PASS` = your Gmail app password

3. Deploy:
```bash
dotnet publish -c Release
# Upload the publish folder to Azure
```

### **AWS Elastic Beanstalk**

1. Create `.ebextensions/env.config`:
```yaml
option_settings:
  aws:elasticbeanstalk:application:environment:
    DB_SERVER: your-database-server.com
    DB_USER: your-db-username
    DB_PASSWORD: your-strong-db-password
    PAYMONGO_PUBLIC_KEY: pk_live_xxxxx
    PAYMONGO_SECRET_KEY: sk_live_xxxxx
    PAYMONGO_WEBHOOK_SECRET: whsec_xxxxx
    EMAIL_USER: your-email@gmail.com
    EMAIL_PASS: your-gmail-app-password
```

2. Deploy:
```bash
eb init
eb create
eb deploy
```

### **Docker (Any VPS)**

1. Create `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish .
EXPOSE 80
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "CRLFruitstandESS.dll"]
```

2. Create `docker-compose.yml`:
```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "80:80"
    environment:
      - DB_SERVER=mysql-db
      - DB_USER=root
      - DB_PASSWORD=${DB_PASSWORD}
      - PAYMONGO_PUBLIC_KEY=${PAYMONGO_PUBLIC_KEY}
      - PAYMONGO_SECRET_KEY=${PAYMONGO_SECRET_KEY}
      - PAYMONGO_WEBHOOK_SECRET=${PAYMONGO_WEBHOOK_SECRET}
      - EMAIL_USER=${EMAIL_USER}
      - EMAIL_PASS=${EMAIL_PASS}
    depends_on:
      - mysql-db
  
  mysql-db:
    image: mysql:8.0
    environment:
      - MYSQL_ROOT_PASSWORD=${DB_PASSWORD}
      - MYSQL_DATABASE=CRLFruitstandDB
    volumes:
      - mysql-data:/var/lib/mysql

volumes:
  mysql-data:
```

3. Create `.env` file (never commit this):
```
DB_PASSWORD=your-strong-password
PAYMONGO_PUBLIC_KEY=pk_live_xxxxx
PAYMONGO_SECRET_KEY=sk_live_xxxxx
PAYMONGO_WEBHOOK_SECRET=whsec_xxxxx
EMAIL_USER=your-email@gmail.com
EMAIL_PASS=your-gmail-app-password
```

4. Deploy:
```bash
docker-compose up -d
```

### **Traditional VPS (Linux)**

1. Set environment variables in your shell:
```bash
export DB_SERVER="your-database-server.com"
export DB_USER="your-db-username"
export DB_PASSWORD="your-strong-db-password"
export PAYMONGO_PUBLIC_KEY="pk_live_xxxxx"
export PAYMONGO_SECRET_KEY="sk_live_xxxxx"
export PAYMONGO_WEBHOOK_SECRET="whsec_xxxxx"
export EMAIL_USER="your-email@gmail.com"
export EMAIL_PASS="your-gmail-app-password"
export ASPNETCORE_ENVIRONMENT="Production"
```

2. Or add to systemd service file:
```ini
[Service]
Environment="DB_SERVER=your-database-server.com"
Environment="DB_USER=your-db-username"
Environment="DB_PASSWORD=your-strong-db-password"
Environment="PAYMONGO_PUBLIC_KEY=pk_live_xxxxx"
Environment="PAYMONGO_SECRET_KEY=sk_live_xxxxx"
Environment="PAYMONGO_WEBHOOK_SECRET=whsec_xxxxx"
Environment="EMAIL_USER=your-email@gmail.com"
Environment="EMAIL_PASS=your-gmail-app-password"
Environment="ASPNETCORE_ENVIRONMENT=Production"
```

3. Deploy:
```bash
dotnet publish -c Release
cd bin/Release/net8.0/publish
dotnet CRLFruitstandESS.dll
```

## 🔍 Pre-Deployment Checklist

- [ ] All environment variables are set on your hosting platform
- [ ] Database server is accessible and running
- [ ] Database user has proper permissions
- [ ] Paymongo keys are production keys (not test keys)
- [ ] Gmail app password is generated and correct
- [ ] HTTPS is enabled on your domain
- [ ] Database backups are created
- [ ] Monitoring/logging is configured

## 🧪 Testing After Deployment

1. **Test Database Connection**
   - Check application logs for database errors
   - Verify users can log in

2. **Test Paymongo Integration**
   - Create a test transaction
   - Verify payment webhook is received
   - Check transaction is recorded in database

3. **Test Email Notifications**
   - Trigger an email notification
   - Verify it arrives in inbox

4. **Test All Dashboards**
   - Executive Dashboard
   - Analytics
   - Financial Reports
   - Inventory Management

## 🚨 Troubleshooting

### Database Connection Failed
```
Check:
- DB_SERVER is correct
- DB_USER has proper permissions
- DB_PASSWORD is correct
- Database server is running and accessible
- Firewall allows connection
```

### Paymongo Errors
```
Check:
- PAYMONGO_SECRET_KEY is set correctly
- Key is production key (starts with sk_live_)
- Webhook secret is configured
```

### Email Not Sending
```
Check:
- EMAIL_USER is correct Gmail address
- EMAIL_PASS is app password (not regular password)
- Gmail account has 2FA enabled
- SMTP settings are correct (smtp.gmail.com:587)
```

## 📞 Support

If you encounter issues:
1. Check application logs
2. Verify all environment variables are set
3. Test each component individually
4. Contact your hosting provider for infrastructure issues

---

**Last Updated:** May 18, 2026
**Version:** 1.0
