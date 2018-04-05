using SocketSharp.Tcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketSharp.Sample.Client
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Enter 'request' for request mode, else some other symbols");
            var mode = Console.ReadLine();

            if (mode == "request")
            {
                using(var request=new TCPRequest("127.0.0.1", 49999))
                {
                    while (true)
                    {
                        var message = Console.ReadLine();
                        SendRequest(request, message);//left without await intentionally
                        Console.WriteLine("Request sent; waiting for reply asynchronously");
                    }
                }
            }
            else
            {

                using (var con = new TCPConnection("127.0.0.1", 49999))
                {
                    con.OnConnected += OnConnected;
                    con.OnReceive += OnReceive;
                    con.OnConnectionException += OnConnectionException;
                    await con.ConnectAsync();
                    while (true)
                    {
                        var message = Console.ReadLine();
                        await con.SendAsync(Encoding.UTF8.GetBytes(message));
                    }
                }
            }

        }


        private static async Task SendRequest (IRequestChannel request,string message)
        {
            var response=await request.RequestAsync(Encoding.UTF8.GetBytes(message));
            Console.WriteLine(Encoding.UTF8.GetString(response));

        }
        private static void OnReceive(ReceiveContext ctx )
        {
            Console.WriteLine($"{Encoding.UTF8.GetString(ctx.Payload)}. Speed: {ctx.Rate}. Duration: {ctx.ReceiveDuration}");
        }

        private static void OnConnectionException(Exceptions.ConnectionException ex)
        {
            Console.WriteLine(ex.Message);
        }

        private static void OnConnected()
        {
            Console.WriteLine("Connected");
        }
    }
}
