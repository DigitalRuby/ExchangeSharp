/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

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
        private readonly ReaderWriterLockAsync cacheTimerLock = new ReaderWriterLockAsync();

#if DEBUG

        private readonly int cacheTimerInterval;

#endif


        

        private void TimerCallback(object state)
        {

#if DEBUG

            // disable timer during debug, we don't want multiple callbacks fouling things up
            cacheTimer.Change(-1, -1);

#endif

            DateTime now = DateTime.UtcNow;
            using (var lockerWrite = cacheTimerLock.LockWrite())
            {
                foreach (var item in cache.ToArray())
                {
                    if (item.Value.Key < now)
                    {
                        cache.Remove(item.Key);
                    }
                }
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
            using (var lockRead = cacheTimerLock.LockRead())
            {
                if (cache.TryGetValue(key, out KeyValuePair<DateTime, object> cacheValue))
                {
                    return new CachedItem<T>((T)cacheValue.Value, cacheValue.Key);
                }
            }

            // most likely the callback needs to make a network request, so don't do it in a lock
            // it's ok if multiple calls stack on the same cache key, the last one to finish will win
            CachedItem<T> newItem = await notFound();

            // don't add null values to the cache
            if (newItem.Value != null)
            {
                using (var lockWrite = cacheTimerLock.LockWrite())
                {
                    cache[key] = new KeyValuePair<DateTime, object>(newItem.Expiration, newItem.Value);
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
            using (var lockWrite = cacheTimerLock.LockWrite())
            {
                return cache.Remove(key);
            }
        }
    }
}
