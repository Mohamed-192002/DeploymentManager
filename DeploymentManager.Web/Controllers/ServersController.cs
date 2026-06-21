using DeploymentManager.Web.Data;
using DeploymentManager.Web.Models;
using DeploymentManager.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DeploymentManager.Web.Controllers
{
    public class ServersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IDeploymentService _deploymentService;

        public ServersController(
            ApplicationDbContext context, 
            IEncryptionService encryptionService,
            IDeploymentService deploymentService)
        {
            _context = context;
            _encryptionService = encryptionService;
            _deploymentService = deploymentService;
        }

        // GET: Servers
        public async Task<IActionResult> Index()
        {
            var servers = await _context.Servers.ToListAsync();
            return View(servers);
        }

        // GET: Servers/Create
        public IActionResult Create()
        {
            return View(new Server());
        }

        // POST: Servers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Server server)
        {
            if (ModelState.IsValid)
            {
                server.AdminPasswordEncrypted = _encryptionService.Encrypt(server.AdminPasswordEncrypted);
                _context.Add(server);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(server);
        }

        // GET: Servers/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();

            // Decrypt password for editing
            server.AdminPasswordEncrypted = _encryptionService.Decrypt(server.AdminPasswordEncrypted);
            return View(server);
        }

        // POST: Servers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Server server)
        {
            if (id != server.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    server.AdminPasswordEncrypted = _encryptionService.Encrypt(server.AdminPasswordEncrypted);
                    _context.Update(server);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServerExists(server.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(server);
        }

        // GET: Servers/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var server = await _context.Servers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (server == null) return NotFound();

            return View(server);
        }

        // POST: Servers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server != null)
            {
                _context.Servers.Remove(server);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Servers/TestConnection
        [HttpPost]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            var success = await _deploymentService.TestConnectionAsync(id);
            var server = await _context.Servers.FindAsync(id);
            return Json(new { success = success, status = server?.Status ?? "Inactive" });
        }

        private bool ServerExists(Guid id)
        {
            return _context.Servers.Any(e => e.Id == id);
        }
    }
}
