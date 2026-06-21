using System;
using System.ComponentModel.DataAnnotations;

namespace DeploymentManager.Web.Models
{
    public class DeploymentLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid DeploymentId { get; set; }

        public virtual Deployment? Deployment { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsError { get; set; }
    }
}
