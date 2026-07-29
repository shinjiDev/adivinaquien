namespace AdivinaQue.Engine.Abstractions;

public sealed class SeededRandom : ISeededRandom
{
    private readonly Random _random;

    public SeededRandom(int seed)
    {
        _random = new Random(seed);
    }

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
