using System;
using System.Threading.Tasks;

namespace SocketSharp
{
    public interface IConnectable
    {
        bool Connected { get;}

        event Action OnConnected;
        void Connect();
        Task ConnectAsync();
    }
}
