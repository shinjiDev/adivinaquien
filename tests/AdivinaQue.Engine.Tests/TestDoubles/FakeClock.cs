using AdivinaQue.Engine.Abstractions;

namespace AdivinaQue.Engine.Tests.TestDoubles;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start)
    {
        UtcNow = start;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}
