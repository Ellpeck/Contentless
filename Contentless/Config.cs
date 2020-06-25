using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Contentless {
    public class Config {

        [JsonProperty("exclude")]
        public string[] ExcludedFiles = {"bin/", "obj/"};

        [JsonProperty("logSkipped")]
        public bool LogSkipped = true;

        [JsonProperty("overrides")]
        public Dictionary<string, Override> Overrides = new Dictionary<string, Override>();

    }

    public class Override {

        [JsonProperty("importer")]
        public string Importer;
        [JsonProperty("processor")]
        public string Processor;
        [JsonProperty("processorParams")]
        public Dictionary<string, string> ProcessorParams = new Dictionary<string, string>();
        [JsonProperty("copy")]
        public bool Copy;

    }
}