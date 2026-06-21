using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DeploymentManager.Web.Models
{
    public class Deployment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ServerId { get; set; }

        public virtual Server? Server { get; set; }

        [Required]
        [Url]
        [StringLength(500)]
        [Display(Name = "Package URL")]
        public string PackageUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Success, Failed

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public virtual ICollection<DeploymentLog> Logs { get; set; } = new List<DeploymentLog>();
    }
}
