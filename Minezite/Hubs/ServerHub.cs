using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Minezite.Hubs
{
    public class ServerHub : Hub
    {
        public async Task UpdateServerStatus(PingPayload payload)
        {
            await Clients.All.SendAsync("ReceivePayload", payload);
        }
    }
}
