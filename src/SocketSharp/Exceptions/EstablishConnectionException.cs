using System.Net.Sockets;

namespace SocketSharp.Exceptions
{
    public class EstablishConnectionException: ConnectionException
    {
        public EstablishConnectionException(SocketException ex):base("Error occured while trying to connect.",ex)
        {

        }
    }
}
