﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Client.Interfaces;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client.Integrations
{
    /// <summary>
    /// Contains methods for configuring delivery of analytics events.
    /// </summary>
    /// <remarks>
    /// The SDK normally buffers analytics events and sends them to LaunchDarkly at intervals. If you want
    /// to customize this behavior, create a builder with <see cref="Components.SendEvents"/>, change its
    /// properties with the methods of this class, and pass it to <see cref="ConfigurationBuilder.Events(IEventProcessorFactory)"/>.
    /// </remarks>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder(sdkKey)
    ///         .Events(
    ///             Components.SendEvents().Capacity(5000).FlushInterval(TimeSpan.FromSeconds(2))
    ///         )
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class EventProcessorBuilder : IEventProcessorFactory, IEventProcessorFactoryWithDiagnostics, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="Capacity(int)"/>.
        /// </summary>
        public const int DefaultCapacity = 10000;

        /// <summary>
        /// The default value for <see cref="DiagnosticRecordingInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultDiagnosticRecordingInterval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The default value for <see cref="FlushInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The default value for <see cref="UserKeysCapacity(int)"/>.
        /// </summary>
        public const int DefaultUserKeysCapacity = 1000;

        /// <summary>
        /// The default value for <see cref="UserKeysFlushInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultUserKeysFlushInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The minimum value for <see cref="DiagnosticRecordingInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan MinimumDiagnosticRecordingInterval = TimeSpan.FromMinutes(1);

        internal static readonly Uri DefaultBaseUri = new Uri("https://events.launchdarkly.com");

        internal bool _allAttributesPrivate = false;
        internal Uri _baseUri = DefaultBaseUri;
        internal int _capacity = DefaultCapacity;
        internal TimeSpan _diagnosticRecordingInterval = DefaultDiagnosticRecordingInterval;
        internal TimeSpan _flushInterval = DefaultFlushInterval;
        internal bool _inlineUsersInEvents = false;
        internal HashSet<string> _privateAttributes = new HashSet<string>();
        internal int _userKeysCapacity = DefaultUserKeysCapacity;
        internal TimeSpan _userKeysFlushInterval = DefaultUserKeysFlushInterval;

        internal int _samplingInterval = 0; // deprecated

        /// <summary>
        /// Sets whether or not all optional user attributes should be hidden from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// If this is <see langword="true"/>, all user attribute values (other than the key) will be private, not just
        /// the attributes specified in <see cref="PrivateAttributeNames(string[])"/> or on a per-user basis with
        /// <see cref="UserBuilder"/> methods. By default, it is <see langword="false"/>.
        /// </remarks>
        /// <param name="allAttributesPrivate">true if all user attributes should be private</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder AllAttributesPrivate(bool allAttributesPrivate)
        {
            _allAttributesPrivate = allAttributesPrivate;
            return this;
        }

        /// <summary>
        /// Sets a custom base URI for the events service.
        /// </summary>
        /// <remarks>
        /// You will only need to change this value in the following cases:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// You are using the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">Relay Proxy</a>.
        /// Set <c>BaseUri</c> to the base URI of the Relay Proxy instance.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// You are connecting to a test server or a nonstandard endpoint for the LaunchDarkly service.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="baseUri">the base URI of the events service; null to use the default</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri ?? DefaultBaseUri;
            return this;
        }

        /// <summary>
        /// Sets the capacity of the events buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The client buffers up to this many events in memory before flushing. If the capacity is exceeded before
        /// the buffer is flushed (see <see cref="FlushInterval(TimeSpan)"/>), events will be discarded. Increasing the
        /// capacity means that events are less likely to be discarded, at the cost of consuming more memory.
        /// </para>
        /// <para>
        /// The default value is <see cref="DefaultCapacity"/>. A zero or negative value will be changed to the default.
        /// </para>
        /// </remarks>
        /// <param name="capacity">the capacity of the event buffer</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder Capacity(int capacity)
        {
            _capacity = (capacity <= 0) ? DefaultCapacity : capacity;
            return this;
        }

        /// <summary>
        /// Sets the interval at which periodic diagnostic data is sent.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="DefaultDiagnosticRecordingInterval"/>; the minimum value is
        /// <see cref="MinimumDiagnosticRecordingInterval"/>. This property is ignored if
        /// <see cref="ConfigurationBuilder.DiagnosticOptOut(bool)"/> is set to <see langword="true"/>.
        /// </remarks>
        /// <param name="diagnosticRecordingInterval">the diagnostics interval</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval)
        {
            _diagnosticRecordingInterval =
                diagnosticRecordingInterval.CompareTo(MinimumDiagnosticRecordingInterval) < 0 ?
                MinimumDiagnosticRecordingInterval : diagnosticRecordingInterval;
            return this;
        }

        // Used only in testing
        internal EventProcessorBuilder DiagnosticRecordingIntervalNoMinimum(TimeSpan diagnosticRecordingInterval)
        {
            _diagnosticRecordingInterval = diagnosticRecordingInterval;
            return this;
        }

        /// <summary>
        /// Sets the interval between flushes of the event buffer.
        /// </summary>
        /// <remarks>
        /// Decreasing the flush interval means that the event buffer is less likely to reach capacity.
        /// The default value is <see cref="DefaultFlushInterval"/>. A zero or negative value will be changed to
        /// the default.
        /// </remarks>
        /// <param name="flushInterval">the flush interval</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder FlushInterval(TimeSpan flushInterval)
        {
            _flushInterval = (flushInterval.CompareTo(TimeSpan.Zero) <= 0) ?
                DefaultFlushInterval : flushInterval;
            return this;
        }

        /// <summary>
        /// Sets whether to include full user details in every analytics event.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="false"/>: events will only include the user key, except for one
        /// "index" event that provides the full details for the user.
        /// </remarks>
        /// <param name="inlineUsersInEvents">true if you want full user details in each event</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder InlineUsersInEvents(bool inlineUsersInEvents)
        {
            _inlineUsersInEvents = inlineUsersInEvents;
            return this;
        }

        /// <summary>
        /// Marks a set of attribute names as private.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed. This is in addition to any attributes that were marked as private for an
        /// individual user with <see cref="UserBuilder"/> methods.
        /// </para>
        /// </remarks>
        /// <param name="attributes">a set of names that will be removed from user data set to LaunchDarkly</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder PrivateAttributeNames(params string[] attributes)
        {
            foreach (var a in attributes)
            {
                _privateAttributes.Add(a);
            }
            return this;
        }

        /// <summary>
        /// Sets the number of user keys that the event processor can remember at any one time.
        /// </summary>
        /// <remarks>
        /// To avoid sending duplicate user details in analytics events, the SDK maintains a cache of
        /// recently seen user keys, expiring at an interval set by <see cref="UserKeysFlushInterval(TimeSpan)"/>.
        /// The default value for the size of this cache is <see cref="DefaultUserKeysCapacity"/>. A zero or
        /// negative value will be changed to the default.
        /// </remarks>
        /// <param name="userKeysCapacity">the maximum number of user keys to remember</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder UserKeysCapacity(int userKeysCapacity)
        {
            _userKeysCapacity = (userKeysCapacity <= 0) ? DefaultUserKeysCapacity : userKeysCapacity;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the event processor will reset its cache of known user keys.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="DefaultUserKeysFlushInterval"/>. A zero or negative value will be
        /// changed to the default.
        /// </remarks>
        /// <param name="userKeysFlushInterval">the flush interval</param>
        /// <returns>the builder</returns>
        /// <see cref="UserKeysCapacity(int)"/>
        public EventProcessorBuilder UserKeysFlushInterval(TimeSpan userKeysFlushInterval)
        {
            _userKeysFlushInterval = (userKeysFlushInterval.CompareTo(TimeSpan.Zero) <= 0) ?
                DefaultUserKeysFlushInterval : userKeysFlushInterval;
            return this;
        }

        internal EventProcessorBuilder SamplingInterval(int samplingInterval)
        {
            _samplingInterval = samplingInterval;
            return this;
        }

        /// <inheritdoc/>
        public IEventProcessor CreateEventProcessor(Configuration config) =>
            ((IEventProcessorFactoryWithDiagnostics)this).CreateEventProcessor(config, null);

        /// <inheritdoc/>
        IEventProcessor IEventProcessorFactoryWithDiagnostics.CreateEventProcessor(Configuration config, IDiagnosticStore diagnosticStore)
        {
            var httpConfig = config.HttpConfiguration;
            var eventsConfig = new EventProcessorConfigImpl
            {
                AllAttributesPrivate = _allAttributesPrivate,
                DiagnosticOptOut = config.DiagnosticOptOut,
                DiagnosticRecordingInterval = _diagnosticRecordingInterval,
                EventCapacity = _capacity,
                EventFlushInterval = _flushInterval,
                EventSamplingInterval = _samplingInterval,
                EventsUri = new Uri(_baseUri, "bulk"),
                DiagnosticUri = new Uri(_baseUri, "diagnostic"),
                HttpClientTimeout = httpConfig.ConnectTimeout,
                InlineUsersInEvents = _inlineUsersInEvents,
                PrivateAttributeNames = _privateAttributes.ToImmutableHashSet(),
                ReadTimeout = httpConfig.ReadTimeout,
                ReconnectTime = TimeSpan.FromSeconds(1),
                UserKeysCapacity = _userKeysCapacity,
                UserKeysFlushInterval = _userKeysFlushInterval
            };

            return new DefaultEventProcessor(
                eventsConfig,
                new DefaultUserDeduplicator(_userKeysCapacity, _userKeysFlushInterval),
                Util.MakeHttpClient(config.HttpRequestConfiguration, ServerSideClientEnvironment.Instance),
                diagnosticStore,
                null,
                null
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(Configuration config)
        {
            return LdValue.BuildObject()
                .Add("allAttributesPrivate", _allAttributesPrivate)
                .Add("customEventsURI", !_baseUri.Equals(DefaultBaseUri))
                .Add("diagnosticRecordingIntervalMillis", _diagnosticRecordingInterval.TotalMilliseconds)
                .Add("eventsCapacity", _capacity)
                .Add("eventsFlushIntervalMillis", _flushInterval.TotalMilliseconds)
                .Add("inlineUsersInEvents", _inlineUsersInEvents)
                .Add("samplingInterval", _samplingInterval)
                .Add("userKeysCapacity", _userKeysCapacity)
                .Add("userKeysFlushIntervalMillis", _userKeysFlushInterval.TotalMilliseconds)
                .Build();
        }

        internal struct EventProcessorConfigImpl: IEventProcessorConfiguration
        {
            internal Configuration Config { get; set; }
            public bool AllAttributesPrivate { get; internal set; }
            public bool DiagnosticOptOut { get; internal set; }
            public TimeSpan DiagnosticRecordingInterval { get; internal set; }
            public int EventCapacity { get; internal set; }
            public TimeSpan EventFlushInterval { get; internal set; }
#pragma warning disable 618
            public int EventSamplingInterval { get; internal set; }
#pragma warning restore 618
            public Uri EventsUri { get; internal set; }
            public Uri DiagnosticUri { get; internal set; }
            public TimeSpan HttpClientTimeout { get; internal set; }
            public bool InlineUsersInEvents { get; internal set; }
            public ISet<string> PrivateAttributeNames { get; internal set; }
            public TimeSpan ReadTimeout { get; internal set; }
            public TimeSpan ReconnectTime { get; internal set; }
            public int UserKeysCapacity { get; internal set; }
            public TimeSpan UserKeysFlushInterval { get; internal set; }
        }

    }
}