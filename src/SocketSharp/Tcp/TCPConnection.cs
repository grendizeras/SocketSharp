using SocketSharp.Abstract;
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
    public class TCPConnection : IChannel, IConnectable
    {
        private Socket _socket;
        private string _ip;
        private ushort _port;
        private bool _open;
        private byte[] _receiveBuffer;
        private int _totalReceived;
        private object _lockObject;
        private CancellationToken _cancellationToken = new CancellationToken();

        public event Action OnConnected;
        public event Action<byte[]> OnReceive;
        public event Action<ConnectionException> OnConnectionException;


        public bool Open => _open;
        public int ReconnectTryCount { get; set; } = 20;
        public string Ip => _ip;
        public ushort Port => _port = 80;
        public int ReceiveTimeout { get; set; } = 20000;



        public TCPConnection(string ip, ushort port)
        {
            _ip = ip;
            _port = port;
        }

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

        public void Connect()
        {
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
        public async Task ConnectAsync()
        {
            var address = IPAddress.Parse(_ip);
            _socket = CreateSocket(address);
            await new TaskFactory(_cancellationToken)
                .FromAsync(_socket.BeginConnect(address, _port, ConnectedAsync, null), _socket.EndConnect);
        }

        public int Send(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return 0;
            }//Empty payload will cause remote connection to close

            try
            {

                var envelope = WrapPayloadToEnvelope(payload);
                if (_open)//socket may be disposed by receive loop
                    return _socket.Send(envelope);
                else
                    throw new SocketException();

            }
            catch (SocketException ex)
            {

                try
                {
                    Close();
                    ConnectInternal();
                    Thread.Sleep(100);
                    Send(payload);
                }
                catch (SocketException sex)
                {
                    OnConnectionException?.Invoke(new SendMessageConnectionException(ex));
                }
                return -1;
            }
        }

        public async Task<int> SendAsync(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return 0;
            if (_open)
            {
                var envelope = WrapPayloadToEnvelope(payload);
                return await new TaskFactory(_cancellationToken)
                    .FromAsync(_socket.BeginSend(envelope, 0, envelope.Length, SocketFlags.None, SentAsync, null), _socket.EndSend);
            }
            await ConnectAsync();
            return await SendAsync(payload);
        }
        public void Dispose()
        {
            Close();
        }



        #region private 

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
        private void ConnectInternal(int triedCount = 0)
        {
            try
            {
                if (_socket != null)
                {
                    Close();
                }
                var address = IPAddress.Parse(_ip);
                _socket = CreateSocket(address);
                _socket.Connect(address, _port);
                ReceiveLoop();
                _open = true;
            }
            catch (SocketException ex)
            {
                if (triedCount <= ReconnectTryCount)
                {
                    Thread.Sleep(1000);
                    ConnectInternal(++triedCount);
                }
                else throw;
            }

        }


        
        private void SentAsync(IAsyncResult result) {
            if (result.IsCompleted)
            {

            }
        }

      
       private void ConnectedAsync(IAsyncResult result)
        {
            _open = true;
            ReceiveLoop();
        }

        

        private T RetryConnectionAction<T>(Func<T> action, int retriedCount = 0)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                if (retriedCount <= ReconnectTryCount)
                {
                    Thread.Sleep(1000);
                    ConnectInternal();
                    return RetryConnectionAction(action, ++retriedCount);
                }
                else
                    throw;
            }
        }


        private void ReceiveLoop(int size = 4, bool header = true)
        {
            var e = new SocketAsyncEventArgs()
            {
                UserToken = header,//notifies receiver handler, that it he should process message header, and call ReceiveLoop with extracted message size
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
            var totalReceived = e.Offset + e.BytesTransferred;
            if (totalReceived >= e.Buffer.Length)
            {
                if ((bool)e.UserToken)
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
                        OnReceive?.Invoke(e.Buffer);
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
                _socket.ReceiveAsync(e);
            }
        }




        #endregion


        public override string ToString()
        {
            return $"{Ip}:{Port}";
        }

      
    }
}
