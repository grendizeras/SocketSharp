using System;
using System.Threading.Tasks;

namespace SocketSharp.Abstract
{
    public interface IRequestChannel:IDisposable
    {
        Task<byte[]> RequestAsync(byte[] data);
    }
}
