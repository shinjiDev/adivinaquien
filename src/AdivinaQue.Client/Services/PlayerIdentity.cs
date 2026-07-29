using Microsoft.JSInterop;

namespace AdivinaQue.Client.Services;

/// <summary>
/// El PlayerId lo genera el cliente y persiste en localStorage — el servidor solo
/// acepta el GUID que reciba, nunca lo asigna (ver skill realtime-contract).
/// </summary>
public sealed class PlayerIdentity
{
    private const string StorageKey = "adivinaque.playerId";

    private readonly IJSRuntime _js;
    private Guid? _playerId;

    public PlayerIdentity(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<Guid> GetOrCreateAsync()
    {
        if (_playerId is not null)
        {
            return _playerId.Value;
        }

        var stored = await _js.InvokeAsync<string?>("blazorInterop.getItem", StorageKey);
        if (Guid.TryParse(stored, out var parsed))
        {
            _playerId = parsed;
            return parsed;
        }

        var created = Guid.NewGuid();
        await _js.InvokeVoidAsync("blazorInterop.setItem", StorageKey, created.ToString());
        _playerId = created;
        return created;
    }
}
