namespace KIScheduler.Core.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
