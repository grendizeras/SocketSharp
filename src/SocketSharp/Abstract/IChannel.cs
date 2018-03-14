using SocketSharp.Exceptions;
using System;
using System.Threading.Tasks;

namespace SocketSharp.Abstract
{
    public interface IChannel:IDisposable
    {
        bool Open { get;}
        
        event Action<byte[]> OnReceive;
        event Action<ConnectionException> OnConnectionException;
        int Send(byte[] payload);
        Task<int> SendAsync(byte[] payload);
        void Close();


    }
}
