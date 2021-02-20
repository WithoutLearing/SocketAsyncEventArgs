using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketAsyncEventArgs类
{
    /// <summary>
    /// 实现套接字服务器的连接逻辑
    /// 接受连接后，从客户端读取的所有数据发送回客户端。读取并回显到客户机模式,直到客户端断开连接
    /// </summary>
    class Server
    {
        private int m_numConnections;   //样本设计为同时处理的最大连接数
        private int m_receiveBufferSize;//用于每个套接字I/O操作的缓冲区大小
        BufferManager m_bufferManager;  // 表示用于所有套接字操作的大型可重用缓冲区集
        const int opsToPreAlloc = 2;    // 读、写（不为接受分配缓冲区空间）
        Socket listenSocket;            // 用于侦听传入连接请求的套接字
        SocketAsyncEventArgsPool m_readWritePool;// 用于写、读和接受套接字操作的可重用SocketAsyncEventArgs对象池
        int m_totalBytesRead;           // 服务器接收的总字节数的计数器
        int m_numConnectedSockets;      // 连接到服务器的客户端总数
        Semaphore m_maxNumberAcceptedClients;

        /// <summary>
        /// 创建未初始化的服务器实例
        /// 启动服务器侦听连接请求,先调用Init方法，然后再调用Start方法
        /// </summary>
        /// <param name="numConnections">样本设计为同时处理的最大连接数</param>
        /// <param name="receiveBufferSize">用于每个套接字I/O操作的缓冲区大小</param>
        public Server(int numConnections, int receiveBufferSize)
        {
            m_totalBytesRead = 0;
            m_numConnectedSockets = 0;
            m_numConnections = numConnections;
            m_receiveBufferSize = receiveBufferSize;
            // 分配缓冲区，以便最大数量的套接字可以有一个未完成的读和写操作同时写入到套接字
            m_bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToPreAlloc,
                receiveBufferSize);

            m_readWritePool = new SocketAsyncEventArgsPool(numConnections);
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
        }

        //通过预分配可重用缓冲区和上下文对象。
        //这些对象不需要预先分配或者重用，但这样做是为了说明API如何,易于用于创建可重用对象以提高服务器性能。
        public void Init()
        {
            // 分配一个大字节缓冲区，所有I/O操作都使用一个缓冲区.  这有助于防止记忆碎片
            m_bufferManager.InitBuffer();

            // 预先分配SocketAsyncEventArgs对象池
            SocketAsyncEventArgs readWriteEventArg;

            for (int i = 0; i < m_numConnections; i++)
            {
                //预先分配一组可重用的SocketAsyncEventArgs
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                readWriteEventArg.UserToken = new AsyncUserToken();

                // 将缓冲池中的字节缓冲区分配给SocketAsyncEventArg对象
                m_bufferManager.SetBuffer(readWriteEventArg);

                // 将SocketAsyncEventArg添加到池中
                m_readWritePool.Push(readWriteEventArg);
            }
        }

        /// <summary>
        /// 启动服务器，使其侦听传入的连接请求。
        /// </summary>
        /// <param name="localEndPoint">服务器将在其上侦听连接请求的终结点</param>
        public void Start(IPEndPoint localEndPoint)
        {
            //创建侦听传入连接的套接字
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            //用100个连接启动服务器
            listenSocket.Listen(100);

            // 监听插座上的post接收
            StartAccept(null);

            //Console.WriteLine("{0} connected sockets with one outstanding receive posted to each....press any key", m_outstandingReadCount);
            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }

        /// <summary>
        /// 开始接受来自客户端的连接请求的操作
        /// </summary>
        /// <param name="acceptEventArg">在服务器的侦听套接字上发出接受操作时要使用的上下文对象</param>
        public void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }
            else
            {
                // 必须清除套接字，因为正在重用上下文对象
                acceptEventArg.AcceptSocket = null;
            }

            m_maxNumberAcceptedClients.WaitOne();
            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// 此方法是与关联的回调方法套接字.sync操作，并在接受操作完成时调用
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref m_numConnectedSockets);
            Console.WriteLine("Client connection accepted. There are {0} clients connected to the server",
                m_numConnectedSockets);

            //获取接受的客户端连接的套接字，并将其放入ReadEventArg对象用户令牌中
            SocketAsyncEventArgs readEventArgs = m_readWritePool.Pop();
            ((AsyncUserToken)readEventArgs.UserToken).Socket = e.AcceptSocket;

            // 客户机连接后，立即向连接发送接收
            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArgs);
            if (!willRaiseEvent)
            {
                ProcessReceive(readEventArgs);
            }

            // 接受下一个连接请求
            StartAccept(e);
        }

        /// <summary>
        /// 每当套接字上的接收或发送操作完成时，就会调用此方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">与已完成的接收操作关联的SocketAsyncEventArg</param>
        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            // 确定刚刚完成的操作类型并调用关联的处理程序
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// 此方法在异步接收操作完成时调用
        /// 如果远程主机关闭了连接，则套接字将关闭。
        /// 如果接收到数据，则将数据回显到客户端。
        /// </summary>
        /// <param name="e"></param>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            // 检查远程主机是否关闭了连接
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //增加服务器接收的总字节数
                Interlocked.Add(ref m_totalBytesRead, e.BytesTransferred);
                Console.WriteLine("The server has read a total of {0} bytes", m_totalBytesRead);

                //将接收到的数据回显到客户端
                e.SetBuffer(e.Offset, e.BytesTransferred);
                bool willRaiseEvent = token.Socket.SendAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessSend(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        /// <summary>
        /// 此方法在异步发送操作完成时调用。
        /// 该方法在套接字上发出另一个receive，以读取从客户端发送的任何附加数据
        /// </summary>
        /// <param name="e"></param>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // 已将数据回显到客户端
                AsyncUserToken token = (AsyncUserToken)e.UserToken;
                // 读取从客户端发送的下一个数据块
                bool willRaiseEvent = token.Socket.ReceiveAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessReceive(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;

            // 关闭与客户端关联的套接字
            try
            {
                token.Socket.Shutdown(SocketShutdown.Send);
            }
            //如果客户端进程已关闭，则引发
            catch (Exception)
            {

            }
            token.Socket.Close();

            // 递减计数器以跟踪连接到服务器的客户端总数
            Interlocked.Decrement(ref m_numConnectedSockets);

            // 释放SocketAsyncEventArg，以便其他客户端可以重用它们
            m_readWritePool.Push(e);

            m_maxNumberAcceptedClients.Release();
            Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", m_numConnectedSockets);
        }


















    }
}
