using System.Text.RegularExpressions;

namespace Contentless {
    public class OverrideInfo {

        public readonly Regex Regex;
        public readonly string Importer;
        public readonly string Processor;

        public OverrideInfo(Regex regex, string importer, string processor) {
            this.Regex = regex;
            this.Importer = importer;
            this.Processor = processor;
        }

    }
}