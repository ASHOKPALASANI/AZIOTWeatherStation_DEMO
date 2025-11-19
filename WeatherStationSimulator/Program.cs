using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;

// Read device connection string from environment for security.
// Set the environment variable `DEVICE_CONNECTION_STRING` before running.
string? deviceConnectionString = Environment.GetEnvironmentVariable("DEVICE_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(deviceConnectionString))
{
    Console.Error.WriteLine("Environment variable DEVICE_CONNECTION_STRING is not set.\nSet it with your device connection string and retry. Example (PowerShell):\n$Env:DEVICE_CONNECTION_STRING='<your-connstr>'; dotnet run --project .\\WeatherStationSimulator\\WeatherStationSimulator.csproj");
    return;
}

DeviceClient client = DeviceClient.CreateFromConnectionString(deviceConnectionString);

Random rnd = new();

while (true)
{
    var telemetry = new
    {
        temperature = rnd.Next(15, 35) + rnd.NextDouble(),
        humidity = rnd.Next(40, 90) + rnd.NextDouble(),
        pressure = 1010 + rnd.NextDouble() * 10
    };

    string json = JsonConvert.SerializeObject(telemetry);
    Message msg = new Message(Encoding.UTF8.GetBytes(json));

    await client.SendEventAsync(msg);
    Console.WriteLine($"Sent: {json}");

    await Task.Delay(5000);
}
