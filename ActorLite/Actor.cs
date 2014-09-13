using System;
using System.Collections.Generic;

namespace ActorLite
{
    public interface IPort<out T>
    {
        void Post(Action<T> message);
    }

    public abstract class Actor<T> : IActor, IPort<T>
        where T : Actor<T>
    {
        protected virtual void Receive(Action<T> message)
        {
            message.DynamicInvoke(this);
        }

        protected Actor()
        {
            this.m_context = new ActorContext(this);
        }

        private ActorContext m_context = null;
        ActorContext IActor.Context
        {
            get
            {
                return this.m_context;
            }
        }

        bool IActor.Exited
        {
            get
            {
                return this.m_exited;
            }
        }

        int IActor.MessageCount
        {
            get
            {
                return this.m_messageQueue.Count;
            }
        }

        void IActor.Execute()
        {
            Action<T> message;
            lock (this.m_messageQueue)
            {
                message = this.m_messageQueue.Dequeue();
            }

            this.Receive(message);
        }

        private bool m_exited = false;
        private Queue<Action<T>> m_messageQueue = new Queue<Action<T>>();

        protected void Exit()
        {
            this.m_exited = true;
        }

        public void Post(Action<T> message)
        {
            if (this.m_exited) return;

            lock (this.m_messageQueue)
            {
                this.m_messageQueue.Enqueue(message);
            }

            Dispatcher.Instance.ReadyToExecute(this);

            //message.DynamicInvoke(this);
        }
    }
}
