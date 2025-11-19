# AZ IoT Weather Station

This repository contains two projects for a simple Azure IoT demo:

- `WeatherStation` — an Azure Function (Event Hub trigger) that reads IoT Hub messages and updates an Azure Digital Twins twin.
- `WeatherStationSimulator` — a small console app that simulates a device sending telemetry to an IoT Hub.

**Quick Summary**

- **Function project:** `WeatherStation/WeatherStation.csproj` (Target: `net8.0`, Azure Functions v4)
- **Simulator project:** `WeatherStationSimulator/WeatherStationSimulator.csproj` (Target: `net10.0`)

**How it works**

- The simulator sends JSON telemetry messages (temperature, humidity, pressure) to an IoT Hub device endpoint.
- The Function `IoTHubToADT` listens on an Event Hub-compatible endpoint (`messages/events`, connection name `IOT_HUB_CONNECTION`), parses telemetry, and patches a Digital Twins twin at `ADT_SERVICE_URL`.

Important: the function requires `ADT_SERVICE_URL` to be set (see `WeatherStation/local.settings.json`) and the Event Hub connection string configured via the `IOT_HUB_CONNECTION` setting.

**Files of interest**

- `WeatherStation/IoTHubToADT.cs` — function source (EventHubTrigger, uses `DefaultAzureCredential` and `ADT_SERVICE_URL`).
- `WeatherStation/local.settings.json` — local settings copied to output for local development (do NOT commit production secrets).
- `__blobstorage__/`, `__queuestorage__/`, etc. — Azurite local storage files included for local emulator testing.

Security note: The simulator currently contains a hard-coded device connection string in `WeatherStationSimulator/Program.cs`. Replace this with a configuration value (environment variable or `local.settings.json`) before committing to source control.

Build & run (PowerShell)

1) Build both projects

```powershell
dotnet build .\WeatherStation\WeatherStation.csproj
dotnet build .\WeatherStationSimulator\WeatherStationSimulator.csproj
```

2) Run the Function locally

Option A — use the included VS Code tasks:
- Run the `build (functions)` task, then run the `func: 4` task (the workspace tasks are configured to build and start the host in `WeatherStation/bin/Debug/net8.0`).

Option B — CLI (after building):

```powershell
Set-Location -Path .\WeatherStation\bin\Debug\net8.0
func host start
```

3) Run the Simulator

```powershell
dotnet run --project .\WeatherStationSimulator\WeatherStationSimulator.csproj
```

Device connection string (simulator)

 - The simulator now reads the device connection string from the environment variable `DEVICE_CONNECTION_STRING`.
 - Example (PowerShell) to run the simulator for a single session:

 ```powershell
$Env:DEVICE_CONNECTION_STRING = "HostName=your-iothub.azure-devices.net;DeviceId=your-device-id;SharedAccessKey=..."
dotnet run --project .\WeatherStationSimulator\WeatherStationSimulator.csproj
```

 - To persist it for the user session you can use `setx` (note: requires a new shell to take effect):

 ```powershell
setx DEVICE_CONNECTION_STRING "HostName=...;DeviceId=...;SharedAccessKey=..."
```
```

Configuration & environment

- Set `ADT_SERVICE_URL` in `WeatherStation/local.settings.json` or environment to point at your Azure Digital Twins instance.
- Ensure `IOT_HUB_CONNECTION` points to your Event Hub-compatible IoT Hub endpoint (used by the Function trigger).
- Replace the hard-coded device connection string in `WeatherStationSimulator/Program.cs` with a secure configuration source.

Troubleshooting

- If the Function logs "ADT_SERVICE_URL is not configured", make sure that `ADT_SERVICE_URL` is present in `local.settings.json` and that you restarted the Functions host.
- The Function logs HTTP responses when fetching/updating the twin — check logs for status codes and response bodies.

Next steps / suggestions

- Move device connection strings and other secrets to `local.settings.json` (which is not for production) or to environment variables in CI/CD.
- Consider using managed identity or Azure Key Vault for ADT credentials instead of embedding secrets.
- Remove the hard-coded device key from `WeatherStationSimulator/Program.cs` and read the connection string from an environment variable.

---

If you want, I can:

- Replace the hard-coded device connection string in `WeatherStationSimulator/Program.cs` with code that reads from an environment variable and update `README.md` with exact env var names.
- Add a `.gitignore` entry reminder to avoid committing `local.settings.json` with secrets.

