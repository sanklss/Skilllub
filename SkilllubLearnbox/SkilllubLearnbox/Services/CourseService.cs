using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Supabase;

namespace SkilllubLearnbox.Services;
public class CourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly Supabase.Client _client;
    private readonly IMemoryCache _cache;

    public CourseService(ILogger<CourseService> logger, Supabase.Client client, IMemoryCache cache)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
    }

    public async Task<List<CourseDto>> GetAllCoursesAsync()
    {
        try
        {
            const string cacheKey = "all_courses";

            if (_cache.TryGetValue(cacheKey, out List<CourseDto> cachedCourses))
            {
                _logger.LogInformation("Курсы загружены из кэша");
                return cachedCourses;
            }

            _logger.LogInformation("Загрузка курсов из базы данных");
            await _client.InitializeAsync();

            var response = await _client.From<Course>().Get();
            var courses = response.Models?.ToList() ?? new List<Course>();

            var courseDtos = courses.Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                DifficultyLevel = c.DifficultyLevel,
                IsPublished = c.IsPublished
            }).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                .SetPriority(CacheItemPriority.Normal);

            _cache.Set(cacheKey, courseDtos, cacheOptions);
            _logger.LogInformation("Курсы сохранены в кэш на 30 минут");

            return courseDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов");
            return new List<CourseDto>();
        }
    }

    public async Task<CourseDto?> GetCourseByIdAsync(string courseId)
    {
        try
        {
            var cacheKey = $"course_{courseId}";

            if (_cache.TryGetValue(cacheKey, out CourseDto cachedCourse))
            {
                _logger.LogInformation("Курс {CourseId} загружен из кэша", courseId);
                return cachedCourse;
            }

            _logger.LogInformation("Загрузка курса {CourseId} из базы данных", courseId);
            await _client.InitializeAsync();

            var response = await _client.From<Course>().Get();
            var course = response.Models?.FirstOrDefault(c => c.Id == courseId);

            if (course == null) return null;

            var courseDto = new CourseDto
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                DifficultyLevel = course.DifficultyLevel,
                IsPublished = course.IsPublished
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

            _cache.Set(cacheKey, courseDto, cacheOptions);

            return courseDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курса {CourseId}", courseId);
            return null;
        }
    }

    public async Task<List<ModuleDto>> GetCourseModulesAsync(string courseId)
    {
        try
        {
            var cacheKey = $"modules_{courseId}";

            if (_cache.TryGetValue(cacheKey, out List<ModuleDto> cachedModules))
            {
                _logger.LogInformation("Модули курса {CourseId} загружены из кэша", courseId);
                return cachedModules;
            }

            _logger.LogInformation("Загрузка модулей курса {CourseId} из базы данных", courseId);
            await _client.InitializeAsync();

            var response = await _client.From<Module>().Get();
            var modules = response.Models?
                .Where(m => m.CourseId == courseId)
                .OrderBy(m => m.ModuleOrder)
                .ToList() ?? new List<Module>();

            var moduleDtos = modules.Select(m => new ModuleDto
            {
                Id = m.Id,
                CourseId = m.CourseId,
                Title = m.Title,
                Description = m.Description,
                Order = m.ModuleOrder
            }).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(20));

            _cache.Set(cacheKey, moduleDtos, cacheOptions);

            return moduleDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модулей курса {CourseId}", courseId);
            return new List<ModuleDto>();
        }
    }

    public async Task<List<LessonDto>> GetModuleLessonsAsync(string moduleId)
    {
        try
        {
            var cacheKey = $"lessons_{moduleId}";

            if (_cache.TryGetValue(cacheKey, out List<LessonDto> cachedLessons))
            {
                _logger.LogInformation("Уроки модуля {ModuleId} загружены из кэша", moduleId);
                return cachedLessons;
            }

            _logger.LogInformation("Загрузка уроков модуля {ModuleId} из базы данных", moduleId);
            await _client.InitializeAsync();

            var response = await _client.From<Lesson>().Get();
            var lessons = response.Models?
                .Where(l => l.ModuleId == moduleId)
                .OrderBy(l => l.LessonOrder)
                .ToList() ?? new List<Lesson>();

            var lessonDtos = lessons.Select(l => new LessonDto
            {
                Id = l.Id,
                ModuleId = l.ModuleId,
                Title = l.Title,
                Description = l.Description,
                Content = l.Content,
                Order = l.LessonOrder,
                Difficulty = l.Difficulty
            }).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

            _cache.Set(cacheKey, lessonDtos, cacheOptions);

            return lessonDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении уроков модуля {ModuleId}", moduleId);
            return new List<LessonDto>();
        }
    }

    public async Task<LessonDto?> GetLessonByIdAsync(string lessonId)
    {
        try
        {
            var cacheKey = $"lesson_{lessonId}";

            if (_cache.TryGetValue(cacheKey, out LessonDto cachedLesson))
            {
                _logger.LogInformation("Урок {LessonId} загружен из кэша", lessonId);
                return cachedLesson;
            }

            _logger.LogInformation("Загрузка урока {LessonId} из базы данных", lessonId);
            await _client.InitializeAsync();

            var response = await _client.From<Lesson>().Get();
            var lesson = response.Models?.FirstOrDefault(l => l.Id == lessonId);

            if (lesson == null) return null;

            var lessonDto = new LessonDto
            {
                Id = lesson.Id,
                ModuleId = lesson.ModuleId,
                Title = lesson.Title,
                Description = lesson.Description,
                Content = lesson.Content,
                Order = lesson.LessonOrder,
                Difficulty = lesson.Difficulty
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

            _cache.Set(cacheKey, lessonDto, cacheOptions);

            return lessonDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении урока {LessonId}", lessonId);
            return null;
        }
    }

    public async Task<CodeTemplateDto?> GetLessonCodeTemplateAsync(string lessonId, string languageId)
    {
        try
        {
            var cacheKey = $"template_{lessonId}_{languageId}";

            if (_cache.TryGetValue(cacheKey, out CodeTemplateDto cachedTemplate))
            {
                _logger.LogInformation("Шаблон кода {LessonId} загружен из кэша", lessonId);
                return cachedTemplate;
            }

            _logger.LogInformation("Загрузка шаблона кода для урока {LessonId} из базы данных", lessonId);
            await _client.InitializeAsync();

            var response = await _client.From<CodeTemplate>().Get();
            var template = response.Models?
                .FirstOrDefault(t => t.LessonId == lessonId && t.LanguageId == languageId);

            if (template == null) return null;

            var templateDto = new CodeTemplateDto
            {
                Id = template.Id,
                LessonId = template.LessonId,
                LanguageId = template.LanguageId,
                TemplateCode = template.TemplateCode,
                StarterCode = template.StarterCode,
                SolutionCode = template.SolutionCode
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

            _cache.Set(cacheKey, templateDto, cacheOptions);

            return templateDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении шаблона кода для урока {LessonId}", lessonId);
            return null;
        }
    }

    public void ClearCoursesCache()
    {
        _cache.Remove("all_courses");
        _logger.LogInformation("Кэш курсов очищен");
    }
}