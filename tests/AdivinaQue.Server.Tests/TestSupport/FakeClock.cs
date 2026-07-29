using AdivinaQue.Engine.Abstractions;

namespace AdivinaQue.Server.Tests.TestSupport;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start)
    {
        UtcNow = start;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}
