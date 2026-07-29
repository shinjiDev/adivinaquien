namespace AdivinaQue.Server.Rooms;

/// <summary>
/// Código de sala de 6 caracteres, alfabeto sin ambigüedades:
/// <c>ABCDEFGHJKLMNPQRSTUVWXYZ23456789</c> (sin 0 O 1 I).
/// </summary>
public static class RoomCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    public static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
