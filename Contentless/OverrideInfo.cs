namespace Contentless;

public class OverrideInfo {

    public readonly string Expression;
    public readonly Override Override;

    public OverrideInfo(string expression, Override over) {
        this.Expression = expression;
        this.Override = over;
    }

}
