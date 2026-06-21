using DeploymentManager.Web.Data;
using DeploymentManager.Web.Models;
using DeploymentManager.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeploymentManager.Web.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeploymentService _deploymentService;

        public DashboardController(ApplicationDbContext context, IDeploymentService deploymentService)
        {
            _context = context;
            _deploymentService = deploymentService;
        }

        // GET: Dashboard or /
        public async Task<IActionResult> Index()
        {
            var servers = await _context.Servers.ToListAsync();
            var deployments = await _context.Deployments
                .Include(d => d.Server)
                .OrderByDescending(d => d.StartedAt)
                .Take(10)
                .ToListAsync();

            // Compute metrics
            ViewBag.TotalServers = servers.Count;
            ViewBag.ActiveServers = servers.Count(s => s.Status == "Active");
            ViewBag.SuccessDeployments = await _context.Deployments.CountAsync(d => d.Status == "Success");
            ViewBag.FailedDeployments = await _context.Deployments.CountAsync(d => d.Status == "Failed");
            ViewBag.RecentDeployments = deployments;

            return View(servers);
        }

        // POST: Dashboard/Deploy
        [HttpPost]
        public async Task<IActionResult> Deploy(List<Guid> serverIds, string packageUrl)
        {
            if (serverIds == null || !serverIds.Any())
            {
                return Json(new { success = false, message = "Please select at least one server." });
            }

            if (string.IsNullOrWhiteSpace(packageUrl))
            {
                return Json(new { success = false, message = "Please specify a valid package URL." });
            }

            try
            {
                // Trigger background deployment
                var deploymentIds = await _deploymentService.TriggerDeploymentsAsync(serverIds, packageUrl);

                // Fetch details of created deployments to return to client
                var deployments = await _context.Deployments
                    .Include(d => d.Server)
                    .Where(d => deploymentIds.Contains(d.Id))
                    .Select(d => new
                    {
                        deploymentId = d.Id.ToString(),
                        serverId = d.ServerId.ToString(),
                        serverName = d.Server != null ? d.Server.Name : "Unknown",
                        status = d.Status,
                        startedAt = d.StartedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    })
                    .ToListAsync();

                return Json(new { success = true, deployments = deployments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Deployment trigger failed: {ex.Message}" });
            }
        }

        // GET: Dashboard/GetDeploymentLogs
        [HttpGet]
        public async Task<IActionResult> GetDeploymentLogs(Guid id)
        {
            var logs = await _context.DeploymentLogs
                .Where(l => l.DeploymentId == id)
                .OrderBy(l => l.Timestamp)
                .Select(l => new
                {
                    message = l.Message,
                    timestamp = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    isError = l.IsError
                })
                .ToListAsync();

            return Json(logs);
        }
    }
}
