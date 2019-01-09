﻿using System;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Parameters that can be used for <see cref="IFeatureStore"/> implementations that support local caching.
    /// The built-in <see cref="InMemoryFeatureStore"/> does not use this class; it is meant for database
    /// implementations.
    ///
    /// This is an immutable class that uses a fluent interface. Obtain an instance by getting the static
    /// value Disabled or Enabled; then if desired, you can use chained methods to set other properties:
    /// <code>
    ///     FeatureStoreCacheConfig.Enabled.WithTtlSeconds(30);
    /// </code>
    /// </summary>
    public sealed class FeatureStoreCacheConfig
    {
        /// <summary>
        /// The default cache expiration time.
        /// </summary>
        public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The cache expiration time. Caching is enabled if this is greater than zero.
        /// </summary>
        /// <seealso cref="WithTtl(TimeSpan)"/>
        /// <seealso cref="WithTtlMillis(double)"/>
        /// <seealso cref="WithTtlSeconds(double)"/>
        public TimeSpan Ttl { get; private set; }

        /// <summary>
        /// True if caching is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return Ttl > TimeSpan.Zero;
            }
        }

        /// <summary>
        /// The maximum number of entries that can be held in the cache at a time.
        /// </summary>
        public int? MaximumEntries { get; private set; }

        /// <summary>
        /// Returns a parameter object indicating that caching should be disabled.
        /// </summary>
        public static readonly FeatureStoreCacheConfig Disabled = new FeatureStoreCacheConfig(TimeSpan.Zero, null);

        /// <summary>
        /// Returns a parameter object indicating that caching should be enabled, using the
        /// default TTL of <see cref="DefaultTtl"/>.
        /// </summary>
        public static readonly FeatureStoreCacheConfig Enabled = new FeatureStoreCacheConfig(DefaultTtl, null);

        internal FeatureStoreCacheConfig(TimeSpan ttl, int? maximumEntries)
        {
            Ttl = ttl;
            MaximumEntries = maximumEntries;
        }
        
        /// <summary>
        /// Specifies the cache TTL. Items will expire from the cache after this amount of time from the
        /// time when they were originally cached.
        /// </summary>
        /// <param name="ttl">the cache TTL; must be greater than zero</param>
        /// <returns>an updated parameters object</returns>
        public FeatureStoreCacheConfig WithTtl(TimeSpan ttl)
        {
            return new FeatureStoreCacheConfig(ttl, MaximumEntries);
        }

        /// <summary>
        /// Shortcut for calling <see cref="WithTtl"/> with a TimeSpan in milliseconds.
        /// </summary>
        /// <param name="millis">the cache TTL in milliseconds</param>
        /// <returns>an updated parameters object</returns>
        public FeatureStoreCacheConfig WithTtlMillis(double millis)
        {
            return WithTtl(TimeSpan.FromMilliseconds(millis));
        }

        /// <summary>
        /// Shortcut for calling <see cref="WithTtl"/> with a TimeSpan in seconds.
        /// </summary>
        /// <param name="seconds">the cache TTL in seconds</param>
        /// <returns>an updated parameters object</returns>
        public FeatureStoreCacheConfig WithTtlSeconds(double seconds)
        {
            return WithTtl(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Specifies the maximum number of entries that can be held in the cache at a time.
        /// If this limit is exceeded, older entries will be evicted from the cache to make room
        /// for new ones.
        /// 
        /// If this is null, there is no limit on the number of entries.
        /// </summary>
        /// <param name="maximumEntries">the maximum number of entries, or null for no limit</param>
        /// <returns>an updated parameters object</returns>
        public FeatureStoreCacheConfig WithMaximumEntries(int? maximumEntries)
        {
            if (maximumEntries != null && maximumEntries <= 0)
            {
                throw new ArgumentException("must be > 0 if not null", nameof(maximumEntries));
            }
            return new FeatureStoreCacheConfig(Ttl, maximumEntries);
        }
    }
}
