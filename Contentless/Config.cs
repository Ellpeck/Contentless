using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Contentless {
    public class Config {

        [JsonProperty(PropertyName = "exclude")]
        public string[] ExcludedFiles = {"bin/", "obj/"};

        [JsonProperty(PropertyName = "logSkipped")]
        public bool LogSkipped = true;

        [JsonProperty(PropertyName = "overrides")]
        public Dictionary<string, JToken> Overrides = new Dictionary<string, JToken>();

    }
}