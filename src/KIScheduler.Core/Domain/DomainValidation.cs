namespace KIScheduler.Core.Domain;

internal static class DomainValidation
{
    public static string Required(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    public static Guid Id(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Eine ID darf nicht leer sein.", parameterName);
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Zeitangaben müssen in UTC vorliegen.", parameterName);
        }

        return value;
    }
}
