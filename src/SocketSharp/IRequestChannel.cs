using System;
using System.Threading.Tasks;

namespace SocketSharp
{
    public interface IRequestChannel:IDisposable
    {
        Task<byte[]> RequestAsync(byte[] data);
    }
}
