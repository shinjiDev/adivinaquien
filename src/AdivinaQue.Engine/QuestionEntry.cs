namespace AdivinaQue.Engine;

public sealed class QuestionEntry
{
    public QuestionEntry(Guid actionId, Guid askedByPlayerId, string text, SuggestedFrom? suggestedFrom, DateTimeOffset askedAt)
    {
        ActionId = actionId;
        AskedByPlayerId = askedByPlayerId;
        Text = text;
        SuggestedFrom = suggestedFrom;
        AskedAt = askedAt;
    }

    public Guid ActionId { get; }

    public Guid AskedByPlayerId { get; }

    public string Text { get; }

    public SuggestedFrom? SuggestedFrom { get; }

    public DateTimeOffset AskedAt { get; }

    public QuestionResolution? Resolution { get; internal set; }

    public DateTimeOffset? ResolvedAt { get; internal set; }
}
