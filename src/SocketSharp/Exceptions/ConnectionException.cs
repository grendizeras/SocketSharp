using System;

namespace SocketSharp.Exceptions
{
    public abstract class ConnectionException:Exception
    {
     public ConnectionException(string message, Exception inner) : base(message, inner) { }
    }
}
