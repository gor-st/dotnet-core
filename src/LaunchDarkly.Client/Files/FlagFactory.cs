﻿using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client.Files
{
    // Creates flag or segment objects from raw JSON.  Note that the FeatureFlag and Segment
    // classes are internal to LaunchDarkly.Client, so we refer to those types indirectly via
    // VersionedDataKind; and if we want to construct a flag from scratch, we can't use the
    // constructor but instead must build some JSON and then parse that.
    internal static class FlagFactory
    {
        public static IVersionedData FlagFromJson(JToken json)
        {
            return json.ToObject(VersionedDataKind.Features.GetItemType()) as IVersionedData;
        }

        // Constructs a flag that always returns the same value. This is done by giving it a
        // single variation and setting the fallthrough variation to that.
        public static IVersionedData FlagWithValue(string key, JToken value)
        {
            var o = new JObject();
            o.Add("key", key);
            o.Add("on", true);
            var vs = new JArray();
            vs.Add(value);
            o.Add("variations", vs);
            var ft = new JObject();
            ft.Add("variation", 0);
            o.Add("fallthrough", ft);
            return FlagFromJson(o);
        }

        public static IVersionedData SegmentFromJson(JToken json)
        {
            return json.ToObject(VersionedDataKind.Segments.GetItemType()) as IVersionedData;
        }
    }
}
