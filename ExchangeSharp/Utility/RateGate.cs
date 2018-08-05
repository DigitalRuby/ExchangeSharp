/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Allows limiting operations over an interval - no more than n operations will exit in the interval specified
    /// </summary>
    public class RateGate
    {
        // Semaphore used to count and limit the number of occurrences per unit time.
        private readonly SemaphoreSlim semaphore;

        // Times (in ticks, where 1 tick = 10000 milliseconds) at which the semaphore should be exited.
        private readonly ConcurrentQueue<TimeSpan> exitTimes = new ConcurrentQueue<TimeSpan>();

        // Timer used to trigger exiting the semaphore.
        private readonly Timer timer;

        // Track exit time
        private readonly Stopwatch exitTimer = Stopwatch.StartNew();

        // No period for timer
        private readonly TimeSpan negativeOne = TimeSpan.FromMilliseconds(-1.0);

        // Whether this instance is disposed.
        private bool isDisposed;

        private TimeSpan CurrentExitTimer()
        {
            lock (exitTimer)
            {
                return exitTimer.Elapsed;
            }
        }

        /// <summary>
        /// Callback for the exit timer that exits the semaphore based on exit times in the queue and then sets the timer for the nextexit time.
        /// </summary>
        /// <param name="state">State</param>
        private void ExitTimerCallback(object state)
        {
            // While there are exit times that are passed due still in the queue, exit the semaphore and dequeue the exit time.
            TimeSpan exitTime;
            while (exitTimes.TryPeek(out exitTime) && (exitTime - CurrentExitTimer()).Ticks <= 0)
            {
                semaphore.Release();
                exitTimes.TryDequeue(out exitTime);
            }

            // Try to get the next exit time from the queue and compute the time until the next check should take place. If the 
            // queue is empty, then no exit times will occur until at least one time unit has passed.
            TimeSpan timeUntilNextCheck;
            if (exitTimes.TryPeek(out exitTime))
            {
                timeUntilNextCheck = (exitTime - CurrentExitTimer());

                // ensure the next time check is within the time unit
                if (timeUntilNextCheck.Ticks < 1)
                {
                    timeUntilNextCheck = TimeSpan.FromTicks(1);
                }
                else if (timeUntilNextCheck > TimeUnit)
                {
                    timeUntilNextCheck = TimeUnit;
                }
            }
            else
            {
                timeUntilNextCheck = TimeUnit;
            }

            // Set the timer in milliseconds
            timer.Change(timeUntilNextCheck, negativeOne);
        }

        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException("RateGate is already disposed");
            }
        }

        /// <summary>
        /// Releases unmanaged resources held by an instance of this class.
        /// </summary>
        /// <param name="isDisposing">Whether this object is being disposed.</param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (!isDisposed && isDisposing)
            {
                // The semaphore and timer both implement IDisposable and therefore must be disposed.
                semaphore.Dispose();
                timer.Dispose();
                isDisposed = true;
            }
        }

        /// <summary>
        /// Initializes a <see cref="RateGate"/> with a rate of <paramref name="occurrences"/> 
        /// per <paramref name="timeUnit"/>.
        /// </summary>
        /// <param name="occurrences">Number of occurrences allowed per unit of time.</param>
        /// <param name="timeUnit">Length of the time unit.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="occurrences"/> or <paramref name="timeUnit"/> is negative.
        /// </exception>
        public RateGate(int occurrences, TimeSpan timeUnit)
        {
            // Check the arguments.
            if (occurrences <= 0)
            {
                throw new ArgumentOutOfRangeException("occurrences", "Number of occurrences must be a positive integer");
            }
            if (timeUnit != timeUnit.Duration())
            {
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be a positive span of time");
            }
            if (timeUnit >= TimeSpan.FromMilliseconds(UInt32.MaxValue))
            {
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be less than 2^32 milliseconds");
            }

            Occurrences = occurrences;
            TimeUnit = timeUnit;

            // Create the semaphore, with the number of occurrences as the maximum count.
            semaphore = new SemaphoreSlim(Occurrences, Occurrences);

            // Create a timer to exit the semaphore. Use the time unit as the original
            // interval length because that's the earliest we will need to exit the semaphore.
            timer = new System.Threading.Timer(ExitTimerCallback, null, TimeUnit, negativeOne);
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="millisecondsTimeout">Number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public async Task<bool> WaitToProceedAsync(int millisecondsTimeout)
        {
            await new SynchronizationContextRemover();

            // Check the arguments.
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout");
            }

            CheckDisposed();

            // Block until we can enter the semaphore or until the timeout expires.
            var entered = await semaphore.WaitAsync(millisecondsTimeout);

            // If we entered the semaphore, compute the corresponding exit time 
            // and add it to the queue.
            if (entered)
            {
                exitTimes.Enqueue(CurrentExitTimer() + TimeUnit);
            }

            return entered;
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public async Task<bool> WaitToProceedAsync(TimeSpan timeout)
        {
            await new SynchronizationContextRemover();

            return await WaitToProceedAsync((int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Blocks the current thread indefinitely until allowed to proceed.
        /// </summary>
        public async Task WaitToProceedAsync()
        {
            await new SynchronizationContextRemover();

            await WaitToProceedAsync(Timeout.Infinite);
        }

        /// <summary>
        /// Releases unmanaged resources held by an instance of this class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Number of occurrences allowed per unit of time.
        /// </summary>
        public int Occurrences { get; private set; }

        /// <summary>
        /// The length of the time unit
        /// </summary>
        public TimeSpan TimeUnit { get; private set; }
    }
}
