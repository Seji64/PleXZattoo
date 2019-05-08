namespace PleXZattoo
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Streams
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("stream")]
        public Stream Stream { get; set; }

        [JsonProperty("register_timeshift_allowed")]
        public bool RegisterTimeshiftAllowed { get; set; }

        [JsonProperty("register_timeshift")]
        public string RegisterTimeshift { get; set; }

        [JsonProperty("csid")]
        public string Csid { get; set; }

        [JsonProperty("unregistered_timeshift")]
        public string UnregisteredTimeshift { get; set; }
    }

    public partial class Stream
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("rb_url")]
        public Uri RbUrl { get; set; }

        [JsonProperty("quality")]
        public string Quality { get; set; }

        [JsonProperty("watch_urls")]
        public List<WatchUrl> WatchUrls { get; set; }

        [JsonProperty("teletext_url")]
        public Uri TeletextUrl { get; set; }
    }

    public partial class WatchUrl
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("maxrate")]
        public long Maxrate { get; set; }

        [JsonProperty("audio_channel")]
        public string AudioChannel { get; set; }
    }

    public partial class Streams
    {
        public static Streams FromJson(string json) => JsonConvert.DeserializeObject<Streams>(json, PleXZattoo.Converter.Settings);
    }
}
