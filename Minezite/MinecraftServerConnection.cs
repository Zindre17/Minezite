using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Minezite
{

    public interface IDataUpdate
    {
        void DataChanged(PingPayload payload);
    }
    public class MinecraftServerConnection:IHostedService, IDisposable
    {
        private List<byte> _buffer;
        private NetworkStream _stream;
        private int _offset = 0;

        private Timer _timer;

        private static string server = "minez.northeurope.cloudapp.azure.com";
        private static int port = 25565;

        private TcpClient client;
        private bool handShakeSent = false;
        private PingPayload latestPing;

        private static MinecraftServerConnection instance = null;
        public static MinecraftServerConnection Instance {
            get {
                if (instance == null)
                {
                    instance = new MinecraftServerConnection();
                }
                return instance;
            }
        }
        

        private IDataUpdate listener = null;
        public void SetListener(IDataUpdate listener)
        {
            this.listener = listener;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("starting background service");
            if (instance == null)
                instance = new MinecraftServerConnection();
            _timer = new Timer(ScheduledFunction, null, TimeSpan.Zero, TimeSpan.FromSeconds(20));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("stopping background service");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public MinecraftServerConnection()
        {
            if (instance != null) return;
            instance = this;
            _buffer = new List<byte>();
        }
        private void ScheduledFunction(object state)
        {
            Console.WriteLine("Timed background process is running");
            Fetch();
        }

        private async Task Fetch()
        {
            await Connect();
            SendHandShake();
            SendStatusRequest();
            ReadStatusResponse();
            client.Close();
            if (listener != null) 
                listener.DataChanged(latestPing);
        }

        public bool IsConnected()
        {
            return client.Connected;
        }

        public PingPayload GetLatest()
        {
            return latestPing;
        }

        private async Task Connect()
        {
            client = new TcpClient();
            _buffer.Clear();
            _offset = 0;
            await client.ConnectAsync(server, port);
            _stream = client.GetStream();
        }

        private void SendHandShake()
        {
            /*
             * Send a "Handshake" packet
             * http://wiki.vg/Server_List_Ping#Ping_Process
             */
            WriteVarInt(477);
            WriteString("localhost");
            WriteShort(25565);
            WriteVarInt(1);
            Flush(0);
        }
            
        private void SendStatusRequest()
        {
            /*
             * Send a "Status Request" packet
             * http://wiki.vg/Server_List_Ping#Ping_Process
             */
            Flush(0);

        }

        private void ReadStatusResponse()
        {
            /*
         * If you are using a modded server then use a larger buffer to account, 
         * see link for explanation and a motd to HTML snippet
         * https://gist.github.com/csh/2480d14fbbb33b4bbae3#gistcomment-2672658
         */
            var buffer = new byte[Int16.MaxValue];
            // var buffer = new byte[4096];
            _stream.Read(buffer, 0, buffer.Length);

            try
            {
                var length = ReadVarInt(buffer);
                var packet = ReadVarInt(buffer);
                var jsonLength = ReadVarInt(buffer);
                var json = ReadString(buffer, jsonLength);
                Console.WriteLine(json);
                latestPing = JsonConvert.DeserializeObject<PingPayload>(json);
                
            }
            catch (IOException ex)
            {
                /*
                 * If an IOException is thrown then the server didn't 
                 * send us a VarInt or sent us an invalid one.
                 */
                
            }
        }

        #region Read/Write methods
        internal byte ReadByte(byte[] buffer)
        {
            var b = buffer[_offset];
            _offset += 1;
            return b;
        }

        internal byte[] Read(byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, _offset, data, 0, length);
            _offset += length;
            return data;
        }

        internal int ReadVarInt(byte[] buffer)
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = ReadByte(buffer)) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5)
                {
                    throw new IOException("This VarInt is an imposter!");
                }
            }
            return value | ((b & 0x7F) << (size * 7));
        }

        internal string ReadString(byte[] buffer, int length)
        {
            var data = Read(buffer, length);
            return Encoding.UTF8.GetString(data);
        }

        internal void WriteVarInt(int value)
        {
            while ((value & 128) != 0)
            {
                _buffer.Add((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }
            _buffer.Add((byte)value);
        }

        internal void WriteShort(short value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        internal void WriteString(string data)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer.Length);
            _buffer.AddRange(buffer);
        }

        internal void Write(byte b)
        {
            _stream.WriteByte(b);
        }

        internal void Flush(int id = -1)
        {
            var buffer = _buffer.ToArray();
            _buffer.Clear();

            var add = 0;
            var packetData = new[] { (byte)0x00 };
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = _buffer.ToArray();
                add = packetData.Length;
                _buffer.Clear();
            }

            WriteVarInt(buffer.Length + add);
            var bufferLength = _buffer.ToArray();
            _buffer.Clear();

            _stream.Write(bufferLength, 0, bufferLength.Length);
            _stream.Write(packetData, 0, packetData.Length);
            _stream.Write(buffer, 0, buffer.Length);
        }
        #endregion
    }

    #region Server ping 
    public class DescriptionPayLoad
    {
        [JsonProperty(PropertyName = "text")]
        public string Motd { get; set; }
    }
    /// <summary>
    /// C# represenation of the following JSON file
    /// https://gist.github.com/thinkofdeath/6927216
    /// </summary>
    public class PingPayload
    {
        /// <summary>
        /// Protocol that the server is using and the given name
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public VersionPayload Version { get; set; }

        [JsonProperty(PropertyName = "players")]
        public PlayersPayload Players { get; set; }

        [JsonProperty(PropertyName = "description")]
        public DescriptionPayLoad Description { get; set; }

        /// <summary>
        /// Server icon, important to note that it's encoded in base 64
        /// </summary>
        [JsonProperty(PropertyName = "favicon")]
        public string Icon { get; set; }
    }

    public class VersionPayload
    {
        [JsonProperty(PropertyName = "protocol")]
        public int Protocol { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }

    public class PlayersPayload
    {
        [JsonProperty(PropertyName = "max")]
        public int Max { get; set; }

        [JsonProperty(PropertyName = "online")]
        public int Online { get; set; }

        [JsonProperty(PropertyName = "sample")]
        public List<Player> Sample { get; set; } = new List<Player>();
    }

    public class Player
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
    }
    #endregion
}
