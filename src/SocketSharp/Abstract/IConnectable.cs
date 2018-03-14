using System;
using System.Threading.Tasks;

namespace SocketSharp.Abstract
{
    public interface IConnectable
    {
        event Action OnConnected;
        void Connect();
        Task ConnectAsync();
    }
}
