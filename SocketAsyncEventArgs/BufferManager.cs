using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketAsyncEventArgs类
{
    class BufferManager
    {
        int m_numBytes;                 // 缓冲池控制的字节总数
        byte[] m_buffer;                // 由缓冲区管理器维护的底层字节数组
        Stack<int> m_freeIndexPool;     
        int m_currentIndex;
        int m_bufferSize;

        public BufferManager(int totalBytes, int bufferSize)
        {
            m_numBytes = totalBytes;
            m_currentIndex = 0;
            m_bufferSize = bufferSize;
            m_freeIndexPool = new Stack<int>();
        }

        public void InitBuffer()
        {
            // 创建一个大的缓冲区并将其分割
            //输出到每个SocketAsyncEventArg对象
            m_buffer = new byte[m_numBytes];
        }

        /// <summary>
        /// 将缓冲池中的缓冲区分配给指定的SocketAsyncEventArgs对象
        /// </summary>
        /// <param name="args"></param>
        /// <returns>如果缓冲区设置成功，则为true，否则为false</returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {

            if (m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if ((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }
            return true;
        }

        //从SocketAsyncEventArg对象中删除缓冲区
        //这会将缓冲区释放回缓冲池
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
