using DeploymentManager.Web.Data;
using DeploymentManager.Web.Hubs;
using DeploymentManager.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeploymentManager.Web.Services
{
    public interface IDeploymentService
    {
        Task<List<Guid>> TriggerDeploymentsAsync(List<Guid> serverIds, string version, string packageUrl);
        Task<bool> TestConnectionAsync(Guid serverId);
    }

    public class DeploymentService : IDeploymentService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<DeploymentHub> _hubContext;
        private readonly ILogger<DeploymentService> _logger;

        public DeploymentService(
            IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory,
            IHubContext<DeploymentHub> hubContext,
            ILogger<DeploymentService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<List<Guid>> TriggerDeploymentsAsync(List<Guid> serverIds, string version, string packageUrl)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var deploymentIds = new List<Guid>();
            var deploymentsToRun = new List<Deployment>();

            foreach (var serverId in serverIds)
            {
                var deployment = new Deployment
                {
                    Id = Guid.NewGuid(),
                    ServerId = serverId,
                    Version = version,
                    PackageUrl = packageUrl,
                    Status = "Pending",
                    StartedAt = DateTime.UtcNow
                };
                deploymentsToRun.Add(deployment);
                context.Deployments.Add(deployment);
                deploymentIds.Add(deployment.Id);
            }
            await context.SaveChangesAsync();

            // Run in background and return immediately to the controller
            _ = Task.Run(async () =>
            {
                var tasks = deploymentsToRun.Select(d => ExecuteDeploymentAsync(d.Id));
                await Task.WhenAll(tasks);
            });

            return deploymentIds;
        }

        public async Task<bool> TestConnectionAsync(Guid serverId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var server = await context.Servers.FindAsync(serverId);
            if (server == null) return false;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);

                var url = $"{server.ApiBaseUrl.TrimEnd('/')}/api/health";
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    server.Status = "Active";
                    await context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Health check failed for server {server.Name}");
            }

            server.Status = "Inactive";
            await context.SaveChangesAsync();
            return false;
        }

        private async Task ExecuteDeploymentAsync(Guid deploymentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var deployment = await context.Deployments
                .Include(d => d.Server)
                .FirstOrDefaultAsync(d => d.Id == deploymentId);

            if (deployment == null || deployment.Server == null) return;

            // Helper to add and log
            async Task LogStepAsync(string message, bool isError = false)
            {
                var log = new DeploymentLog
                {
                    DeploymentId = deploymentId,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    IsError = isError
                };
                context.DeploymentLogs.Add(log);
                await context.SaveChangesAsync();

                // Stream via SignalR to clients subscribed to this deployment ID
                await _hubContext.Clients.Group(deploymentId.ToString()).SendAsync("ReceiveLog", new
                {
                    message = log.Message,
                    timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    isError = log.IsError
                });

                // Also update the UI with status changes
                await _hubContext.Clients.All.SendAsync("UpdateDeploymentStatus", new
                {
                    deploymentId = deploymentId.ToString(),
                    serverId = deployment.ServerId.ToString(),
                    serverName = deployment.Server.Name,
                    status = deployment.Status,
                    completedAt = deployment.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                });
            }

            try
            {
                deployment.Status = "InProgress";
                await context.SaveChangesAsync();
                await LogStepAsync($"[Manager] Initializing deployment connection to agent {deployment.Server.ApiBaseUrl}...");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("X-Api-Key", deployment.Server.ApiKey);

                var url = $"{deployment.Server.ApiBaseUrl.TrimEnd('/')}/api/deploy";
                var payload = new
                {
                    serverId = deployment.ServerId.ToString(),
                    version = deployment.Version,
                    packageUrl = deployment.PackageUrl
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await LogStepAsync($"[Manager] Dispatching REST request to {url}...");

                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<AgentResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result != null && result.Success)
                    {
                        // Add all agent logs
                        if (result.Logs != null)
                        {
                            foreach (var agentLog in result.Logs)
                            {
                                await LogStepAsync(agentLog);
                            }
                        }

                        deployment.Status = "Success";
                        await LogStepAsync("[Manager] Agent reported successful deployment execution.");
                    }
                    else
                    {
                        if (result?.Logs != null)
                        {
                            foreach (var agentLog in result.Logs)
                            {
                                await LogStepAsync(agentLog, isError: true);
                            }
                        }
                        deployment.Status = "Failed";
                        await LogStepAsync($"[Manager] Agent reported failure: {result?.Message ?? "Unknown Error"}", isError: true);
                    }
                }
                else
                {
                    deployment.Status = "Failed";
                    await LogStepAsync($"[Manager] Agent returned HTTP error code: {response.StatusCode}. Details: {responseContent}", isError: true);
                }
            }
            catch (Exception ex)
            {
                deployment.Status = "Failed";
                await LogStepAsync($"[Manager] Error communicating with agent: {ex.Message}", isError: true);
            }
            finally
            {
                deployment.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                // Final broadcast
                await _hubContext.Clients.All.SendAsync("UpdateDeploymentStatus", new
                {
                    deploymentId = deploymentId.ToString(),
                    serverId = deployment.ServerId.ToString(),
                    serverName = deployment.Server.Name,
                    status = deployment.Status,
                    completedAt = deployment.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                });
            }
        }
    }

    public class AgentResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? Logs { get; set; }
    }
}
