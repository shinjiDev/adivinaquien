namespace AdivinaQue.PackTool.Validation;

public sealed record ValidationFinding(Rule Rule, Severity Severity, string Message)
{
    public static ValidationFinding Error(Rule rule, string message) => new(rule, Severity.Error, message);

    public static ValidationFinding Warning(Rule rule, string message) => new(rule, Severity.Warning, message);
}
