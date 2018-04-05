using System;
using System.Collections.Generic;
using System.Text;

namespace SocketSharp
{
    public class ReceiveContext
    {
        public byte[] Payload { get; internal set; }
        public string Rate { get; internal set; }
        public double ReceiveDuration { get; internal set; }
        public int Size => Payload.Length;
    }
}
