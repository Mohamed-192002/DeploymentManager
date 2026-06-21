using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DeploymentManager.Web.Models
{
    public class Server
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "IP Address")]
        public string IpAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string Domain { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "IIS Site Name")]
        public string IisSiteName { get; set; } = string.Empty;

        public int? Port { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Admin Username")]
        public string AdminUsername { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Admin Password")]
        public string AdminPasswordEncrypted { get; set; } = string.Empty;

        [Required]
        [Url]
        [Display(Name = "API Base URL")]
        public string ApiBaseUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "API Key")]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for deployments
        public virtual ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
    }
}
