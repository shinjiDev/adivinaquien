namespace AdivinaQue.Engine.Abstractions;

public interface ISeededRandom
{
    int Next(int minInclusive, int maxExclusive);

    void Shuffle<T>(IList<T> items);
}
