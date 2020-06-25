using System.Text.RegularExpressions;

namespace Contentless {
    public class OverrideInfo {

        public readonly Regex Regex;
        public readonly Override Override;

        public OverrideInfo(Regex regex, Override over) {
            this.Regex = regex;
            this.Override = over;
        }

    }
}