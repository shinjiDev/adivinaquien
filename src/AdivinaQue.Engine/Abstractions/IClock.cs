namespace AdivinaQue.Engine.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
