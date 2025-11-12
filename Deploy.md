# 🚀 Deployment Guide - COTA Real-Time Analytics

Complete deployment instructions for Azure, Docker, and local development.

---

## 📋 Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Account](https://azure.microsoft.com/) (for cloud deployment)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (optional, for containerization)
- [Git](https://git-scm.com/)

---

## 🏠 Local Development

### Step 1: Clone Repository

```bash
git clone https://github.com/LasaKaru/HackTheTrackAnalytics.git
cd HackTheTrackAnalytics
```

### Step 2: Restore Dependencies

```bash
dotnet restore
```

### Step 3: Run Application

```bash
dotnet run
```

The application will start at:
- **HTTPS:** `https://localhost:7xxx`
- **HTTP:** `http://localhost:5xxx`

(Port numbers will be displayed in the console)

### Step 4: Upload Data

1. Navigate to the home page
2. Drag & drop your COTA telemetry files:
   - `R1_cota_telemetry_data.csv` (2GB+ file supported)
   - `AnalysisEnduranceWithSections_*.csv`
   - `99_Best 10 Laps - *.csv`
3. Click "Start Simulation"

---

## ☁️ Deploy to Azure Static Web Apps (FREE)

### Method 1: Azure Portal (Easiest)

1. **Create Azure Static Web App**
   - Go to [portal.azure.com](https://portal.azure.com)
   - Click "Create a resource" → "Static Web App"
   - Fill in details:
     - **Name:** `cota-analytics`
     - **Region:** `East US 2`
     - **Source:** GitHub
     - **Repository:** `LasaKaru/HackTheTrackAnalytics`
     - **Branch:** `main` or your feature branch
     - **Build Presets:** `Blazor`
     - **App location:** `/`
     - **Api location:** (leave empty)
     - **Output location:** `wwwroot`

2. **Deploy**
   - Click "Review + create" → "Create"
   - Azure will automatically set up GitHub Actions
   - Wait 5-10 minutes for first deployment

3. **Access Your App**
   - URL will be: `https://cota-analytics.azurestaticapps.net`
   - Or custom domain if configured

### Method 2: GitHub Actions (Automated CI/CD)

Azure automatically creates this workflow file:

**.github/workflows/azure-static-web-apps.yml**

```yaml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches:
      - main
  pull_request:
    types: [opened, synchronize, reopened, closed]
    branches:
      - main

jobs:
  build_and_deploy_job:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: ubuntu-latest
    name: Build and Deploy Job
    steps:
      - uses: actions/checkout@v3
        with:
          submodules: true
      - name: Build And Deploy
        id: builddeploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "/"
          output_location: "wwwroot"

  close_pull_request_job:
    if: github.event_name == 'pull_request' && github.event.action == 'closed'
    runs-on: ubuntu-latest
    name: Close Pull Request Job
    steps:
      - name: Close Pull Request
        id: closepullrequest
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          action: "close"
```

### Method 3: Azure CLI

```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login
az login

# Create resource group
az group create --name RacingAppsRG --location eastus2

# Create static web app
az staticwebapp create \
  --name cota-analytics \
  --resource-group RacingAppsRG \
  --source https://github.com/LasaKaru/HackTheTrackAnalytics \
  --location eastus2 \
  --branch main \
  --app-location "/" \
  --output-location "wwwroot" \
  --login-with-github

# Get deployment token (for GitHub Actions)
az staticwebapp secrets list --name cota-analytics --resource-group RacingAppsRG
```

---

## 🐳 Docker Deployment

### Step 1: Create Dockerfile

**Dockerfile** (already in project root):

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["HackTheTrackAnalytics.csproj", "./"]
RUN dotnet restore "HackTheTrackAnalytics.csproj"

# Copy remaining source code
COPY . .

# Build application
RUN dotnet build "HackTheTrackAnalytics.csproj" -c Release -o /app/build

# Publish application
FROM build AS publish
RUN dotnet publish "HackTheTrackAnalytics.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 80
EXPOSE 443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80

# Run application
ENTRYPOINT ["dotnet", "HackTheTrackAnalytics.dll"]
```

### Step 2: Build Docker Image

```bash
docker build -t cota-analytics:latest .
```

### Step 3: Run Container Locally

```bash
docker run -d -p 8080:80 --name cota-analytics cota-analytics:latest
```

Access at: `http://localhost:8080`

### Step 4: Push to Docker Hub (Optional)

```bash
docker login
docker tag cota-analytics:latest yourusername/cota-analytics:latest
docker push yourusername/cota-analytics:latest
```

### Step 5: Deploy to Azure Container Instances

```bash
az container create \
  --resource-group RacingAppsRG \
  --name cota-analytics \
  --image yourusername/cota-analytics:latest \
  --dns-name-label cota-analytics \
  --ports 80
```

---

## 🌐 Custom Domain Setup

### Azure Static Web Apps

1. Go to Azure Portal → Your Static Web App
2. Click "Custom domains"
3. Click "Add"
4. Enter your domain: `analytics.yourdomain.com`
5. Add CNAME record in your DNS:
   ```
   CNAME analytics -> yourapp.azurestaticapps.net
   ```
6. Wait for validation (5-15 minutes)
7. Enable HTTPS (automatic with Let's Encrypt)

---

## 🔒 Environment Configuration

### Production Settings

**appsettings.Production.json**:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "SignalR": {
    "MaximumReceiveMessageSize": 10485760,
    "ClientTimeoutInterval": 30,
    "KeepAliveInterval": 15
  }
}
```

### Environment Variables

Set in Azure Portal → Configuration → Application settings:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:443;http://+:80
```

---

## 📊 Monitoring & Analytics

### Application Insights (Recommended)

```bash
# Install package
dotnet add package Microsoft.ApplicationInsights.AspNetCore

# Add to Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### Azure Portal Monitoring

1. Go to Azure Portal → Your Static Web App
2. Click "Insights" to view:
   - Request metrics
   - Error rates
   - Performance
   - User analytics

---

## 🚨 Troubleshooting

### Issue: Application won't build

**Solution:**
```bash
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### Issue: SignalR connection failures

**Solution:**
- Check firewall rules (port 443/80 open)
- Enable WebSockets in Azure:
  - Azure Portal → Configuration → General settings
  - Web sockets: On

### Issue: Large file uploads failing

**Solution:**
Update `web.config` or Azure settings:
```xml
<system.webServer>
  <security>
    <requestFiltering>
      <requestLimits maxAllowedContentLength="3221225472" /> <!-- 3GB -->
    </requestFiltering>
  </security>
</system.webServer>
```

---

## 📈 Performance Optimization

### Enable Response Compression

Already configured in Program.cs:
```csharp
builder.Services.AddResponseCompression();
app.UseResponseCompression();
```

### Enable Output Caching

```csharp
builder.Services.AddOutputCache();
app.UseOutputCache();
```

### CDN Integration

For static assets (CSS, JS, images):
1. Azure Portal → Your Static Web App → CDN
2. Enable Azure CDN
3. Update asset URLs to use CDN endpoint

---

## 🔄 Continuous Deployment

### Automatic Deployment on Git Push

GitHub Actions automatically deploys on push to `main` branch.

### Manual Deployment Trigger

```bash
# Trigger workflow manually
gh workflow run azure-static-web-apps.yml
```

### Rollback to Previous Version

```bash
# Azure CLI
az staticwebapp environment list --name cota-analytics --resource-group RacingAppsRG
az staticwebapp environment delete --name cota-analytics --resource-group RacingAppsRG --environment-name <version>
```

---

## 📝 Post-Deployment Checklist

- [ ] Test file upload (50MB+ file)
- [ ] Test simulation playback
- [ ] Verify SignalR real-time updates
- [ ] Check pit strategy alerts
- [ ] Test on mobile devices
- [ ] Verify HTTPS certificate
- [ ] Set up monitoring/alerts
- [ ] Document public URL
- [ ] Create demo video

---

## 🎥 Demo Video Recording

### Using OBS Studio

1. **Install OBS:** https://obsproject.com/
2. **Settings:**
   - Resolution: 1920x1080
   - FPS: 30
   - Bitrate: 5000 kbps
3. **Recording Script:**
   - [0:00-0:10] Landing page + title
   - [0:10-0:30] File upload demo
   - [0:30-1:00] Dashboard overview
   - [1:00-1:30] Live simulation
   - [1:30-2:00] Pit strategy alert
   - [2:00-2:30] Sector analysis
   - [2:30-3:00] Conclusion + URL

---

## 📞 Support & Contact

- **GitHub Issues:** https://github.com/LasaKaru/HackTheTrackAnalytics/issues
- **Documentation:** https://github.com/LasaKaru/HackTheTrackAnalytics/wiki
- **Email:** [your-email@example.com]

---

**Built with ❤️ and .NET 8 for Hack the Track 2025**
