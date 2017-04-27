using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sitecore.Support.Modules.EmailCampaign.Core
{
    internal class ReadAheadBuffer<T> where T : class
    {
        // Fields
        private bool isProductionComplete;
        private int numActiveProducers;
        private int numRequestsSinceRecentPeakDecline;
        private int numWaitingConsumers;
        private int numWaitingConsumersPeak;
        private readonly int padding;
        private readonly Func<T> producer;
        private readonly Queue<Tuple<T, Exception>> queue;
        private readonly int skipPeakDeclineInterval;

        // Methods
        internal ReadAheadBuffer(Func<T> producer, int padding = 2, int skipPeakDeclineInterval = 5)
        {
            this.queue = new Queue<Tuple<T, Exception>>();
            Assert.ArgumentNotNull(producer, "producer");
            Assert.ArgumentCondition(padding >= 0, "padding", "A non-negative number was expected.");
            Assert.ArgumentCondition(skipPeakDeclineInterval >= 0, "skipPeakDeclineInterval", "A non-negative number was expected.");
            this.producer = producer;
            this.padding = padding;
            this.skipPeakDeclineInterval = skipPeakDeclineInterval;
        }

        private void EnsureProduction()
        {
            int num = 0;
            Queue<Tuple<T, Exception>> queue = this.queue;
            lock (queue)
            {
                if (!this.isProductionComplete)
                {
                    int num2 = this.numWaitingConsumersPeak + this.padding;
                    num = Math.Max(0, (num2 - this.queue.Count) - this.numActiveProducers);
                    this.numActiveProducers += num;
                }
            }
            for (int i = 0; i < num; i++)
            {
                Task.Factory.StartNew(new Action(this.ProducerMain));
            }
        }

        private void ProducerMain()
        {
            Tuple<T, Exception> tuple;
            try
            {
                T local = this.producer();
                tuple = (local == null) ? null : new Tuple<T, Exception>(local, null);
            }
            catch (Exception exception)
            {
                tuple = new Tuple<T, Exception>(default(T), exception);
            }
            Queue<Tuple<T, Exception>> queue = this.queue;
            lock (queue)
            {
                if (tuple == null)
                {
                    this.isProductionComplete = true;
                }
                else
                {
                    this.queue.Enqueue(tuple);
                }
                Monitor.Pulse(this.queue);
                this.numActiveProducers--;
            }
        }

        internal bool TryGetNextItem(out T item, out Exception exception)
        {
            Tuple<T, Exception> tuple = null;
            Queue<Tuple<T, Exception>> queue = this.queue;
            lock (queue)
            {
                while ((tuple == null) && ((!this.isProductionComplete || (this.queue.Count > 0)) || (this.numActiveProducers > 0)))
                {
                    if (this.queue.Count > 0)
                    {
                        tuple = this.queue.Dequeue();
                    }
                    else
                    {
                        this.numWaitingConsumers++;
                        if (this.numWaitingConsumers > this.numWaitingConsumersPeak)
                        {
                            this.numWaitingConsumersPeak = this.numWaitingConsumers;
                            this.numRequestsSinceRecentPeakDecline = 0;
                        }
                        this.EnsureProduction();
                        Monitor.Wait(this.queue);
                        this.numWaitingConsumers--;
                    }
                }
                if (tuple == null)
                {
                    Monitor.PulseAll(this.queue);
                }
                else
                {
                    this.EnsureProduction();
                    int num = this.numRequestsSinceRecentPeakDecline + 1;
                    this.numRequestsSinceRecentPeakDecline = num;
                    if (num >= this.skipPeakDeclineInterval)
                    {
                        this.numWaitingConsumersPeak = Math.Max(0, this.numWaitingConsumersPeak - 1);
                        this.numRequestsSinceRecentPeakDecline = 0;
                    }
                }
            }
            if (tuple == null)
            {
                item = default(T);
                exception = null;
                return false;
            }
            item = tuple.Item1;
            exception = tuple.Item2;
            return true;
        }
    }
}
