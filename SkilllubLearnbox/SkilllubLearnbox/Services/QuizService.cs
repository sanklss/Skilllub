using Microsoft.Extensions.Logging;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SkilllubLearnbox.Services;

public class QuizService
{
    private readonly ILogger<QuizService> _logger;
    private readonly Supabase.Client _client;
    private readonly ProgressService _progressService;

    public QuizService(
        ILogger<QuizService> logger,
        Supabase.Client client,
        ProgressService progressService)
    {
        _logger = logger;
        _client = client;
        _progressService = progressService;
    }

    public async Task<(bool Success, List<QuizQuestionDto>? Questions, string? Error)> GetQuizQuestionsAsync(string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();

            var questions = response.Models?
                .Select(q => new QuizQuestionDto
                {
                    Id = q.Id,
                    LessonId = q.LessonId,
                    QuestionText = q.QuestionText,
                    Option1 = q.Option1,
                    Option2 = q.Option2,
                    Option3 = q.Option3,
                    Option4 = q.Option4,
                    CorrectOption = q.CorrectOption,
                    Explanation = q.Explanation ?? ""
                })
                .ToList() ?? new List<QuizQuestionDto>();

            return (true, questions, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quiz questions for lesson {LessonId}", lessonId);
            return (false, null, ex.Message);
        }
    }

    public async Task<QuizResultDto> SubmitQuizAnswersAsync(string userId, string lessonId, List<QuizAnswerDto> answers)
    {
        try
        {
            _logger.LogInformation("📝 Submitting quiz for lesson {LessonId}, user {UserId}", lessonId, userId);

            var (success, questions, error) = await GetQuizQuestionsAsync(lessonId);

            if (!success || questions == null || questions.Count == 0)
            {
                return new QuizResultDto
                {
                    Score = 0,
                    TotalQuestions = 0,
                    CorrectAnswers = 0,
                    IsPassed = false,
                    Message = "Вопросы не найдены",
                    QuestionResults = new List<QuestionResultDto>()
                };
            }

            int correctCount = 0;
            var questionResults = new List<QuestionResultDto>();

            foreach (var answer in answers)
            {
                var question = questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question == null) continue;

                bool isCorrect = question.CorrectOption == answer.UserAnswer;
                if (isCorrect) correctCount++;

                questionResults.Add(new QuestionResultDto
                {
                    QuestionId = question.Id,
                    IsCorrect = isCorrect,
                    UserAnswer = answer.UserAnswer,
                    CorrectAnswer = question.CorrectOption,
                    Explanation = question.Explanation ?? ""
                });
            }

            double score = (double)correctCount / questions.Count * 100;
            bool isPassed = score >= 70;

            var quizAttempt = new QuizAttempt
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                LessonId = lessonId,
                Score = score,
                IsPassed = isPassed,
                Answers = JsonSerializer.Serialize(answers),
                CreatedAt = DateTime.UtcNow
            };

            await _client.From<QuizAttempt>().Insert(quizAttempt);

            if (isPassed)
            {
                _logger.LogInformation("✅ Quiz passed for lesson {LessonId}, marking quiz as completed", lessonId);
                await _progressService.MarkQuizAsCompletedAsync(userId, lessonId, (int)score);
            }

            return new QuizResultDto
            {
                Score = (int)score,
                TotalQuestions = questions.Count,
                CorrectAnswers = correctCount,
                IsPassed = isPassed,
                QuestionResults = questionResults,
                Message = isPassed ? "✅ Тест пройден!" : "❌ Тест не пройден. Попробуйте еще раз."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error submitting quiz");
            return new QuizResultDto
            {
                Score = 0,
                TotalQuestions = 0,
                CorrectAnswers = 0,
                IsPassed = false,
                Message = $"Ошибка сервера: {ex.Message}",
                QuestionResults = new List<QuestionResultDto>()
            };
        }
    }

    public async Task<bool> CheckIfLessonHasQuiz(string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();

            return response.Models?.Any() ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if lesson has quiz");
            return false;
        }
    }
}