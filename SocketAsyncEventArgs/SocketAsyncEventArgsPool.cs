using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;


namespace SocketAsyncEventArgs类
{
    class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> m_pool;

        /// <summary>
        /// 将对象池初始化为指定大小
        /// </summary>
        /// <param name="capacity">SocketAsyncEventArgs池可以容纳的对象</param>
        public SocketAsyncEventArgsPool(int capacity)
        {
            m_pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        /// <summary>
        /// 将SocketAsyncEventArg实例添加到池中
        /// </summary>
        /// <param name="item">SocketAsyncEventArgs实例添加到池中</param>
        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            }
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        /// <summary>
        /// 从池中删除SocketAsyncEventArgs实例,并返回从池中移除的对象
        /// </summary>
        /// <returns></returns>
        public SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
                return m_pool.Pop();
            }
        }

        //池中SocketAsyncEventArgs实例数
        public int Count
        {
            get { return m_pool.Count; }
        }
    }
}
