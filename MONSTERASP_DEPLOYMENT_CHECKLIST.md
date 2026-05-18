# MonsterASP.net Deployment Checklist

## Problem
Website shows "ASP.NET Core application was not detected" error.

## Root Cause
The ASP.NET Core application files may not be properly uploaded or the web.config is not being recognized.

---

## Step-by-Step Deployment

### Step 1: Prepare Files
- [ ] Run: `dotnet publish -c Release`
- [ ] Verify files exist in: `bin/Release/net8.0/publish/`
- [ ] Check that `web.config` is in the publish folder

### Step 2: Connect via FTP
- [ ] Open FileZilla or WinSCP
- [ ] Connect to: `sftp9530.databaseasp.net` (or your FTP server)
- [ ] Login with your FTP credentials
- [ ] Navigate to website root folder

### Step 3: Clean Old Files
- [ ] **IMPORTANT**: Delete ALL files in the website root folder
- [ ] This ensures no old files interfere with the new deployment
- [ ] Keep the folder structure (don't delete the folder itself)

### Step 4: Upload New Files
- [ ] Upload ALL files from `bin/Release/net8.0/publish/`
- [ ] Make sure these files are uploaded:
  - [ ] `web.config` (in root)
  - [ ] `CRLFruitstandESS.dll` (in root)
  - [ ] `CRLFruitstandESS.exe` (in root)
  - [ ] `appsettings.json` (in root)
  - [ ] `appsettings.Production.json` (in root)
  - [ ] `wwwroot/` folder (with all CSS, JS, images)
  - [ ] All `.dll` files (in root)
  - [ ] `runtimes/` folder (with all runtime files)

### Step 5: Verify Upload
- [ ] Check file count matches (should be 50+ files)
- [ ] Verify `web.config` is in root (not in a subfolder)
- [ ] Verify `CRLFruitstandESS.dll` is in root

### Step 6: Restart Website
1. Go to MonsterASP.net control panel
2. Find your website: `crlfruitstand.runassp.net`
3. Click **Stop** button
4. Wait 10 seconds
5. Click **Start** button
6. Wait 30-60 seconds for startup

### Step 7: Test
- [ ] Open browser
- [ ] Go to: `https://crlfruitstand.runassp.net`
- [ ] You should see login page (not 500 error)
- [ ] Try logging in with admin credentials
- [ ] You should see the dashboard

---

## Troubleshooting

### Error: "ASP.NET Core application was not detected"
**Cause**: Files not uploaded or web.config missing

**Fix**:
1. Delete all files from website folder
2. Re-upload ALL files from publish folder
3. Verify `web.config` is in root folder
4. Restart website
5. Wait 60 seconds and try again

### Error: 500 Internal Server Error
**Cause**: Application started but database connection failed

**Fix**:
1. Check MonsterASP.net logs
2. Verify database is online
3. Verify connection string is correct
4. Try restarting website

### Error: "This site can't be reached"
**Cause**: Website not running or DNS issue

**Fix**:
1. Go to MonsterASP.net
2. Click **Start** button
3. Wait 60 seconds
4. Try again

---

## File Structure (What Should Be Uploaded)

```
wwwroot/
├── css/
├── js/
├── images/
└── ...
runtimes/
├── win-x64/
├── linux-x64/
└── ...
web.config
CRLFruitstandESS.dll
CRLFruitstandESS.exe
appsettings.json
appsettings.Production.json
appsettings.example.json
[50+ other DLL files]
```

---

## Connection String Verification

Your connection string should be:
```
Server=db52619.databaseasp.net;Port=3306;Database=db52619;Uid=db52619;Pwd=5Aw+Q=6n7Ry!;Connection Timeout=60;Command Timeout=60;SslMode=None;
```

**Key points**:
- Uses `Uid=` not `User=`
- Uses `Pwd=` not `Password=`
- Includes `SslMode=None`
- Includes timeout settings

---

## Environment Variables

The `web.config` should have:
```xml
<environmentVariables>
  <environmentVariable name="PAYMONGO_SECRET_KEY" value="sk_live_KPjB2wBUEnMqwsLT5ppAZ8NPpk_live_5UE5xU5VjfLeNGtcwAYATTD3" />
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
</environmentVariables>
```

---

## Logging

Stdout logging is enabled in web.config. To view logs:
1. Go to MonsterASP.net control panel
2. Click on your website
3. Look for **Logs** or **Event logs**
4. Check for error messages

---

## Quick Commands

```bash
# Clean and publish
dotnet clean
dotnet publish -c Release

# Verify files
dir bin/Release/net8.0/publish/ | wc -l  # Should show 50+ files
```

---

## Success Indicators

✅ Website responds (not "can't reach")  
✅ Login page loads (not 500 error)  
✅ Can log in with admin credentials  
✅ Dashboard loads without errors  
✅ Database queries work  

---

## Next Steps After Successful Deployment

1. Test all dashboard features
2. Verify reports load correctly
3. Test payment processing
4. Monitor logs for any errors
5. Set up automated backups
