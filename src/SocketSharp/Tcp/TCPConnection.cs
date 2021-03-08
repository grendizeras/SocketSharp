using SocketSharp.Exceptions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SocketSharp.Tcp
{
    /// <summary>
    /// TCP implementation of <see cref="IChannel"/>
    /// </summary>
    public class TCPConnection : IChannel, IConnectable
    {
        private Socket _socket;
        private string _ip;
        private ushort _port = 80;
        private bool _open;
        private CancellationToken _cancellationToken = new CancellationToken();

        /// <summary>
        /// Event is fired, when connection succeedes (Also on each successful reconnect).
        /// </summary>
        public event Action OnConnected;
        /// <summary>
        /// Event is fired when full message is received.
        /// </summary>
        public event Action<ReceiveContext> OnReceive;
        /// <summary>
        /// Event is fired when connection exception occures (Also on each unsuccessful reconnect).
        /// </summary>
        public event Action<ConnectionException> OnConnectionException;


        public bool Open => _open;
        /// <summary>
        /// Specifies how many times should try to reconnect, before exception is thrown.
        /// </summary>
        public int ReconnectTryCount { get; set; } = 2;
        public string Ip => _ip;
        public ushort Port => _port;
        public int ReceiveTimeout { get; set; } = 20000;
        public Socket UnderlyingSocket => _socket;



        public TCPConnection(string ip, ushort port)
        {
            _ip = ip;
            _port = port;
        }
        /// <summary>
        /// This constructor override tries to extract ip address from dns address.
        /// </summary>
        /// <param name="address"></param>
        public TCPConnection(string address)
        {
            var addressSplit = address.Split(':');
            _ip = Dns.GetHostEntry(addressSplit[0]).AddressList[0].MapToIPv4().ToString();
            if (addressSplit.Count() > 1)
            {
                _port = ushort.Parse(addressSplit[1]);
            }
        }

        internal TCPConnection(Socket socket)
        {
            _open = true;
            _socket = socket;
            var remoteEndpoint = socket.RemoteEndPoint as IPEndPoint;
            if (remoteEndpoint != null)
            {
                _ip = remoteEndpoint.Address.ToString();
                _port = (ushort)remoteEndpoint.Port;
            }
            ReceiveLoop();
        }

        public void Close()
        {
            _open = false;
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Dispose();
            }
            catch { }
        }
        /// <summary>
        /// Connect to remote server. Retries to connect <see cref="ReconnectTryCount"/> times if server is unavailable. (Implementation of <see cref="IConnectable"/>)
        /// </summary>
        public void Connect()
        {
            if (Open)
                return;
            try
            {
                ConnectInternal();
                OnConnected?.Invoke();
            }
            catch (SocketException ex)
            {
                _open = false;
                OnConnectionException?.Invoke(new EstablishConnectionException(ex));
            }
        }
        /// <summary>
        /// Connect to remote server async. Retries to connect <see cref="ReconnectTryCount"/> times if server is unavailable. (Implementation of <see cref="IConnectable"/>)
        /// </summary>
        public async Task ConnectAsync()
        {
            if (Open)
                return;
            try
            {
                await ConnectInternalAsync();
            }
            catch (SocketException ex)
            {
                _open = false;
                OnConnectionException?.Invoke(new EstablishConnectionException(ex));
            }
        }


        /// <summary>
        /// Send data to remote host. If connection broke, tries to <see cref="ConnectInternal"/> and resend.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns>Data legth, that was sent. -1, if error.</returns>
        public int Send(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return 0;
            }//Empty payload will cause remote connection to close


            //Async wrapper hack to not copy logic
            return RetrySendAction(async () =>
            {
                int sent = 0;
                if (_open)//socket may be disposed by receive loop
                {
                    var envelope = WrapPayloadToEnvelope(payload);
                    sent = _socket.Send(envelope);
                    return await Task.FromResult(sent);
                }

                Connect();
                sent = Send(payload);
                return await Task.FromResult(sent); ;
            }, ConnectInternal).Result;

        }



        /// <summary>
        /// Send data to remote host. If connection broke, tries to <see cref="ConnectInternalAsync"/> and resend.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns>Data legth, that was sent. -1, if error.</returns>
        public async Task<int> SendAsync(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return 0;

            return await RetrySendAction(async () =>
            {
                if (_open)
                {
                    var envelope = WrapPayloadToEnvelope(payload);
                    return await new TaskFactory(_cancellationToken)
                        .FromAsync(_socket.BeginSend(envelope, 0, envelope.Length, SocketFlags.None, SentAsync, null), _socket.EndSend);
                }
                await ConnectAsync();
                return await SendAsync(payload);
            }, ConnectInternalAsync);

        }
        public void Dispose()
        {
            Close();
        }



        #region private 

        private async Task<int> RetrySendAction(Func<Task<int>> sendAction, Func<Task> connectAction)
        {
            try
            {
                return await sendAction();
            }
            catch (SocketException ex)
            {

                try
                {
                    Close();
                    await connectAction();
                    Thread.Sleep(100);
                    return await sendAction();
                }
                catch (SocketException sex)
                {
                    OnConnectionException?.Invoke(new SendMessageConnectionException(ex));
                }
            }
            return -1;
        }

        private byte[] WrapPayloadToEnvelope(byte[] payload)
        {
            var payloadLength = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(payloadLength);
            var envelope = new byte[payload.Length + 4];
            Array.Copy(payloadLength, envelope, 4);
            Array.Copy(payload, 0, envelope, 4, payload.Length);
            return envelope;
        }


        private Socket CreateSocket(IPAddress address)
        {

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = ReceiveTimeout;
            return socket;
        }

        private Task ConnectInternal()
        {
            var address = IPAddress.Parse(_ip);
            return RetryConnectAction(() =>
            {
                _socket = CreateSocket(address);
                _socket.ReceiveBufferSize = int.MaxValue;
                _socket.Connect(address, _port);
                ReceiveLoop();
                _open = true;
                return Task.CompletedTask;
            });

        }

        private async Task ConnectInternalAsync()
        {
            var address = IPAddress.Parse(_ip);
            await RetryConnectAction(async () =>
           {
               _socket = CreateSocket(address);
               await new TaskFactory(_cancellationToken)
              .FromAsync(_socket.BeginConnect(address, _port, ConnectedAsync, null), _socket.EndConnect);
           });
        }

        private async Task RetryConnectAction(Func<Task> action, int triedCount = 0)
        {

            try
            {
                if (_socket != null)
                {
                    Close();
                }
                await action();
            }
            catch (SocketException ex)
            {
                if (triedCount <= ReconnectTryCount)
                {
                    Thread.Sleep(1000);
                    await RetryConnectAction(action, ++triedCount);
                }
                else throw;
            }
        }



        private void SentAsync(IAsyncResult result)
        {
            if (result.IsCompleted)
            {

            }
        }


        private void ConnectedAsync(IAsyncResult result)
        {
            _open = true;
            ReceiveLoop();
        }


        private void ReceiveLoop(int size = 4, bool header = true)
        {
            var e = new SocketAsyncEventArgs()
            {
                UserToken = new EventArgsToken
                {
                    ReadHeader = header,
                    ChunkTimestamp = DateTime.UtcNow.Ticks
                },//notifies receiver handler, that it he should process message header, and call ReceiveLoop with extracted message size
                DisconnectReuseSocket = false
            };
            e.Completed += ReceivePacket;
            e.SetBuffer(new byte[size], 0, size);
            if (!_socket.ReceiveAsync(e))
            {
                ReceivePacket(this, e);
            }
        }


        private void ReceivePacket(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred == 0)
            {
                Close();
                OnConnectionException?.Invoke(new ReceiveMessageConnectionException(new SocketException()));
                return;
            }



            var token = e.UserToken as EventArgsToken;

            token.SetRate(e.BytesTransferred);

            var totalReceived = e.Offset + e.BytesTransferred;


            if (totalReceived >= e.Buffer.Length)
            {
                if (token.ReadHeader)
                {//extract message size from envelope header and start receiving actual payload
                    var buffer = e.Buffer;
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(buffer);
                    var messageLength = BitConverter.ToInt32(buffer, 0);
                    ReceiveLoop(messageLength, false);
                }
                else
                {
                    try
                    {
                        //whole payload was received, pass it to handler
                        OnReceive?.Invoke(new ReceiveContext
                        {
                            Payload = e.Buffer,
                            Rate = token.Rate,
                            ReceiveDuration = token.ReceiveDuration
                        });
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.ToString());
                    }
                    //restart loop for new messages
                    ReceiveLoop();
                }
            }
            else
            {//didn't get full message, continue receiving

                token.ChunkTimestamp = DateTime.UtcNow.Ticks;
                e.UserToken = token;
                e.SetBuffer(e.Buffer, totalReceived, e.Buffer.Length - totalReceived);
                _socket.ReceiveAsync(e);
            }
        }




        #endregion


        public override string ToString()
        {
            return $"{Ip}:{Port}";
        }



        private class EventArgsToken
        {

            private double _rate;
            private long _packetTimestamp;
            public bool ReadHeader { get; set; }
            public long ChunkTimestamp { get; set; }
            public double ReceiveDuration => new TimeSpan(DateTime.UtcNow.Ticks - _packetTimestamp).TotalSeconds;
            public string Rate
            {
                get
                {
                    var rate = _rate / 1048576;
                    return $"{Math.Round(rate, 2)} {(rate > 1 ? "mb/s" : "kb/s")}";
                }
            }


            public EventArgsToken()
            {
                _packetTimestamp = DateTime.UtcNow.Ticks;
            }

            internal void SetRate(int bytesTransferred)
            {
                var seconds = new TimeSpan(DateTime.UtcNow.Ticks - this.ChunkTimestamp).TotalSeconds;

                if (this.ChunkTimestamp != 0 && seconds > 0)
                {
                    var rate = (bytesTransferred / seconds);
                    this._rate = (rate + this._rate) / 2;
                }
            }


        }

    }
}
