using System;
using System.Threading.Tasks;

namespace SocketSharp
{
    public interface IConnectable
    {
        event Action OnConnected;
        void Connect();
        Task ConnectAsync();
    }
}
