using System.IO.Compression;
using Microsoft.Web.Administration;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddHttpClient();
builder.Services.AddLogging();

var app = builder.Build();

// Configuration shortcut
var config = app.Configuration;

// Helper log container
var deploymentLogs = new List<string>();

// Health Endpoint
app.MapGet("/api/health", (IConfiguration cfg) =>
{
    var simulate = cfg.GetValue<bool>("DeploymentConfig:SimulateIIS");
    var target = cfg.GetValue<string>("DeploymentConfig:TargetDirectory") ?? "Not Configured";
    return Results.Ok(new
    {
        status = "Healthy",
        version = "1.0.0",
        iisSimulation = simulate,
        targetDirectory = target
    });
})
.AddEndpointFilter(ApiKeyFilter);

// Deploy Endpoint
app.MapPost("/api/deploy", async (DeployRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration cfg, ILogger<Program> logger) =>
{
    var logs = new List<string>();
    var start = DateTime.UtcNow;
    logs.Add($"[Info] Started deployment at {start} UTC");
    logs.Add($"[Info] Server ID: {request.ServerId}");
    logs.Add($"[Info] Package URL: {request.PackageUrl}");

    string targetDirectory = request.TargetDirectory ?? cfg.GetValue<string>("DeploymentConfig:TargetDirectory") ?? @"D:\New folder\MockDeployDirectory";
    string appPoolName = request.IisAppPoolName ?? cfg.GetValue<string>("DeploymentConfig:IisAppPoolName") ?? "DefaultAppPool";
    bool simulateIis = request.SimulateIis ?? cfg.GetValue<bool>("DeploymentConfig:SimulateIIS");

    string tempZipPath = Path.Combine(Path.GetTempPath(), $"deploy_{Guid.NewGuid()}.zip");
    string tempExtractPath = Path.Combine(Path.GetTempPath(), $"extract_{Guid.NewGuid()}");

    try
    {
        // 1. Ensure Target Directory exists
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
            logs.Add($"[Info] Created target directory: {targetDirectory}");
        }

        // 2. Download package
        logs.Add($"[Info] Downloading deployment package from {request.PackageUrl}...");
		if (request.PackageUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
		{
			var localPath = new Uri(request.PackageUrl).LocalPath;
			logs.Add($"[Info] Copying package from file path: {localPath}");
			File.Copy(localPath, tempZipPath, overwrite: true);
		}
		else
		{
			using var client = httpClientFactory.CreateClient();
			using var response = await client.GetAsync(
				request.PackageUrl,
				HttpCompletionOption.ResponseHeadersRead
			);

			response.EnsureSuccessStatusCode();

			using var fileStream = new FileStream(
				tempZipPath,
				FileMode.Create,
				FileAccess.Write
			);

			await response.Content.CopyToAsync(fileStream);
		}
		//using (var client = httpClientFactory.CreateClient())
		//      {
		//          using (var response = await client.GetAsync(request.PackageUrl, HttpCompletionOption.ResponseHeadersRead))
		//          {
		//              if (!response.IsSuccessStatusCode)
		//              {
		//                  throw new Exception($"Failed to download package. HTTP Status: {response.StatusCode}");
		//              }
		//              using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
		//              {
		//                  await response.Content.CopyToAsync(fileStream);
		//              }
		//          }
		//      }
		logs.Add($"[Info] Download complete. Temp zip saved at {tempZipPath}");

        // 3. Backup current version (Optional)
        //try
        //{
        //    var filesInTarget = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);
        //    if (filesInTarget.Length > 0)
        //    {
        //        string backupDir = Path.Combine(Directory.GetParent(targetDirectory)?.FullName ?? targetDirectory, "backups");
        //        if (!Directory.Exists(backupDir))
        //        {
        //            Directory.CreateDirectory(backupDir);
        //        }
        //        string backupPath = Path.Combine(backupDir, $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        //        logs.Add($"[Info] Backing up current files to {backupPath}...");
        //        ZipFile.CreateFromDirectory(targetDirectory, backupPath);
        //        logs.Add($"[Info] Backup completed successfully.");
        //    }
        //    else
        //    {
        //        logs.Add("[Info] Target folder is empty. Skipping backup.");
        //    }
        //}
        //catch (Exception ex)
        //{
        //    logs.Add($"[Warning] Backup failed: {ex.Message}. Continuing with deployment.");
        //}

        // 4. Stop IIS App Pool
        if (simulateIis)
        {
            logs.Add($"[IIS Simulation] Stopping Application Pool '{appPoolName}'...");
            await Task.Delay(1000); // Simulate some work
            logs.Add($"[IIS Simulation] Application Pool '{appPoolName}' stopped.");
        }
        else
        {
            logs.Add($"[IIS] Stopping Application Pool '{appPoolName}'...");
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[appPoolName];
                    if (pool != null)
                    {
                        if (pool.State == ObjectState.Started || pool.State == ObjectState.Starting)
                        {
                            pool.Stop();
                            int retry = 0;
                            while (pool.State != ObjectState.Stopped && retry < 15)
                            {
                                await Task.Delay(1000);
                                retry++;
                            }
                            logs.Add($"[IIS] Application Pool '{appPoolName}' stopped successfully.");
                        }
                        else
                        {
                            logs.Add($"[IIS] Application Pool '{appPoolName}' was already in {pool.State} state.");
                        }
                    }
                    else
                    {
                        logs.Add($"[Warning] Application Pool '{appPoolName}' not found in IIS. Proceeding.");
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[Warning] Failed to stop App Pool via IIS API: {ex.Message}. Falling back to Simulation.");
                logs.Add($"[IIS Simulation] Stopping Application Pool '{appPoolName}'...");
                await Task.Delay(500);
            }
        }

        // 5. Extract Package
        logs.Add($"[Info] Extracting package ZIP to temporary directory: {tempExtractPath}...");
        ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);
        logs.Add($"[Info] Extraction complete.");

        // 6. Copy and Overwrite Only
        logs.Add($"[Info] Copying files to deployment directory '{targetDirectory}'...");
        var filesToCopy = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
        int copiedFilesCount = 0;

        foreach (var file in filesToCopy)
        {
            var relativePath = Path.GetRelativePath(tempExtractPath, file);
            var destPath = Path.Combine(targetDirectory, relativePath);
            var destDir = Path.GetDirectoryName(destPath);

            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, destPath, overwrite: true);
            copiedFilesCount++;
        }
        logs.Add($"[Info] Successfully deployed/overwrote {copiedFilesCount} files. Existing non-packaged files preserved.");

        // 7. Start IIS App Pool
        if (simulateIis)
        {
            logs.Add($"[IIS Simulation] Starting Application Pool '{appPoolName}'...");
            await Task.Delay(1000);
            logs.Add($"[IIS Simulation] Application Pool '{appPoolName}' started.");
        }
        else
        {
            logs.Add($"[IIS] Starting Application Pool '{appPoolName}'...");
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[appPoolName];
                    if (pool != null)
                    {
                        if (pool.State == ObjectState.Stopped || pool.State == ObjectState.Stopping)
                        {
                            pool.Start();
                            int retry = 0;
                            while (pool.State != ObjectState.Started && retry < 15)
                            {
                                await Task.Delay(1000);
                                retry++;
                            }
                            logs.Add($"[IIS] Application Pool '{appPoolName}' started successfully.");
                        }
                        else
                        {
                            logs.Add($"[IIS] Application Pool '{appPoolName}' is in {pool.State} state.");
                        }
                    }
                    else
                    {
                        logs.Add($"[Warning] Application Pool '{appPoolName}' not found in IIS.");
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[Warning] Failed to start App Pool via IIS API: {ex.Message}. Falling back to Simulation.");
                logs.Add($"[IIS Simulation] Starting Application Pool '{appPoolName}'...");
                await Task.Delay(500);
            }
        }

        var end = DateTime.UtcNow;
        logs.Add($"[Success] Deployment completed successfully at {end} UTC in {(end - start).TotalSeconds:F2} seconds.");

        return Results.Ok(new DeploymentResponse(true, "Deployment successful.", logs));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Deployment failed.");
        logs.Add($"[Error] Deployment failed: {ex.Message}");

        // Attempt to start App Pool just in case it was stopped
        try
        {
            if (simulateIis)
            {
                logs.Add("[IIS Simulation] Ensuring App Pool is started after failure...");
            }
            else
            {
                logs.Add("[IIS] Attempting to restart App Pool after failure...");
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[appPoolName];
                    if (pool != null && pool.State == ObjectState.Stopped)
                    {
                        pool.Start();
                        logs.Add("[IIS] App Pool restarted.");
                    }
                }
            }
        }
        catch (Exception pex)
        {
            logs.Add($"[Warning] Could not restart App Pool: {pex.Message}");
        }

        return Results.Json(new DeploymentResponse(false, $"Deployment failed: {ex.Message}", logs), statusCode: 500);
    }
    finally
    {
        // Clean up temp files
        try
        {
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
            if (Directory.Exists(tempExtractPath)) 
            {
                var dirInfo = new DirectoryInfo(tempExtractPath);
                foreach (var info in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    info.Attributes = FileAttributes.Normal;
                }
                dirInfo.Attributes = FileAttributes.Normal;
                Directory.Delete(tempExtractPath, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up temp files.");
        }
    }
})
.AddEndpointFilter(ApiKeyFilter);

app.Run();

// API Key Authentication Endpoint Filter
static async ValueTask<object?> ApiKeyFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var httpContext = context.HttpContext;
    var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
    var expectedKey = config["DeploymentConfig:ApiKey"];

    if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) || providedKey != expectedKey)
    {
        return Results.Json(new { success = false, message = "Unauthorized: Invalid or missing X-Api-Key header." }, statusCode: 401);
    }

    return await next(context);
}

// Request & Response Records
public record DeployRequest(string ServerId, string PackageUrl, string? TargetDirectory, string? IisAppPoolName, bool? SimulateIis);
public record DeploymentResponse(bool Success, string Message, List<string> Logs);
