# AI Gaming Optimizer

Windows desktop optimizer with a dark dashboard, live CPU/GPU/RAM gauges, process boosting, junk cleanup, startup control, and an AI chat assistant that can call real system tools.

Built with **C# / WPF on .NET 8**.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop workload)
- Optional: NVIDIA driver (for NVML GPU utilization). AMD/Intel GPUs use Windows GPU Engine counters.

## Run

```powershell
cd src\AIGamingOptimizer
dotnet restore
dotnet run
```

Release build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

The published exe is under `src\AIGamingOptimizer\bin\Release\net8.0-windows\win-x64\publish\`.

## Test locally

1. Launch the app and confirm Overview gauges move as you load the machine.
2. **Analyze System** should fill the dashboard cards (background apps, junk estimate, startup count, Game Mode).
3. **Game Boost**: refresh the process list, leave “Safe to close” rows checked, run One-Click Game Boost. Browsers/updaters should close; `csrss`/`lsass`/`explorer` must stay.
4. **System Cleaner**: Scan then Clean. Temp files that are locked are skipped.
5. **Startup & Services**: toggle a non-critical Run key, then **Gaming Power Plan**.
6. **AI Assistant**: try “What processes are taking up most of my CPU?” and “Boost my FPS now”. The model receives live JSON from this PC and can execute tools.

Run as Administrator if Windows Temp, some processes, or HKLM startup keys need elevation. The sidebar shows the current privilege.

## AI API

`appsettings.json`:

```json
{
  "Ai": {
    "ApiKey": "your-key",
    "BaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "AuthStyle": "Auto"
  }
}
```

- `AuthStyle`: `Bearer` (OpenAI-compatible), `ApiKey` (Azure-style `api-key` header), or `Auto` (`AQ.` keys use `api-key`).
- Override with env vars `Ai__ApiKey`, `Ai__BaseUrl`, `Ai__Model`, or `AI_API_KEY`.
- Optional `appsettings.Local.json` (gitignored) for machine-specific endpoints.

The assistant tools are: `get_system_snapshot`, `get_top_processes`, `game_boost`, `close_browsers`, `close_processes`, `clean_temp_files`, `apply_gaming_power_plan`, `analyze_system`.

## Project layout

```
src/AIGamingOptimizer/
  App.xaml(.cs)          DI host + theme
  MainWindow.xaml(.cs)   Custom chrome + sidebar
  Views/                 Overview, Game Boost, Cleaner, Startup, Assistant
  ViewModels/            MVVM + commands
  Services/              Metrics, processes, cleanup, startup, AI + tools
  Native/                Win32 (memory / working set)
  Themes/                Dark UI
```

## Safety notes

- Critical system processes are never targeted by Game Boost.
- Registry cleanup is limited to Explorer Run MRU and typed paths — not a shotgun “registry fixer”.
- Closing apps and switching power plans are destructive by design. Use on your own machine.
- Rotate the API key if this project is shared; do not commit extra secrets.
