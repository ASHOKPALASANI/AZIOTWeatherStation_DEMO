using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Azure.Identity;
using Azure.Core;

public class TelemetryData {
    public double temperature { get; set; }
    public double humidity { get; set; }
    public double pressure { get; set; }
}

public class IoTHubToADT
{
    private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
    private static readonly HttpClient httpClient = new HttpClient();

    [FunctionName("IoTHubToADT")]
    public async Task Run(
        [EventHubTrigger("messages/events", Connection = "IOT_HUB_CONNECTION")] byte[] message,
        ILogger log)
    {
        if (string.IsNullOrEmpty(adtInstanceUrl))
        {
            log.LogError("ADT_SERVICE_URL is not configured.");
            return;
        }

        string payload = Encoding.UTF8.GetString(message);
        var data = JsonConvert.DeserializeObject<TelemetryData>(payload);

        // Acquire token for ADT
        var credential = new DefaultAzureCredential();
        var tokenRequestContext = new TokenRequestContext(new[] { "https://digitaltwins.azure.net/.default" });
        var accessToken = await credential.GetTokenAsync(tokenRequestContext);

        string twinId = "WeatherStationTwin";
        var twinUrl = new Uri(new Uri(adtInstanceUrl), $"digitaltwins/{twinId}?api-version=2023-10-31");

        // Fetch current twin to inspect structure
        var twinRequest = new HttpRequestMessage(HttpMethod.Get, twinUrl);
        twinRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        var twinResponse = await httpClient.SendAsync(twinRequest);
        if (!twinResponse.IsSuccessStatusCode)
        {
            string err = await twinResponse.Content.ReadAsStringAsync();
            log.LogError($"Failed to fetch twin. Status: {twinResponse.StatusCode}, Response: {err}");
            return;
        }

        var twinJsonString = await twinResponse.Content.ReadAsStringAsync();
        log.LogInformation($"Fetched twin JSON: {twinJsonString}");
        var twinJson = JObject.Parse(twinJsonString);

        // Build dynamic JSON Patch
        var patchList = new JArray();

        // Recursively search the twin JSON for properties matching a given name and
        // return JSON Patch compatible paths (leading slash, nested with slashes).
        void FindPropertyPaths(JToken node, string propertyName, string currentPath, IList<string> results)
        {
            if (node is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    var propPath = string.IsNullOrEmpty(currentPath) ? prop.Name : currentPath + "/" + prop.Name;

                    if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found a matching property name
                        results.Add("/" + propPath);
                    }

                    // Recurse into child objects
                    if (prop.Value is JObject || prop.Value is JArray)
                    {
                        FindPropertyPaths(prop.Value, propertyName, propPath, results);
                    }
                }
            }
            else if (node is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var item = arr[i];
                    var arrPath = currentPath + "/" + i;
                    FindPropertyPaths(item, propertyName, arrPath, results);
                }
            }
        }

        void AddPatchFor(string propertyName, double value)
        {
            if (twinJson[propertyName] != null)
            {
                patchList.Add(new JObject
                {
                    ["op"] = "replace",
                    ["path"] = "/" + propertyName,
                    ["value"] = value
                });
                log.LogInformation($"Added patch for '{propertyName}' at path '/{propertyName}' with value {value}.");
            }
            else
            {
                log.LogWarning($"Property '{propertyName}' not found on twin. Skipping patch.");
            }
        }


        AddPatchFor("temperature", data.temperature);
        AddPatchFor("humidity", data.humidity);
        AddPatchFor("pressure", data.pressure);

        if (patchList.Count == 0)
        {
            log.LogWarning("No matching properties found on twin for patch.");
            return;
        }

        string patchJson = JsonConvert.SerializeObject(patchList);
        log.LogInformation($"Patch JSON: {patchJson}");

        // Send PATCH request
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, twinUrl);
        patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        patchRequest.Content = new StringContent(patchJson, Encoding.UTF8, "application/json-patch+json");

        var patchResponse = await httpClient.SendAsync(patchRequest);
        if (!patchResponse.IsSuccessStatusCode)
        {
            string resp = await patchResponse.Content.ReadAsStringAsync();
            log.LogError($"Failed to update twin. Status: {patchResponse.StatusCode}, Response: {resp}");
            return;
        }

        log.LogInformation($"Successfully updated twin with telemetry: {payload}");
    }
}
