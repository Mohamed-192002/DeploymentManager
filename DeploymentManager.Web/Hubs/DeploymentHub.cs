using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DeploymentManager.Web.Hubs
{
    public class DeploymentHub : Hub
    {
        public async Task JoinDeploymentGroup(string deploymentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, deploymentId);
        }

        public async Task LeaveDeploymentGroup(string deploymentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, deploymentId);
        }
    }
}
