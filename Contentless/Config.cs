using Newtonsoft.Json;

namespace Contentless {
    public class Config {

        [JsonProperty(PropertyName = "exclude")]
        public string[] ExcludedFiles = {"bin/", "obj/"};

        [JsonProperty(PropertyName = "logSkipped")]
        public bool LogSkipped = true;

    }
}