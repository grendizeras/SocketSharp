# SocketSharp
SocketSharp is wrapper library for <a href="https://msdn.microsoft.com/en-us/library/system.net.sockets.socket(v=vs.110).aspx">System.Net.Sockets.Socket</a>.
It provides abstraction layer over Socket class, so underlying implementation could be any type of connection (TCP, UDP etc.)

### It has following advantages:
* `Fast             - It leverages from socket's native asynchronous api, which makes duplex communication fast with minimum overhead from wrapper class.`
* `Easy to Use      - Sockets abstracted with IChannel interface, with clear-to-understand api.`
* `Extensible       - Abstraction makes it possible to use any type of connection by switching the implementaion of IChannel interface.`
* `Request/Response - Use this pattern, if you want to receive response immediately. Just await async request.`
* `Robust - Retries to connect, if connection was shut down unexpectedly. If connection broke while sending message, it will try to reconnect and resend message.`

# Installation
Package is available in <a href="https://www.nuget.org/packages/SocketSharp/">NuGet</a> repository: 
```
PM> Install-Package SocketSharp -Version 1.0.0
```
# How to use


## Server

```c#
//Create server object.
var server = new TCPServer();

 //set event handler on inbound connection. Input parameter is IChannel
server.OnInboundConnection += channel=> 
{
   channel.OnReceive += OnChannelReceive;
   channel.OnConnectionException += OnChannelConnectionException;
 };

//Start the server (not blocking)
server.Start(49999);
```


## Client
```c#
 using (var con = new TCPConnection("127.0.0.1", 49999))
 {
     con.OnConnected += OnConnected;
     // Set handler to receive messages asynchronously.
     con.OnReceive += OnReceive;
     con.OnConnectionException += OnConnectionException;
     //Connect to the server
     await con.ConnectAsync();
     //send message
     await con.SendAsync(Encoding.UTF8.GetBytes(message));
     Console.Read();
 }
 
 ...
 
  private static void OnReceive(byte[] data)
  {
      Console.WriteLine(Encoding.UTF8.GetString(data));
  }
```
There is possibility to make remote calls using Request/Response pattern over sockets by accessing TCPRequest's RequestAsync(byte[] data) method:
```c#
 using (var request = new TCPRequest("127.0.0.1", 49999))
 {                
      var message = Console.ReadLine();
      byte[] response=await request.RequestAsync(Encoding.UTF8.GetBytes(message));
 }
```

## Note
Only TCP implementation is available until now.

