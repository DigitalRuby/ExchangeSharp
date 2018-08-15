/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ExchangeSharp
{
    /// <summary>
    /// Upgrade to write lock
    /// </summary>
    public interface IReaderWriterLockAsyncUpgrade : IDisposable
    {
        /// <summary>
        /// Upgrade to write lock
        /// </summary>
        void UpgradeToWriteLock();
    }

    /// <summary>
    /// Non recursive reader/writer lock that handles async
    /// </summary>
    public class ReaderWriterLockAsync
    {
        private struct ReaderWriterLockAsyncLocker : IReaderWriterLockAsyncUpgrade
        {
            private bool write;
            private ReaderWriterLockAsync locker;

            public ReaderWriterLockAsyncLocker(ReaderWriterLockAsync locker, bool write)
            {
                this.locker = locker;
                this.write = write;

                // it is important that these never throw exceptions, they shouldn't ever
                // because Interlocked should never throw
                locker.WaitForReadLock();
                if (write)
                {
                    locker.WaitForWriteLock();
                }
            }

            public void Dispose()
            {
                if (locker != null)
                {
                    // it is important that these never throw exceptions, they shouldn't ever
                    // because Interlocked should never throw
                    locker.ReleaseReadLock();
                    if (write)
                    {
                        locker.ReleaseWriteLock();
                    }
                    locker = null;
                }
            }

            public void UpgradeToWriteLock()
            {
                if (write)
                {
                    throw new InvalidOperationException("Lock already upgraded");
                }

                // it is important that these never throw exceptions, they shouldn't ever
                // because Interlocked should never throw
                locker.WaitForWriteLock();
                write = true;
            }
        }

        private readonly int spinMilliseconds;

        private int readers;
        private int writers;

        private void WaitForReadLock()
        {
            Interlocked.Increment(ref readers);
            while (writers != 0)
            {
                // release the read lock
                Interlocked.Decrement(ref readers);

                // wait for no more writers
                while (writers != 0)
                {
                    // should be rare
                    Thread.Sleep(1);
                }

                // re-acquire the read lock
                Interlocked.Increment(ref readers);
            }
        }

        private void WaitForWriteLock()
        {
            if (readers == 0)
            {
                throw new InvalidOperationException("Must acquire read lock first");
            }

            // in order to acquire the write lock, there can be no other writers, and only 1 reader (the reader that was acquired right before this right lock)
            while (Interlocked.Increment(ref writers) != 1 || readers != 1)
            {
                Interlocked.Decrement(ref writers);

                // should be rare
                Thread.Sleep(1);
            }
        }

        private void ReleaseReadLock()
        {
            if (readers == 0)
            {
                throw new InvalidOperationException("Read lock already released");
            }

            Interlocked.Decrement(ref readers);
        }

        private void ReleaseWriteLock()
        {
            if (writers == 0)
            {
                throw new InvalidOperationException("Write lock already released");
            }

            Interlocked.Decrement(ref writers);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="spinMilliseconds">Spin lock wait in milliseconds</param>
        public ReaderWriterLockAsync(int spinMilliseconds = 1)
        {
            this.spinMilliseconds = spinMilliseconds;
        }

        /// <summary>
        /// Acquire a read lock
        /// </summary>
        /// <returns>Lock</returns>
        public IReaderWriterLockAsyncUpgrade LockRead()
        {
            return new ReaderWriterLockAsyncLocker(this, false);
        }

        /// <summary>
        /// Acquire a write lock
        /// </summary>
        /// <returns>Lock</returns>
        public IDisposable LockWrite()
        {
            return new ReaderWriterLockAsyncLocker(this, true);
        }
    }
}
