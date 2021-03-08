
using SocketSharp.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SocketSharp.Tcp
{
    public class TCPServer : IHost
    {
        Socket _listenerSocket;
        List<IChannel> _connections = new List<IChannel>();
        public Socket UnderlyingSocket => _listenerSocket;
        public IEnumerable<IChannel> Connections => _connections;

        public event Action<IChannel> OnInboundConnection;

        public int MaxIncomingConnections => 10;

        public TCPServer()
        {
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start(ushort port)
        {
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            _listenerSocket.Listen(MaxIncomingConnections);
            BeginAccept();
        }

        private void BeginAccept()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += OnConnected;
            _listenerSocket.AcceptAsync(e);
        }

        private void OnConnected(object sender, SocketAsyncEventArgs e)
        {
            var connected = new TCPConnection(e.AcceptSocket);
            connected.OnConnectionException += (ex) =>
            {
                if (ex is ReceiveMessageConnectionException)
                {
                    connected.Dispose();
                    _connections.Remove(connected);
                }
            };
            _connections.Add(connected);
            try
            {
                OnInboundConnection?.Invoke(connected);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            BeginAccept();
        }

        public void Dispose()
        {
            _listenerSocket.Dispose();
        }


    }
}
