namespace KIScheduler.Core.Domain;

public sealed record PlatformId
{
    public PlatformId(string value) => Value = DomainValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record ModelId
{
    public ModelId(string value) => Value = DomainValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record EffortLevel
{
    public EffortLevel(string value) => Value = DomainValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record PromptPath
{
    public PromptPath(string value) => Value = DomainValidation.Required(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct WorkItemPriority
{
    public const int Minimum = 0;
    public const int Maximum = 100;

    public WorkItemPriority(int value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Die Priorität muss zwischen {Minimum} und {Maximum} liegen.");
        }

        Value = value;
    }

    public int Value { get; }
}

public readonly record struct UsagePercent
{
    public UsagePercent(decimal value)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Der Prozentwert muss zwischen 0 und 100 liegen.");
        }

        Value = value;
    }

    public decimal Value { get; }
    public override string ToString() => $"{Value:0.##} %";
}
