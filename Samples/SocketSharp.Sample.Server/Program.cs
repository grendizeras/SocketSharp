using SocketSharp.Tcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketSharp.Sample.Server
{
    class Program
    {
        public static async Task Main(string[] args)
        {

            using (var server = new TCPServer())
            {
                server.OnInboundConnection += OnInboundConnection;

                server.Start(49999);


                while (true)
                {
                    var message = Console.ReadLine();
                    foreach (var con in server.Connections)
                    {                        
                        await con.SendAsync(Encoding.UTF8.GetBytes(message));
                    }
                }
            }


        }

        private static void OnInboundConnection(Abstract.IChannel channel)
        {
            channel.OnReceive += OnChannelReceive;
            channel.OnConnectionException += OnChannelConnectionException;
        }

        private static void OnChannelConnectionException(Exceptions.ConnectionException ex)
        {
            Console.WriteLine(ex.Message);
        }

        private static void OnChannelReceive(byte[] data)
        {
            Console.WriteLine(Encoding.UTF8.GetString(data));
        }
    }
}
