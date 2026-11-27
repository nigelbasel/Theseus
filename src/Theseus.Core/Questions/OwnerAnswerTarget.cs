namespace Theseus.Core.Questions;

/// <summary>The card an answer on the board goes to (2.4e).</summary>
/// <param name="CardKey">The card's key.</param>
/// <param name="CardTitle">The card's title.</param>
/// <param name="SessionId">The session that asked: the card's last session.</param>
/// <param name="Reach">How the answer can reach it now.</param>
public sealed record OwnerAnswerTarget(string CardKey, string CardTitle, string SessionId, AnswerReach Reach);
