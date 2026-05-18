# FTP Deployment Guide for MonsterASP.net

## Quick Summary
You need to upload the published files from your computer to MonsterASP.net using FTP.

---

## Step 1: Get Your FTP Credentials

1. Go to your MonsterASP.net control panel
2. Find your website: `crlfruitstand.runassp.net`
3. Look for **FTP/SFTP Access** section
4. You should see:
   - **FTP Server**: `sftp9530.databaseasp.net` (or similar)
   - **FTP Username**: Your username
   - **FTP Password**: Your password
   - **Port**: 21 (for FTP) or 22 (for SFTP)

---

## Step 2: Download an FTP Client

Choose one (all are free):
- **FileZilla** (recommended): https://filezilla-project.org/
- **WinSCP**: https://winscp.net/
- **Windows built-in**: Use File Explorer (not recommended for large uploads)

### Using FileZilla (Easiest):
1. Download and install FileZilla
2. Open FileZilla
3. Go to **File → Site Manager**
4. Click **New Site**
5. Fill in:
   - **Host**: `sftp9530.databaseasp.net`
   - **Port**: `22` (for SFTP, more secure)
   - **Protocol**: SFTP
   - **User**: Your FTP username
   - **Password**: Your FTP password
6. Click **Connect**

---

## Step 3: Navigate to Your Website Folder

Once connected via FTP:
1. Look for a folder like `wwwroot`, `public_html`, or `web`
2. This is your website root folder
3. **Delete all existing files** in this folder (if any)

---

## Step 4: Upload the Published Files

1. On your computer, open: `C:\Users\user\CRLFruitstandESS\bin\Release\net8.0\publish\`
2. Select **ALL files and folders** in this directory
3. Drag and drop them into the FTP window (or right-click → Upload)
4. Wait for the upload to complete (this may take 2-5 minutes)

**Important files to verify are uploaded:**
- `web.config` ✅
- `CRLFruitstandESS.dll` ✅
- `appsettings.json` ✅
- `appsettings.Production.json` ✅
- `wwwroot/` folder ✅

---

## Step 5: Restart Your Website

1. Go back to MonsterASP.net control panel
2. Find your website: `crlfruitstand.runassp.net`
3. Click the **Restart** button
4. Wait 30 seconds for the app to restart

---

## Step 6: Test Your Application

1. Open your browser
2. Go to: `https://crlfruitstand.runassp.net`
3. You should see the login page
4. Log in with your admin credentials
5. You should see the CFO Dashboard (not a 500 error)

---

## Troubleshooting

### "This site can't be reached"
- Website may still be starting up (wait 1-2 minutes)
- Try clicking **Restart** again in MonsterASP.net
- Check if all files were uploaded correctly

### Still Getting 500 Error
- Check MonsterASP.net logs (if available)
- Verify `web.config` was uploaded
- Make sure database is online in MonsterASP.net

### FTP Connection Failed
- Double-check your FTP credentials
- Try using port 21 instead of 22
- Contact MonsterASP.net support

---

## What Was Deployed

✅ Fixed MySQL connection string format  
✅ Added environment variables in web.config  
✅ PayMongo API key configured  
✅ All dependencies included  
✅ Database migrations ready  

---

## Next Steps After Successful Deployment

1. Test all dashboard features
2. Verify database connectivity
3. Check that reports load correctly
4. Test payment processing (if applicable)
5. Monitor application logs for any errors

---

## Need Help?

If you get stuck:
1. Take a screenshot of the error
2. Check MonsterASP.net logs
3. Verify all files were uploaded
4. Try restarting the website again
