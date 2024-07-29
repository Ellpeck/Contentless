using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Contentless;

public class Config {

    [JsonProperty("exclude")]
    public string[] ExcludedFiles = {"bin/", "obj/"};

    [JsonProperty("logSkipped")]
    public bool LogSkipped = true;

    [JsonProperty("overrides")]
    public Dictionary<string, Override> Overrides = new();
    
    [JsonProperty("references")] 
    public string[] References = Array.Empty<string>();
}

public class Override {

    [JsonProperty("importer")]
    public string Importer;
    [JsonProperty("processor")]
    public string Processor;
    [JsonProperty("processorParams")]
    public Dictionary<string, string> ProcessorParams = new();
    [JsonProperty("copy")]
    public bool Copy;

}
