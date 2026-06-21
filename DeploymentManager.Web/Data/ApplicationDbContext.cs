using DeploymentManager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DeploymentManager.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Server> Servers { get; set; } = null!;
        public DbSet<Deployment> Deployments { get; set; } = null!;
        public DbSet<DeploymentLog> DeploymentLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Server relationships
            modelBuilder.Entity<Server>()
                .HasMany(s => s.Deployments)
                .WithOne(d => d.Server)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Deployment relationships
            modelBuilder.Entity<Deployment>()
                .HasMany(d => d.Logs)
                .WithOne(l => l.Deployment)
                .HasForeignKey(l => l.DeploymentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
