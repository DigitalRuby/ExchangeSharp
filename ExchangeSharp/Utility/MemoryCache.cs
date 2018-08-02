using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Cache item
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    public struct CachedItem<T>
    {
        /// <summary>
        /// Constructor for found result
        /// </summary>
        /// <param name="result">Result</param>
        /// <param name="expiration">Expiration</param>
        public CachedItem(T result, DateTime expiration)
        {
            Found = true;
            Value = result;
            Expiration = expiration;
        }

        /// <summary>
        /// True if found, false otherwise
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// If found, contains the value from the cache
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Item expiration if found
        /// </summary>
        public DateTime Expiration { get; private set; }
    }

    /// <summary>
    /// Simple fast in memory cache with auto expiration
    /// </summary>
    public class MemoryCache : IDisposable
    {
        private readonly Dictionary<string, KeyValuePair<DateTime, object>> cache = new Dictionary<string, KeyValuePair<DateTime, object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer cacheTimer;

#if DEBUG

        private readonly int cacheTimerInterval;

#endif

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
            Interlocked.Decrement(ref readers);
        }

        private void ReleaseWriteLock()
        {
            Interlocked.Decrement(ref writers);
        }

        private void TimerCallback(object state)
        {

#if DEBUG

            // disable timer during debug, we don't want multiple callbacks fouling things up
            cacheTimer.Change(-1, -1);

#endif

            DateTime now = DateTime.UtcNow;
            WaitForReadLock();
            try
            {
                WaitForWriteLock();
                try
                {
                    foreach (var item in cache.ToArray())
                    {
                        if (item.Value.Key < now)
                        {
                            cache.Remove(item.Key);
                        }
                    }
                }
                finally
                {
                    ReleaseWriteLock();
                }
            }
            finally
            {
                ReleaseReadLock();
            }

#if DEBUG

            cacheTimer.Change(cacheTimerInterval, cacheTimerInterval);

#endif

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cleanupIntervalMilliseconds">Cleanup interval in milliseconds, removes expired items from cache</param>
        public MemoryCache(int cleanupIntervalMilliseconds = 10000)
        {

#if DEBUG

            cacheTimerInterval = cleanupIntervalMilliseconds;

#endif

            // set timer to remove expired cache items
            cacheTimer = new Timer(new System.Threading.TimerCallback(TimerCallback), null, cleanupIntervalMilliseconds, cleanupIntervalMilliseconds);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~MemoryCache()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            try
            {
                cacheTimer.Dispose();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Read a value from the cache
        /// </summary>
        /// <typeparam name="T">Type to read</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="notFound">Create T if not found, null to not do this. Item1 = value, Item2 = expiration.</param>
        public async Task<CachedItem<T>> Get<T>(string key, Func<Task<CachedItem<T>>> notFound) where T : class
        {
            WaitForReadLock();
            try
            {
                if (cache.TryGetValue(key, out KeyValuePair<DateTime, object> cacheValue))
                {
                    return new CachedItem<T>((T)cacheValue.Value, cacheValue.Key);
                }
            }
            finally
            {
                ReleaseReadLock();
            }

            // most likely the callback needs to make a network request, so don't do it in a lock
            // it's ok if multiple calls stack on the same cache key, the last one to finish will win
            CachedItem<T> newItem = await notFound();

            // don't add null values to the cache
            if (newItem.Value != null)
            {
                try
                {
                    WaitForReadLock();
                    try
                    {
                        WaitForWriteLock();
                        cache[key] = new KeyValuePair<DateTime, object>(newItem.Expiration, newItem.Value);
                    }
                    finally
                    {
                        ReleaseWriteLock();
                    }
                }
                finally
                {
                    ReleaseReadLock();
                }
            }

            return newItem;
        }

        /// <summary>
        /// Remove a key from the cache immediately
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>True if removed, false if not found</returns>
        public bool Remove(string key)
        {
            try
            {
                WaitForReadLock();
                try
                {
                    WaitForWriteLock();
                    return cache.Remove(key);
                }
                finally
                {
                    ReleaseWriteLock();
                }
            }
            finally
            {
                ReleaseReadLock();
            }
        }
    }
}
