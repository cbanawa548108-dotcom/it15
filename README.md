# CRL Fruitstand ESS — Landing Page

**Executive Decision Support System**  
A public-facing landing page built with C# ASP.NET Core 8 MVC.

---

## 📁 Project Structure

```
CRLFruitstandESS/
├── Controllers/
│   ├── HomeController.cs        # Home, About, Features pages
│   └── AccountController.cs     # Login page (placeholder)
├── Views/
│   ├── Home/
│   │   └── Index.cshtml         # Landing page (Hero + About + Features)
│   ├── Account/
│   │   └── Login.cshtml         # Login page UI
│   ├── Shared/
│   │   └── _Layout.cshtml       # Shared navbar + footer layout
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── wwwroot/
│   ├── css/site.css             # Blue-themed responsive stylesheet
│   └── js/site.js               # Navbar scroll, reveal animations, counters
├── .vscode/
│   ├── launch.json              # VS Code debug config
│   └── tasks.json               # Build / watch tasks
├── appsettings.json
├── Program.cs
└── CRLFruitstandESS.csproj
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio Code](https://code.visualstudio.com/)
- [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

### Run Locally

```bash
# 1. Navigate to the project folder
cd CRLFruitstandESS

# 2. Restore dependencies
dotnet restore

# 3. Run with hot reload
dotnet watch run
```

Then open your browser at: **https://localhost:7080** (or http://localhost:5080)

### Run in VS Code
1. Open the `CRLFruitstandESS` folder in VS Code
2. Press **F5** to launch with the debugger attached
3. The browser will open automatically

---

## 🎨 Design

| Property | Value |
|---|---|
| Theme | Blue Executive |
| Font (Display) | Playfair Display |
| Font (Body) | DM Sans |
| Primary Color | `#2d7ef7` (Accent Blue) |
| Background | `#0a1628` (Navy) |
| Gold Accent | `#f0b429` |

---

## 📄 Pages

| Route | Description |
|---|---|
| `/` | Landing page — Hero, About, Features |
| `/Account/Login` | Login form (UI only, no auth required) |

---

## 👨‍💻 Developer

**Cris Lee Banawa**  
CRL Fruitstand Executive Decision Support System
