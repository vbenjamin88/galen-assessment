// Upload a CSV file to Azure Blob Storage (inbound container)
// Usage: dotnet run -- <csv-path>
// Reads connection string from env AzureWebJobsStorage or ../Galen.Integration.Functions/local.settings.json

using Azure.Storage.Blobs;

var csvPath = args.FirstOrDefault();
if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
{
    Console.WriteLine("Usage: dotnet run -- <path-to-csv>");
    Console.WriteLine("Example: dotnet run -- ../../sample-data/sample.csv");
    return 1;
}

var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
if (string.IsNullOrEmpty(connectionString))
{
    var settingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Galen.Integration.Functions", "local.settings.json"));
    if (File.Exists(settingsPath))
    {
        var json = await File.ReadAllTextAsync(settingsPath);
        var match = System.Text.RegularExpressions.Regex.Match(json, @"AzureWebJobsStorage""\s*:\s*""([^""]+)""");
        if (match.Success)
            connectionString = match.Groups[1].Value;
    }
}

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Error: AzureWebJobsStorage not found. Set env var or ensure local.settings.json exists.");
    return 1;
}

var fileName = Path.GetFileName(csvPath);
var containerName = Environment.GetEnvironmentVariable("BlobContainerName") ?? "inbound";

try
{
    var client = new BlobContainerClient(connectionString, containerName);
    await client.CreateIfNotExistsAsync();
    var blob = client.GetBlobClient(fileName);
    await using var stream = File.OpenRead(csvPath);
    await blob.UploadAsync(stream, overwrite: true);
    Console.WriteLine($"Uploaded: {fileName} -> {containerName}/{fileName}");
    Console.WriteLine("If your Function App is deployed and listening, it will process the file shortly.");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}
