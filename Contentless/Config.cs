using System.Collections.Generic;
using Newtonsoft.Json;

namespace Contentless {
    public class Config {

        [JsonProperty(PropertyName = "exclude")]
        public string[] ExcludedFiles = {"bin/", "obj/"};

        [JsonProperty(PropertyName = "logSkipped")]
        public bool LogSkipped = true;

        [JsonProperty(PropertyName = "overrides")]
        public Dictionary<string, string> Overrides = new Dictionary<string, string>();

    }
}