using System.Net.Sockets;

namespace SocketSharp.Exceptions
{
    public class SendMessageConnectionException: ConnectionException
    {
        public SendMessageConnectionException(SocketException ex):base("error occured while sending message", ex) { }
    }
}
