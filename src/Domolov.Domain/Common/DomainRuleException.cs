namespace Domolov.Domain.Common;

/// <summary>A request would break a domain rule; surfaces as a validation problem.</summary>
public sealed class DomainRuleException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
