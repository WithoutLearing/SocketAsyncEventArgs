using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace SocketAsyncEventArgs类
{
    class Program
    {

        static void Main(string[] args)
        {
            Server blServer = new Server(10, 1024);
            blServer.Init();

            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Any, 502);
            blServer.Start(iPEnd);
            

        }
    }
}
