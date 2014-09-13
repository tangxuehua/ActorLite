using System;
using ActorLite;

namespace CsActor
{
    public interface ICountRequestHandler
    {
        void Count(IPort<ICountResponseHandler> collector, int baseValue, int value);
    }
    public interface ICountResponseHandler
    {
        void OutCurrentTotal(IPort<ICountRequestHandler> counter, int newValue);
    }

    public class Counter : Actor<Counter>, ICountRequestHandler
    {
        void ICountRequestHandler.Count(IPort<ICountResponseHandler> collector, int baseValue, int value)
        {
            collector.Post(c => c.OutCurrentTotal(this, baseValue + value));
        }
    }

    public class Calculator : Actor<Calculator>, ICountResponseHandler
    {
        private int currentValue = 0;
        private int maxValue = 0;
        private int index = 0;
        IPort<ICountRequestHandler> counter;

        void ICountResponseHandler.OutCurrentTotal(IPort<ICountRequestHandler> counter, int newValue)
        {
            this.currentValue = newValue;
            this.index++;
            if (this.index <= this.maxValue)
            {
                counter.Post(ct => ct.Count(this, currentValue, index));
            }
            else
            {
                Console.WriteLine("CurrentTotal: {0}", newValue);
            }
        }

        public void StartCalculate(int maxValue)
        {
            this.maxValue = maxValue;
            counter = new Counter();
            counter.Post(ct => ct.Count(this, currentValue, index));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Calculator aa = new Calculator();
            aa.Post(c => c.StartCalculate(10000));
            Console.ReadLine();
        }
    }
}
