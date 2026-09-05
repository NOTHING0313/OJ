using System.Text;
using OnlineJudge.Application.Common;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Problems;

internal static class ChoiceProblemDefinitionValidator
{
    public const int MaxQuestions = 50;
    public const int MaxOptionsPerQuestion = 10;
    public const int MaxQuestionBytes = 16 * 1024;
    public const int MaxOptionBytes = 4 * 1024;
    public const int MaxExplanationBytes = 16 * 1024;
    public const int MaxDefinitionBytes = 512 * 1024;
    public const int MaxQuestionScore = 1_000;
    public const int MaxTotalScore = 10_000;

    public static Result Validate(
        IReadOnlyCollection<ProblemChoiceQuestion> questions,
        ChoiceAnswerRevealPolicy? revealPolicy,
        DateTimeOffset? revealAt,
        bool requireComplete)
    {
        if (questions.Count > MaxQuestions)
        {
            return Result.Failure($"A choice problem cannot contain more than {MaxQuestions} questions.");
        }

        if (requireComplete && questions.Count == 0)
        {
            return Result.Failure("A choice problem must contain at least one question before it can be published.");
        }

        if (!revealPolicy.HasValue || !Enum.IsDefined(revealPolicy.Value))
        {
            if (requireComplete) return Result.Failure("A valid answer reveal policy is required.");
            if (revealAt is not null) return Result.Failure("A reveal time requires a valid answer reveal policy.");
        }
        else if (revealPolicy == ChoiceAnswerRevealPolicy.AtScheduledTime && revealAt is null)
        {
            return Result.Failure("Scheduled answer reveal requires a UTC reveal time.");
        }

        if (revealPolicy == ChoiceAnswerRevealPolicy.AfterSubmission && revealAt is not null)
        {
            return Result.Failure("After-submission reveal cannot specify a reveal time.");
        }

        long totalBytes = 0;
        var totalScore = 0;
        foreach (var question in questions.OrderBy(question => question.Order))
        {
            if (!Enum.IsDefined(question.SelectionMode))
            {
                return Result.Failure("Unsupported choice selection mode.");
            }

            if (question.Score < 1 || question.Score > MaxQuestionScore)
            {
                return Result.Failure($"Choice question score must be between 1 and {MaxQuestionScore}.");
            }

            if (Utf8Bytes(question.StemMarkdown) > MaxQuestionBytes)
            {
                return Result.Failure($"Choice question text exceeds the {MaxQuestionBytes}-byte UTF-8 limit.");
            }

            if (Utf8Bytes(question.ExplanationMarkdown) > MaxExplanationBytes)
            {
                return Result.Failure($"Choice explanation exceeds the {MaxExplanationBytes}-byte UTF-8 limit.");
            }

            var options = question.Options.Where(option => !option.IsDeleted).OrderBy(option => option.Order).ToList();
            if (options.Count > MaxOptionsPerQuestion || (requireComplete && options.Count < 2))
            {
                return Result.Failure($"A published choice question must contain between 2 and {MaxOptionsPerQuestion} options.");
            }

            if (requireComplete && string.IsNullOrWhiteSpace(question.StemMarkdown))
            {
                return Result.Failure("Published choice questions require question text.");
            }

            foreach (var option in options)
            {
                if (Utf8Bytes(option.ContentMarkdown) > MaxOptionBytes)
                {
                    return Result.Failure($"Choice option text exceeds the {MaxOptionBytes}-byte UTF-8 limit.");
                }

                if (requireComplete && string.IsNullOrWhiteSpace(option.ContentMarkdown))
                {
                    return Result.Failure("Published choice options require content.");
                }

                totalBytes += Utf8Bytes(option.ContentMarkdown);
            }

            var correctCount = options.Count(option => option.IsCorrect);
            if (requireComplete && question.SelectionMode == ChoiceSelectionMode.Single && correctCount != 1)
            {
                return Result.Failure("A single-choice question must have exactly one correct option.");
            }

            if (requireComplete && question.SelectionMode == ChoiceSelectionMode.Multiple && correctCount < 1)
            {
                return Result.Failure("A multiple-choice question must have at least one correct option.");
            }

            totalScore = checked(totalScore + question.Score);
            totalBytes += Utf8Bytes(question.StemMarkdown) + Utf8Bytes(question.ExplanationMarkdown);
        }

        if (totalScore > MaxTotalScore)
        {
            return Result.Failure($"Choice problem total score cannot exceed {MaxTotalScore}.");
        }

        return totalBytes > MaxDefinitionBytes
            ? Result.Failure($"Choice definition exceeds the {MaxDefinitionBytes}-byte UTF-8 limit.")
            : Result.Success();
    }

    private static int Utf8Bytes(string? value) => Encoding.UTF8.GetByteCount(value ?? string.Empty);
}
