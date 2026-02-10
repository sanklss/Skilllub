using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Supabase;

namespace SkilllubLearnbox.Services;
public class QuizService
{
    private readonly ILogger<QuizService> _logger;
    private readonly Supabase.Client _client;
    private readonly IMemoryCache _cache;

    public QuizService(ILogger<QuizService> logger, Supabase.Client client, IMemoryCache cache)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
    }

    public async Task<List<QuizQuestionDto>> GetQuizQuestionsByLessonAsync(string lessonId)
    {
        try
        {
            var cacheKey = $"quiz_questions_{lessonId}";

            if (_cache.TryGetValue(cacheKey, out List<QuizQuestionDto> cachedQuestions))
            {
                _logger.LogInformation("Вопросы для урока {LessonId} загружены из кэша", lessonId);
                return cachedQuestions;
            }

            _logger.LogInformation("Загрузка вопросов для урока {LessonId} из базы данных", lessonId);
            await _client.InitializeAsync();

            var response = await _client.From<QuizQuestion>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            var questions = response.Models?.ToList() ?? new List<QuizQuestion>();

            var questionDtos = questions.Select(q => new QuizQuestionDto
            {
                Id = q.Id,
                LessonId = q.LessonId,
                QuestionText = q.QuestionText,
                Option1 = q.Option1,
                Option2 = q.Option2,
                Option3 = q.Option3,
                Option4 = q.Option4,
                CorrectOption = q.CorrectOption,
                Explanation = q.Explanation
            }).ToList();

            _logger.LogInformation("Найдено вопросов для урока {LessonId}: {Count}", lessonId, questions.Count);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

            _cache.Set(cacheKey, questionDtos, cacheOptions);
            _logger.LogInformation("Вопросы для урока {LessonId} сохранены в кэш", lessonId);

            return questionDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении вопросов для урока {LessonId}", lessonId);
            return new List<QuizQuestionDto>();
        }
    }

    public async Task<QuizResultDto> SubmitQuizAnswersAsync(string userId, string lessonId, List<QuizAnswerDto> answers)
    {
        try
        {
            await _client.InitializeAsync();

            var questionsResponse = await _client.From<QuizQuestion>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            var questions = questionsResponse.Models?.ToList() ?? new List<QuizQuestion>();

            if (!questions.Any())
            {
                throw new Exception("Вопросы не найдены");
            }

            var questionResults = new List<QuestionResultDto>();
            int correctAnswers = 0;

            foreach (var question in questions)
            {
                var userAnswer = answers.FirstOrDefault(a => a.QuestionId == question.Id);

                var isCorrect = userAnswer != null && userAnswer.UserAnswer == question.CorrectOption;

                if (isCorrect) correctAnswers++;

                questionResults.Add(new QuestionResultDto
                {
                    QuestionId = question.Id,
                    IsCorrect = isCorrect,
                    UserAnswer = userAnswer?.UserAnswer ?? 0,
                    CorrectAnswer = question.CorrectOption,
                    Explanation = question.Explanation
                });
            }

            var score = (int)Math.Round((double)correctAnswers / questions.Count * 100);
            var isPassed = score >= 70;

            await UpdateUserQuizProgressAsync(userId, lessonId, score, correctAnswers, questions.Count);

            return new QuizResultDto
            {
                Score = score,
                TotalQuestions = questions.Count,
                CorrectAnswers = correctAnswers,
                QuestionResults = questionResults,
                IsPassed = isPassed,
                Message = isPassed
                    ? "Поздравляем! Вы успешно прошли тест."
                    : "К сожалению, вы не прошли тест. Попробуйте еще раз."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке ответов на вопросы урока {LessonId}", lessonId);
            throw;
        }
    }

    private async Task UpdateUserQuizProgressAsync(string userId, string lessonId, int score, int correctAnswers, int totalQuestions)
    {
        try
        {
            await _client.InitializeAsync();

            var progressResponse = await _client.From<UserProgress>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            var userProgress = progressResponse.Models?.FirstOrDefault();

            if (userProgress == null)
            {
                userProgress = new UserProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    LessonId = lessonId,
                    Completed = true,
                    BestScore = score,
                    AttemptsCount = 1,
                    LastAttempt = DateTime.UtcNow
                };
                await _client.From<UserProgress>().Insert(userProgress);
            }
            else
            {
                userProgress.Completed = true;
                userProgress.BestScore = Math.Max(userProgress.BestScore, score);
                userProgress.AttemptsCount++;
                userProgress.LastAttempt = DateTime.UtcNow;
                await _client.From<UserProgress>().Update(userProgress);
            }

            _logger.LogInformation("Прогресс квиза обновлен для пользователя {UserId}, урок {LessonId}, результат: {Score}%",
                userId, lessonId, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса квиза");
        }
    }
}