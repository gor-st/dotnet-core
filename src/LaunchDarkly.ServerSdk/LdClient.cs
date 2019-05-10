﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Common.Logging;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// A client for the LaunchDarkly API. Client instances are thread-safe. Applications should instantiate
    /// a single <c>LdClient</c> for the lifetime of their application.
    /// </summary>
    public sealed class LdClient : IDisposable, ILdClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        private readonly Configuration _configuration;
        internal readonly IEventProcessor _eventProcessor;
        private readonly IFeatureStore _featureStore;
        internal readonly IUpdateProcessor _updateProcessor;
        private bool _shouldDisposeEventProcessor;
        private bool _shouldDisposeFeatureStore;

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration, and a custom
        /// implementation of the analytics event processor.
        /// 
        /// This constructor is deprecated; please use
        /// <see cref="ConfigurationExtensions.WithEventProcessorFactory(Configuration, IEventProcessorFactory)"/>
        /// instead.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        /// <param name="eventProcessor">an event processor</param>
        [Obsolete("Deprecated, please use Configuration.WithEventProcessorFactory")]
        public LdClient(Configuration config, IEventProcessor eventProcessor)
        {
            Log.InfoFormat("Starting LaunchDarkly Client {0}",
                ServerSideClientEnvironment.Instance.Version);

            _configuration = config;

            if (eventProcessor == null)
            {
                _eventProcessor = (_configuration.EventProcessorFactory ??
                    Components.DefaultEventProcessor).CreateEventProcessor(_configuration);
                _shouldDisposeEventProcessor = true;
            }
            else
            {
                _eventProcessor = eventProcessor;
                // The following line is for backward compatibility with the obsolete mechanism by which the
                // caller could pass in an IStoreEvents implementation instance that we did not create.  We
                // were not disposing of that instance when the client was closed, so we should continue not
                // doing so until the next major version eliminates that mechanism.  We will always dispose
                // of instances that we created ourselves from a factory.
                _shouldDisposeEventProcessor = false;
            }

            IFeatureStore store;
            if (_configuration.FeatureStore == null)
            {
                store = (_configuration.FeatureStoreFactory ??
                    Components.InMemoryFeatureStore).CreateFeatureStore();
                _shouldDisposeFeatureStore = true;
            }
            else
            {
                store = _configuration.FeatureStore;
                _shouldDisposeFeatureStore = false; // see previous comment
            }
            _featureStore = new FeatureStoreClientWrapper(store);

            _updateProcessor = (_configuration.UpdateProcessorFactory ??
                Components.DefaultUpdateProcessor).CreateUpdateProcessor(_configuration, _featureStore);

            var initTask = _updateProcessor.Start();

            if (!(_updateProcessor is NullUpdateProcessor))
            {
                Log.InfoFormat("Waiting up to {0} milliseconds for LaunchDarkly client to start..",
                    _configuration.StartWaitTime.TotalMilliseconds);
            }

            try
            {
                var unused = initTask.Wait(_configuration.StartWaitTime);
            }
            catch (AggregateException)
            {
                // StreamProcessor may throw an exception if initialization fails, because we want that behavior
                // in the Xamarin client. However, for backward compatibility we do not want to throw exceptions
                // from the LdClient constructor in the .NET client, so we'll just swallow this.
            }
        }

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration. This constructor
        /// can be used to configure advanced client features, such as customizing the LaunchDarkly base URL.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        #pragma warning disable 618  // suppress warning for calling obsolete ctor
        public LdClient(Configuration config) : this(config, null)
        #pragma warning restore 618
        {
        }

        /// <summary>
        /// Creates a new client instance that connects to LaunchDarkly with the default configuration. In most
        /// cases, you should use this constructor.
        /// </summary>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
        {
        }

        /// <see cref="ILdClient.Initialized"/>
        public bool Initialized()
        {
            return IsOffline() || _updateProcessor.Initialized();
        }

        /// <see cref="ILdCommonClient.IsOffline"/>
        public bool IsOffline()
        {
            return _configuration.Offline;
        }

        /// <see cref="ILdClient.BoolVariation(string, User, bool)"/>
        public bool BoolVariation(string key, User user, bool defaultValue = false)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Boolean, EventFactory.Default).Value;
            return value.Value<bool>();
        }

        /// <see cref="ILdClient.IntVariation(string, User, int)"/>
        public int IntVariation(string key, User user, int defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Integer, EventFactory.Default).Value;
            return value.Value<int>();
        }

        /// <see cref="ILdClient.FloatVariation(string, User, float)"/>
        public float FloatVariation(string key, User user, float defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Float, EventFactory.Default).Value;
            return value.Value<float>();
        }

        /// <see cref="ILdClient.StringVariation(string, User, string)"/>
        public string StringVariation(string key, User user, string defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.String, EventFactory.Default).Value;
            return value.Value<string>();
        }

        /// <see cref="ILdClient.JsonVariation(string, User, JToken)"/>
        public JToken JsonVariation(string key, User user, JToken defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, null, EventFactory.Default).Value;
            return value;
        }

        /// <see cref="ILdClient.BoolVariationDetail(string, User, bool)"/>
        public EvaluationDetail<bool> BoolVariationDetail(string key, User user, bool defaultValue)
        {
            var detail = Evaluate(key, user, defaultValue, JTokenType.Boolean, EventFactory.DefaultWithReasons);
            return new EvaluationDetail<bool>((bool)detail.Value, detail.VariationIndex, detail.Reason);
        }

        /// <see cref="ILdClient.IntVariationDetail(string, User, int)"/>
        public EvaluationDetail<int> IntVariationDetail(string key, User user, int defaultValue)
        {
            var detail = Evaluate(key, user, defaultValue, JTokenType.Integer, EventFactory.DefaultWithReasons);
            return new EvaluationDetail<int>((int)detail.Value, detail.VariationIndex, detail.Reason);
        }

        /// <see cref="ILdClient.FloatVariationDetail(string, User, float)"/>
        public EvaluationDetail<float> FloatVariationDetail(string key, User user, float defaultValue)
        {
            var detail = Evaluate(key, user, defaultValue, JTokenType.Float, EventFactory.DefaultWithReasons);
            return new EvaluationDetail<float>((float)detail.Value, detail.VariationIndex, detail.Reason);
        }

        /// <see cref="ILdClient.StringVariationDetail(string, User, string)"/>
        public EvaluationDetail<string> StringVariationDetail(string key, User user, string defaultValue)
        {
            var detail = Evaluate(key, user, defaultValue, JTokenType.String, EventFactory.DefaultWithReasons);
            return new EvaluationDetail<string>((string)detail.Value, detail.VariationIndex, detail.Reason);
        }

        /// <see cref="ILdClient.JsonVariationDetail(string, User, JToken)"/>
        public EvaluationDetail<JToken> JsonVariationDetail(string key, User user, JToken defaultValue)
        {
            return Evaluate(key, user, defaultValue, null, EventFactory.DefaultWithReasons);
        }

        /// <see cref="ILdClient.AllFlags(User)"/>
        public IDictionary<string, JToken> AllFlags(User user)
        {
            var state = AllFlagsState(user);
            if (!state.Valid)
            {
                return null;
            }
            return state.ToValuesMap();
        }

        /// <see cref="ILdClient.AllFlagsState(User, FlagsStateOption[])"/>
        public FeatureFlagsState AllFlagsState(User user, params FlagsStateOption[] options)
        {
            if (IsOffline())
            {
                Log.Warn("AllFlagsState() was called when client is in offline mode. Returning empty state.");
                return new FeatureFlagsState(false);
            }
            if (!Initialized())
            {
                if (_featureStore.Initialized())
                {
                    Log.Warn("AllFlagsState() called before client initialized; using last known values from feature store");
                }
                else
                {
                    Log.Warn("AllFlagsState() called before client initialized; feature store unavailable, returning empty state");
                    return new FeatureFlagsState(false);
                }
            }
            if (user == null || user.Key == null)
            {
                Log.Warn("AllFlagsState() called with null user or null user key. Returning empty state");
                return new FeatureFlagsState(false);
            }

            var state = new FeatureFlagsState(true);
            var clientSideOnly = FlagsStateOption.HasOption(options, FlagsStateOption.ClientSideOnly);
            var withReasons = FlagsStateOption.HasOption(options, FlagsStateOption.WithReasons);
            var detailsOnlyIfTracked = FlagsStateOption.HasOption(options, FlagsStateOption.DetailsOnlyForTrackedFlags);
            IDictionary<string, FeatureFlag> flags = _featureStore.All(VersionedDataKind.Features);
            foreach (KeyValuePair<string, FeatureFlag> pair in flags)
            {
                var flag = pair.Value;
                if (clientSideOnly && !flag.ClientSide)
                {
                    continue;
                }
                try
                {
                    FeatureFlag.EvalResult result = flag.Evaluate(user, _featureStore, EventFactory.Default);
                    state.AddFlag(flag, result.Result.Value, result.Result.VariationIndex,
                        withReasons ? result.Result.Reason : null, detailsOnlyIfTracked);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Exception caught for feature flag \"{0}\" when evaluating all flags: {1}", flag.Key, Util.ExceptionMessage(e));
                    Log.Debug(e.ToString(), e);
                    EvaluationReason reason = new EvaluationReason.Error(EvaluationErrorKind.EXCEPTION);
                    state.AddFlag(flag, null, null, withReasons ? reason : null, detailsOnlyIfTracked);
                }
            }
            return state;
        }

        private EvaluationDetail<JToken> Evaluate(string featureKey, User user, JToken defaultValue, JTokenType? expectedType,
            EventFactory eventFactory)
        {
            if (!Initialized())
            {
                if (_featureStore.Initialized())
                {
                    Log.Warn("Flag evaluation before client initialized; using last known values from feature store");
                }
                else
                {
                    Log.Warn("Flag evaluation before client initialized; feature store unavailable, returning default value");
                    return new EvaluationDetail<JToken>(defaultValue, null,
                        new EvaluationReason.Error(EvaluationErrorKind.CLIENT_NOT_READY));
                }
            }

            FeatureFlag featureFlag = null;
            try
            {
                featureFlag = _featureStore.Get(VersionedDataKind.Features, featureKey);
                if (featureFlag == null)
                {
                    Log.InfoFormat("Unknown feature flag {0}; returning default value",
                        featureKey);

                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue,
                        EvaluationErrorKind.FLAG_NOT_FOUND));
                    return new EvaluationDetail<JToken>(defaultValue, null,
                        new EvaluationReason.Error(EvaluationErrorKind.FLAG_NOT_FOUND));
                }

                if (user == null || user.Key == null)
                {
                    Log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
                    _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue,
                        EvaluationErrorKind.USER_NOT_SPECIFIED));
                    return new EvaluationDetail<JToken>(defaultValue, null,
                        new EvaluationReason.Error(EvaluationErrorKind.USER_NOT_SPECIFIED));
                }
                
                FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, _featureStore, eventFactory);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvents)
                    {
                        _eventProcessor.SendEvent(prereqEvent);
                    }
                }
                var detail = evalResult.Result;
                if (detail.VariationIndex == null)
                {
                    detail = new EvaluationDetail<JToken>(defaultValue, null, detail.Reason);
                }
                if (detail.Value != null && !CheckResultType(expectedType, detail.Value))
                {
                    Log.ErrorFormat("Expected type: {0} but got {1} when evaluating FeatureFlag: {2}. Returning default",
                        expectedType,
                        detail.Value.GetType(),
                        featureKey);

                    _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue,
                        EvaluationErrorKind.WRONG_TYPE));
                    return new EvaluationDetail<JToken>(defaultValue, null,
                        new EvaluationReason.Error(EvaluationErrorKind.WRONG_TYPE));
                }
                _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(featureFlag, user, detail, defaultValue));
                return detail;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                     Util.ExceptionMessage(e),
                     featureKey,
                     user.Key);
                Log.Debug(e.ToString(), e);
                var detail = new EvaluationDetail<JToken>(defaultValue, null,
                    new EvaluationReason.Error(EvaluationErrorKind.EXCEPTION));
                if (featureFlag == null)
                {
                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue,
                        EvaluationErrorKind.EXCEPTION));
                }
                else
                {
                    _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(featureFlag, user,
                        detail, defaultValue));
                }
                return detail;
            }
        }

        private bool CheckResultType(JTokenType? expectedType, JToken result)
        {
            if (expectedType == null || result == null)
            {
                return true;
            }
            JTokenType resultType = result.Type;
            switch (expectedType.Value)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return resultType == JTokenType.Integer || resultType == JTokenType.Float;
                default:
                    return resultType == expectedType;
            }
        }

        /// <see cref="ILdClient.SecureModeHash(User)"/>
        public string SecureModeHash(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Key))
            {
                return null;
            }
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] keyBytes = encoding.GetBytes(_configuration.SdkKey);

            HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes);
            byte[] hashedMessage = hmacSha256.ComputeHash(encoding.GetBytes(user.Key));
            return BitConverter.ToString(hashedMessage).Replace("-", "").ToLower();
        }

        /// <see cref="ILdClient.Track(string, User)"/>
        public void Track(string name, User user)
        {
            Track(name, null, user);
        }

        /// <see cref="ILdClient.Track(string, User, string)"/>
        public void Track(string name, User user, string data)
        {
            Track(name, data, user);
        }

        /// <see cref="ILdClient.Track(string, JToken, User)"/>
        public void Track(string name, JToken data, User user)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                Log.Warn("Track called with null user or null user key");
                return;
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewCustomEvent(name, user, data));
        }

        /// <see cref="ILdClient.Identify(User)"/>
        public void Identify(User user)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                Log.Warn("Identify called with null user or null user key");
                return;
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewIdentifyEvent(user));
        }

        /// <see cref="ILdCommonClient.Version"/>
        public Version Version
        {
            get
            {
                return ServerSideClientEnvironment.Instance.Version;
            }
        }
        
        private void Dispose(bool disposing)
        {
            if (disposing) // follow standard IDisposable pattern
            {
                Log.Info("Closing LaunchDarkly client.");
                // See comments in LdClient constructor: eventually all of these implementation objects
                // will be factory-created and will have the same lifecycle as the client.
                if (_shouldDisposeEventProcessor)
                {
                    _eventProcessor.Dispose();
                }
                if (_shouldDisposeFeatureStore)
                {
                    _featureStore.Dispose();
                }
                _updateProcessor.Dispose();
            }
        }

        /// <summary>
        /// Shuts down the client and releases any resources it is using.
        /// 
        /// Any components that were added by specifying a factory object
        /// (<see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>, etc.)
        /// will also be disposed of by this method; their lifecycle is the same as the client's.
        /// However, for any components that you constructed yourself and passed in (via the deprecated
        /// method <see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>,
        /// or the deprecated <c>LdClient</c> constructor that takes an <see cref="IEventProcessor"/>),
        /// this will not happen; you are responsible for managing their lifecycle.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <see cref="ILdCommonClient.Flush"/>
        public void Flush()
        {
            _eventProcessor.Flush();
        }
    }
}