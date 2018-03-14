using System.Net.Sockets;

namespace SocketSharp.Exceptions
{
    public class ReceiveMessageConnectionException: ConnectionException
    {
        public ReceiveMessageConnectionException(SocketException ex):base("Error occured while receiving packet", ex) { }
    }
}
