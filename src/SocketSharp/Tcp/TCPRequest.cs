
using System.Threading.Tasks;

namespace SocketSharp.Tcp
{
    public class TCPRequest : IRequestChannel
    {

        private readonly TCPConnection _channel;
        private TaskCompletionSource<ReceiveContext> _tcs;
        public TCPRequest(string ip, ushort port)
        {
            _channel = new TCPConnection(ip, port);
            _channel.OnReceive += OnReceive;
            _channel.OnConnectionException += OnException;
        }


        public TCPRequest(string address)
        {
            _channel = new TCPConnection(address);
            _channel.OnReceive += OnReceive;
            _channel.OnConnectionException += OnException;
        }
        private void OnException(Exceptions.ConnectionException obj)
        {
            if (_tcs.Task.Status == TaskStatus.WaitingForActivation)
                _tcs.SetException(obj);
        }

        private void OnReceive(ReceiveContext ctx)
        {
            if (_tcs.Task.Status == TaskStatus.WaitingForActivation)
                _tcs.SetResult(ctx);
        }

        public async Task<byte[]> RequestAsync(byte[] data)
        {
            if (!_channel.Open)
            {
                await _channel.ConnectAsync();
            }
            _tcs = new TaskCompletionSource<ReceiveContext>();
            await _channel.SendAsync(data);
            return (await _tcs.Task).Payload;

        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
