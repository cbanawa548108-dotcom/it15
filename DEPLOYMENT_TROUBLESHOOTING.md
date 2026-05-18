# Deployment Troubleshooting Guide

## 500 Error After Login - Diagnosis & Solutions

### Problem
User successfully logs in but receives a 500 Internal Server Error when trying to access the dashboard.

### Root Causes & Fixes

#### 1. **Database Connection String Format** ✅ FIXED
**Issue**: Connection string was using incorrect MySQL format
- **Before**: `uid=db52619;password=...`
- **After**: `User=db52619;Password=...`

**Files Updated**:
- `appsettings.json`
- `appsettings.Production.json`

**Why This Matters**: The old format (`uid=`) is not recognized by the MySQL connector. The correct format uses `User=` and `Password=`.

---

#### 2. **Database Connectivity Issues**
**Symptoms**:
- Connection timeout errors
- "Unable to connect to database" messages
- Dashboard fails to load after login

**Troubleshooting Steps**:

1. **Check if database server is reachable**:
   ```bash
   ping db52619.databaseasp.net
   ```

2. **Verify credentials**:
   - Server: `db52619.databaseasp.net`
   - Port: `3306`
   - Database: `db52619`
   - User: `db52619`
   - Password: `5Aw+Q=6n7Ry!`

3. **Test connection locally** (if possible):
   ```bash
   mysql -h db52619.databaseasp.net -u db52619 -p5Aw+Q=6n7Ry! db52619
   ```

4. **Check hosting firewall rules**:
   - Ensure port 3306 is open for outbound connections
   - Contact hosting provider if blocked

---

#### 3. **Environment Variables Not Set** (Production Only)
**Issue**: If using environment variables, they must be configured on the hosting platform.

**Required Environment Variables**:
```
PAYMONGO_SECRET_KEY=sk_live_KPjB2wBUEnMqwsLT5ppAZ8NPpk_live_5UE5xU5VjfLeNGtcwAYATTD3
```

**How to Set** (varies by platform):
- **Azure App Service**: Configuration → Application settings
- **AWS Elastic Beanstalk**: Environment properties
- **Heroku**: Config Vars
- **IIS**: Environment variables in Application Pool

---

#### 4. **PayMongo Service Initialization Failure**
**Issue**: PayMongo HTTP client initialization fails if SecretKey is empty or invalid.

**Fix**: 
- SecretKey is now empty in config files (uses environment variable)
- Program.cs reads from `PAYMONGO_SECRET_KEY` environment variable first
- Falls back to config value if environment variable not set

**Verify**:
```csharp
// In Program.cs - PayMongo configuration
var secretKey = Environment.GetEnvironmentVariable("PAYMONGO_SECRET_KEY")
    ?? sp.GetRequiredService<IConfiguration>()["PayMongo:SecretKey"]
    ?? "";
```

---

#### 5. **Database Seeding Errors**
**Issue**: Database seeding failures during startup could prevent app from running.

**Fix**: Error handling in Program.cs now logs errors but allows app to continue:
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred during database seeding.");
    logger.LogWarning("⚠️  Database seeding failed. Check your connection string.");
}
```

---

### How to Check Server Logs

#### On Azure App Service:
1. Go to App Service → Log stream
2. Look for error messages after login attempt
3. Check for database connection errors

#### On IIS:
1. Event Viewer → Windows Logs → Application
2. Look for .NET Runtime errors
3. Check IIS logs in `%SystemRoot%\System32\LogFiles\HTTPERR`

#### On Linux/Docker:
```bash
docker logs <container-id>
# or
journalctl -u <service-name> -f
```

---

### Deployment Checklist

- [ ] Connection string format is correct (using `User=` not `uid=`)
- [ ] Database server is reachable from production environment
- [ ] Database credentials are correct
- [ ] Environment variables are set on hosting platform
- [ ] PayMongo API key is set (if using payments)
- [ ] Email SMTP credentials are configured (if using email)
- [ ] Application logs are accessible for debugging
- [ ] Database migrations have run successfully
- [ ] Admin user has been created in database

---

### Quick Fix Steps

1. **Update connection string** (already done):
   ```
   Server=db52619.databaseasp.net;Port=3306;Database=db52619;User=db52619;Password=5Aw+Q=6n7Ry!;Connection Timeout=60;Command Timeout=60;
   ```

2. **Redeploy application** with updated config files

3. **Check server logs** for specific error messages

4. **Verify database connectivity** from production server

5. **Set environment variables** on hosting platform if needed

---

### Still Getting 500 Error?

**Next Steps**:
1. Enable detailed error logging in `appsettings.Production.json`
2. Check application event logs on server
3. Verify all services are running (database, email, payment gateway)
4. Check for null reference exceptions in dashboard controllers
5. Verify user roles and permissions are set correctly

**Contact Hosting Provider If**:
- Database server is unreachable
- Port 3306 is blocked
- Firewall rules prevent outbound connections
- Server resources are exhausted
