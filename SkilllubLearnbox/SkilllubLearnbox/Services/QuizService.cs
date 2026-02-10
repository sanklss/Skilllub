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
}