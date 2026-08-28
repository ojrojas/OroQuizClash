namespace OroQuizClash.Application.Features.Games.Notifications;

/// <summary>
/// Payload for QuestionPresented realtime event (SPEC-012 FR-004/FR-013).
/// Never contains IsCorrect — anti-cheat projection of Question.
/// </summary>
public sealed record QuestionPresentedPayload(
    Guid QuestionId,
    string Text,
    IReadOnlyList<QuestionOptionPayload> AnswerOptions);

public sealed record QuestionOptionPayload(
    Guid Id,
    string Text);
