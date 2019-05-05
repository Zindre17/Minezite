using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;

namespace Minezite.Pages
{
    public class IndexModel : PageModel
    {
        public PingPayload Payload { get; set; } = null;

        public void OnGet()
        {
            Payload = MinecraftServerConnection.Instance.GetLatest();
        }
    }
}
            