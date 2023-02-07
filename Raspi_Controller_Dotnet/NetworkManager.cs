using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using System.Security.Cryptography;

using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;


namespace Raspi_Controller_Dotnet
{
    public class NetworkManager
    {
        const string file = "network_data.json";
        static IPAddress AllHostsMulticast;
        public JsonObject JSON;
        static SHA256 Hash = SHA256.Create();
        private static EndPoint RemoteEndpoint;
        int delay;
        int ticksWaited = 0;

        Socket IcmpListener;
        byte[] buffer = new byte[4096];
        public NetworkManager()
        {
            return;
            delay = 60000 / Program.TickDelay; // Ping once every minute
            ticksWaited = delay;
            IcmpListener = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            IcmpListener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            byte[] bytes = GetDefaultGateway().GetAddressBytes();
            bytes[3] = 255;
            AllHostsMulticast = new IPAddress(bytes);
            if(File.Exists(file))
            {
                FileStream fs = File.OpenRead(file);
                try
                {
                    JSON = JsonNode.Parse(fs).AsObject();
                }
                catch
                {
                    SetupJson();
                }
                fs.Close();
            }
            else SetupJson();
            BeginReceive();
        }
        public void SetupJson()
        {
            JSON = new JsonObject
            {
                { "devices", new JsonObject() }
            };
        }
        public void Save()
        {
            FileStream fs = File.OpenWrite(file);
            JSON.WriteTo(new System.Text.Json.Utf8JsonWriter(fs));
            fs.Close();
        }
        public void BeginReceive()
        {
            RemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            IcmpListener.BeginReceiveFrom(buffer, 0, 4096, SocketFlags.None, ref RemoteEndpoint, ReceiveCallback, null);
        }
        private void ReceiveCallback(IAsyncResult ar)
        {
            IcmpListener.EndReceiveFrom(ar, ref RemoteEndpoint);
            BeginReceive();
            IPAddress addr = ((IPEndPoint)RemoteEndpoint).Address;
            string addrString = addr.ToString();
            Console.WriteLine($"Received a response from {addrString}");
            if (!HttpManager.IsPrivate(addr)) return;
            string hostname = Dns.GetHostEntry(addr).HostName;
            JsonObject node = JSON["devices"][hostname].AsObject();
            if (node == null)
            {
                node = new JsonObject()
                {
                    {"addresses", new JsonArray() { JsonValue.Create(addrString)}},
                    {"appearances", new JsonArray() {new JsonObject()
                    {
                        {"address", addrString.ToString() },
                        {"timestamp", DateTime.UtcNow.ToString() }
                    }
                    } }
                };
            }
            else
            {
                node["appearances"].AsArray().Add(new JsonObject()
                {
                    {"address", addrString },
                    {"timestamp", DateTime.UtcNow.ToString() }
                });
                JsonArray addresses = node["addresses"].AsArray();
                if (addresses[addresses.Count - 1].GetValue<string>() != addrString) addresses.Add(addrString);
            }
        }
        public void Update()
        {
            return;
            ticksWaited++;
            if(ticksWaited >= delay)
            {
                Ping();
                ticksWaited = 0;
            }
            
        }
        void Ping()
        {
            Console.WriteLine("Pinging");
            byte[] buffer = new byte[32];
            new System.Random().NextBytes(buffer);
            IcmpListener.SendTo(buffer, new IPEndPoint(AllHostsMulticast, 0));
        }
        public static IPAddress GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .Where(a => a != null)
                // .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                // .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                .FirstOrDefault();
        }
    }
}
