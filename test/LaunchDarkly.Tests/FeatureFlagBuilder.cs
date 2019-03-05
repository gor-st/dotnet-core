﻿using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Tests
{
    internal class FeatureFlagBuilder
    {
        private readonly string _key;
        private int _version;
        private bool _on;
        private List<Prerequisite> _prerequisites = new List<Prerequisite>();
        private string _salt;
        private List<Target> _targets = new List<Target>();
        private List<Rule> _rules = new List<Rule>();
        private VariationOrRollout _fallthrough;
        private int? _offVariation;
        private List<JToken> _variations;
        private bool _trackEvents;
        private bool _trackEventsFallthrough;
        private long? _debugEventsUntilDate;
        private bool _deleted;
        private bool _clientSide;

        internal FeatureFlagBuilder(string key)
        {
            _key = key;
        }

        internal FeatureFlagBuilder(FeatureFlag from)
        {
            _key = from.Key;
            _version = from.Version;
            _on = from.On;
            _prerequisites = from.Prerequisites;
            _salt = from.Salt;
            _targets = from.Targets;
            _rules = from.Rules;
            _fallthrough = from.Fallthrough;
            _offVariation = from.OffVariation;
            _variations = from.Variations;
            _trackEvents = from.TrackEvents;
            _trackEventsFallthrough = from.TrackEventsFallthrough;
            _debugEventsUntilDate = from.DebugEventsUntilDate;
            _deleted = from.Deleted;
            _clientSide = from.ClientSide;
        }

        internal FeatureFlag Build()
        {
            return new FeatureFlag(_key, _version, _on, _prerequisites, _salt,
                _targets, _rules, _fallthrough, _offVariation, _variations,
                _trackEvents, _trackEventsFallthrough, _debugEventsUntilDate, _deleted, _clientSide);
        }

        internal FeatureFlagBuilder Version(int version)
        {
            _version = version;
            return this;
        }

        internal FeatureFlagBuilder On(bool on)
        {
            _on = on;
            return this;
        }

        internal FeatureFlagBuilder Prerequisites(List<Prerequisite> prerequisites)
        {
            _prerequisites = prerequisites;
            return this;
        }

        internal FeatureFlagBuilder Prerequisites(params Prerequisite[] prerequisites)
        {
            return Prerequisites(new List<Prerequisite>(prerequisites));
        }

        internal FeatureFlagBuilder Salt(string salt)
        {
            _salt = salt;
            return this;
        }

        internal FeatureFlagBuilder Targets(List<Target> targets)
        {
            _targets = targets;
            return this;
        }

        internal FeatureFlagBuilder Targets(params Target[] targets)
        {
            return Targets(new List<Target>(targets));
        }

        internal FeatureFlagBuilder Rules(List<Rule> rules)
        {
            _rules = rules;
            return this;
        }

        internal FeatureFlagBuilder Rules(params Rule[] rules)
        {
            return Rules(new List<Rule>(rules));
        }

        internal FeatureFlagBuilder Fallthrough(VariationOrRollout fallthrough)
        {
            _fallthrough = fallthrough;
            return this;
        }

        internal FeatureFlagBuilder FallthroughVariation(int variation)
        {
            _fallthrough = new VariationOrRollout(variation, null);
            return this;
        }

        internal FeatureFlagBuilder OffVariation(int? offVariation)
        {
            _offVariation = offVariation;
            return this;
        }

        internal FeatureFlagBuilder Variations(List<JToken> variations)
        {
            _variations = variations;
            return this;
        }

        internal FeatureFlagBuilder Variations(params JToken[] variations)
        {
            return Variations(new List<JToken>(variations));
        }

        internal FeatureFlagBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }

        internal FeatureFlagBuilder TrackEventsFallthrough(bool trackEventsFallthrough)
        {
            _trackEventsFallthrough = trackEventsFallthrough;
            return this;
        }

        internal FeatureFlagBuilder DebugEventsUntilDate(long? debugEventsUntilDate)
        {
            _debugEventsUntilDate = debugEventsUntilDate;
            return this;
        }

        internal FeatureFlagBuilder ClientSide(bool clientSide)
        {
            _clientSide = clientSide;
            return this;
        }

        internal FeatureFlagBuilder Deleted(bool deleted)
        {
            _deleted = deleted;
            return this;
        }

        internal FeatureFlagBuilder OffWithValue(JToken value)
        {
            return On(false).OffVariation(0).Variations(value);
        }

        internal FeatureFlagBuilder BooleanWithClauses(params Clause[] clauses)
        {
            return On(true).OffVariation(0)
                .Variations(new JValue(false), new JValue(true))
                .Rules(new RuleBuilder().Id("id").Variation(1).Clauses(clauses).Build());
        }
    }

    internal class RuleBuilder
    {
        private string _id = "";
        private int? _variation = null;
        private Rollout _rollout = null;
        private List<Clause> _clauses = new List<Clause>();
        private bool _trackEvents = false;

        internal Rule Build()
        {
            return new Rule(_id, _variation, _rollout, _clauses, _trackEvents);
        }

        internal RuleBuilder Id(string id)
        {
            _id = id;
            return this;
        }

        internal RuleBuilder Variation(int? variation)
        {
            _variation = variation;
            return this;
        }

        internal RuleBuilder Rollout(Rollout rollout)
        {
            _rollout = rollout;
            return this;
        }

        internal RuleBuilder Clauses(List<Clause> clauses)
        {
            _clauses = clauses;
            return this;
        }

        internal RuleBuilder Clauses(params Clause[] clauses)
        {
            return Clauses(new List<Clause>(clauses));
        }

        internal RuleBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }
    }

    internal class ClauseBuilder
    {
        private string _attribute;
        private string _op;
        private List<JValue> _values = new List<JValue>();
        private bool _negate;

        internal Clause Build()
        {
            return new Clause(_attribute, _op, _values, _negate);
        }

        public ClauseBuilder Attribute(string attribute)
        {
            _attribute = attribute;
            return this;
        }

        public ClauseBuilder Op(string op)
        {
            _op = op;
            return this;
        }

        public ClauseBuilder Values(List<JValue> values)
        {
            _values = values;
            return this;
        }

        public ClauseBuilder Values(params JValue[] values)
        {
            return Values(new List<JValue>(values));
        }

        public ClauseBuilder Negate(bool negate)
        {
            _negate = negate;
            return this;
        }

        public ClauseBuilder KeyIs(string key)
        {
            return Attribute("key").Op("in").Values(new JValue(key));
        }

        public static Clause ShouldMatchUser(User user)
        {
            return new ClauseBuilder().KeyIs(user.Key).Build();
        }

        public static Clause ShouldNotMatchUser(User user)
        {
            return new ClauseBuilder().KeyIs(user.Key).Negate(true).Build();
        }
    }
}
