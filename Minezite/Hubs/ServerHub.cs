using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Minezite.Hubs
{
    public class ServerHub : Hub
    {
        public async Task UpdateServerStatus()
        {
            var payload = MinecraftServerConnection.Instance.GetLatest();
            await Clients.All.SendAsync("dataChanged", payload);
        }
    }
}
