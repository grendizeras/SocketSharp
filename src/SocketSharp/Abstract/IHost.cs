using System;
using System.Collections.Generic;

namespace SocketSharp.Abstract
{
    public interface IHost: IDisposable
    {
        IEnumerable<IChannel> Connections { get; }
        event Action<IChannel> OnInboundConnection;
        void Start(ushort port);
    }
}
