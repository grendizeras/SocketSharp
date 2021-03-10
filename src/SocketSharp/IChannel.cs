using SocketSharp.Exceptions;
using System;
using System.Threading.Tasks;

namespace SocketSharp
{
    public interface IChannel:IDisposable
    {
        
        event Action<ReceiveContext> OnReceive;
        event Action<ConnectionException> OnConnectionException;
        int Send(byte[] payload);
        Task<int> SendAsync(byte[] payload);
        void Close();


    }
}
