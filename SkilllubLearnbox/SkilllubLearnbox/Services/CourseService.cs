using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest;

namespace SkilllubLearnbox.Services;

public class CourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly Supabase.Client _client;
    private readonly IMemoryCache _cache;
    private readonly ProgressService _progressService;

    public CourseService(
        ILogger<CourseService> logger,
        Supabase.Client client,
        IMemoryCache cache,
        ProgressService progressService)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
        _progressService = progressService;
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

            var response = await _client
                .From<Course>()
                .Where(x => x.IsPublished == true) 
                .Get();

            var courses = response?.Models?.ToList() ?? new List<Course>();

            var courseDtos = courses.Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                DifficultyLevel = c.DifficultyLevel,
                IsPublished = c.IsPublished
            }).ToList();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

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

            var response = await _client
                .From<Course>()
                .Where(x => x.Id == courseId && x.IsPublished == true)
                .Single();

            if (response == null)
            {
                _logger.LogWarning("Курс {CourseId} не найден или не опубликован", courseId);
                return null;
            }

            var courseDto = new CourseDto
            {
                Id = response.Id,
                Title = response.Title,
                Description = response.Description,
                DifficultyLevel = response.DifficultyLevel,
                IsPublished = response.IsPublished
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

    public async Task<List<ModuleDto>> GetCourseModulesAsync(string courseId, string userId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(courseId))
            {
                _logger.LogWarning("Пустой courseId при получении модулей");
                return new List<ModuleDto>();
            }

            var cacheKey = $"modules_{courseId}_{userId ?? "guest"}";

            if (_cache.TryGetValue(cacheKey, out List<ModuleDto> cachedModules))
            {
                _logger.LogDebug("Модули из кэша для курса {CourseId}", courseId);
                return cachedModules;
            }

            _logger.LogInformation("Загрузка модулей курса {CourseId}", courseId);

            var response = await _client
                .From<Module>()
                .Where(x => x.CourseId == courseId)
                .Order(x => x.ModuleOrder, Constants.Ordering.Ascending)
                .Get();

            if (response == null || response.Models == null)
            {
                _logger.LogWarning("Модули не найдены для курса {CourseId}", courseId);
                return new List<ModuleDto>();
            }

            var modules = response.Models.ToList();
            var moduleDtos = new List<ModuleDto>();

            if (string.IsNullOrEmpty(userId))
            {
                moduleDtos = modules.Select(module => new ModuleDto
                {
                    Id = module.Id,
                    CourseId = module.CourseId,
                    Title = module.Title,
                    Description = module.Description,
                    Order = module.ModuleOrder,
                    IsAccessible = true,
                    IsCompleted = false
                }).ToList();
            }
            else
            {
                foreach (var module in modules)
                {
                    var isAccessible = await _progressService.IsModuleAccessibleAsync(userId, module.Id);
                    var isCompleted = await _progressService.IsModuleCompletedAsync(userId, module.Id);

                    moduleDtos.Add(new ModuleDto
                    {
                        Id = module.Id,
                        CourseId = module.CourseId,
                        Title = module.Title,
                        Description = module.Description,
                        Order = module.ModuleOrder,
                        IsAccessible = isAccessible,
                        IsCompleted = isCompleted
                    });
                }
            }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, moduleDtos, cacheOptions);
            return moduleDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модулей курса {CourseId}", courseId);
            return new List<ModuleDto>();
        }
    }

    public async Task<List<LessonDto>> GetModuleLessonsAsync(string moduleId, string userId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(moduleId))
            {
                _logger.LogWarning("Пустой moduleId при получении уроков");
                return new List<LessonDto>();
            }

            var cacheKey = $"lessons_{moduleId}_{userId ?? "guest"}";

            if (_cache.TryGetValue(cacheKey, out List<LessonDto> cachedLessons))
            {
                return cachedLessons;
            }

            var lessonsResponse = await _client
                .From<Lesson>()
                .Where(x => x.ModuleId == moduleId)
                .Order(x => x.LessonOrder, Constants.Ordering.Ascending)
                .Get();

            if (lessonsResponse == null || lessonsResponse.Models == null)
            {
                _logger.LogWarning("Уроки не найдены для модуля {ModuleId}", moduleId);
                return new List<LessonDto>();
            }

            var lessons = lessonsResponse.Models.ToList();
            var lessonDtos = new List<LessonDto>();

            HashSet<string> completedLessonIds = new HashSet<string>();

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var progressResponse = await _client
                        .From<UserProgress>()
                        .Where(x => x.UserId == userId && x.Completed == true)
                        .Get();

                    if (progressResponse != null && progressResponse.Models != null)
                    {
                        completedLessonIds = new HashSet<string>(
                            progressResponse.Models
                                .Where(up => !string.IsNullOrEmpty(up.LessonId))
                                .Select(up => up.LessonId)
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при получении прогресса для модуля {ModuleId}", moduleId);
                }
            }

            HashSet<string> lessonsWithQuiz = new HashSet<string>();
            try
            {
                var quizResponse = await _client
                    .From<QuizQuestion>()
                    .Select("lesson_id")
                    .Get();

                if (quizResponse != null && quizResponse.Models != null)
                {
                    lessonsWithQuiz = new HashSet<string>(
                        quizResponse.Models
                            .Where(q => !string.IsNullOrEmpty(q.LessonId))
                            .Select(q => q.LessonId)
                    );
                }
            }
            catch
            {
            }

            foreach (var lesson in lessons)
            {
                if (lesson == null) continue;

                lessonDtos.Add(new LessonDto
                {
                    Id = lesson.Id,
                    ModuleId = lesson.ModuleId,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    Content = lesson.Content,
                    Order = lesson.LessonOrder,
                    Difficulty = lesson.Difficulty,
                    IsCompleted = completedLessonIds.Contains(lesson.Id),
                    HasQuiz = lessonsWithQuiz.Contains(lesson.Id)
                });
            }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, lessonDtos, cacheOptions);
            return lessonDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении уроков модуля {ModuleId}", moduleId);
            return new List<LessonDto>();
        }
    }


    public async Task<LessonDto?> GetLessonByIdAsync(string lessonId, string userId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(lessonId))
            {
                _logger.LogWarning("Пустой lessonId при получении урока");
                return null;
            }

            var cacheKey = $"lesson_{lessonId}_{userId ?? "guest"}";

            if (_cache.TryGetValue(cacheKey, out LessonDto cachedLesson))
            {
                return cachedLesson;
            }

            var response = await _client
                .From<Lesson>()
                .Where(x => x.Id == lessonId)
                .Single();

            if (response == null)
            {
                _logger.LogWarning("Урок {LessonId} не найден", lessonId);
                return null;
            }

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var isAccessible = await _progressService.IsModuleAccessibleAsync(userId, response.ModuleId);
                    if (!isAccessible)
                    {
                        _logger.LogInformation("Урок {LessonId} недоступен для пользователя {UserId}", lessonId, userId);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка проверки доступности урока {LessonId}", lessonId);
                }
            }

            var lessonDto = new LessonDto
            {
                Id = response.Id,
                ModuleId = response.ModuleId,
                Title = response.Title,
                Description = response.Description,
                Content = response.Content,
                Order = response.LessonOrder,
                Difficulty = response.Difficulty,
                IsCompleted = false,
                HasQuiz = false
            };

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var progress = await _client
                        .From<UserProgress>()
                        .Where(x => x.UserId == userId && x.LessonId == lessonId && x.Completed == true)
                        .Single();

                    lessonDto.IsCompleted = progress != null;
                }
                catch { }
            }

            try
            {
                var quiz = await _client
                    .From<QuizQuestion>()
                    .Where(x => x.LessonId == lessonId)
                    .Single();

                lessonDto.HasQuiz = quiz != null;
            }
            catch { }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

            _cache.Set(cacheKey, lessonDto, cacheOptions);
            return lessonDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении урока {LessonId}", lessonId);
            return null;
        }
    }

    public async Task<CodeTemplateDto?> GetLessonCodeTemplateAsync(string lessonId, string languageId, string userId = null)
    {
        try
        {
            var cacheKey = $"template_{lessonId}_{languageId}_{userId ?? "guest"}";

            if (_cache.TryGetValue(cacheKey, out CodeTemplateDto cachedTemplate))
            {
                _logger.LogInformation("Шаблон кода {LessonId} загружен из кэша", lessonId);
                return cachedTemplate;
            }

            if (!string.IsNullOrEmpty(userId))
            {
                var lesson = await GetLessonByIdAsync(lessonId, userId);
                if (lesson == null)
                {
                    _logger.LogInformation("Урок {LessonId} недоступен для пользователя {UserId}", lessonId, userId);
                    return null;
                }
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

    public async Task PreloadCourseDataAsync(string courseId, string userId = null)
    {
        try
        {
            _logger.LogInformation("🚀 Предзагрузка данных курса {CourseId} для пользователя {UserId}",
                courseId, userId ?? "гость");

            var tasks = new List<Task>();

            tasks.Add(this.GetCourseByIdAsync(courseId));

            tasks.Add(this.GetCourseModulesAsync(courseId, userId));

            var modules = await this.GetCourseModulesAsync(courseId, userId);
            var moduleIds = modules.Select(m => m.Id).ToList();

            if (moduleIds.Any())
            {
                var lessonTasks = moduleIds.Select(moduleId =>
                    this.GetModuleLessonsAsync(moduleId, userId)).ToList();
                tasks.AddRange(lessonTasks);
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation("✅ Предзагрузка данных курса {CourseId} завершена", courseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при предзагрузке данных курса");
        }
    }

    public async Task<Dictionary<string, LessonDto>> GetLessonsBulkAsync(List<string> lessonIds, string userId = null)
    {
        try
        {
            if (lessonIds == null || !lessonIds.Any())
                return new Dictionary<string, LessonDto>();

            var result = new Dictionary<string, LessonDto>();
            var lessonsToLoad = new List<string>();

            foreach (var lessonId in lessonIds)
            {
                var cacheKey = $"lesson_{lessonId}_{userId ?? "guest"}";
                if (_cache.TryGetValue(cacheKey, out LessonDto cachedLesson))
                {
                    result[lessonId] = cachedLesson;
                }
                else
                {
                    lessonsToLoad.Add(lessonId);
                }
            }

            if (!lessonsToLoad.Any())
                return result;

            _logger.LogInformation("Bulk загрузка уроков: {Count} из {Total}",
                lessonsToLoad.Count, lessonIds.Count);

            await _client.InitializeAsync();

            var response = await _client.From<Lesson>().Get();
            var lessons = response.Models?
                .Where(l => lessonsToLoad.Contains(l.Id))
                .ToList() ?? new List<Lesson>();

            foreach (var lesson in lessons)
            {
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

                result[lesson.Id] = lessonDto;

                var cacheKey = $"lesson_{lesson.Id}_{userId ?? "guest"}";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(string.IsNullOrEmpty(userId) ? 15 : 3));

                _cache.Set(cacheKey, lessonDto, cacheOptions);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при массовой загрузке уроков");
            return new Dictionary<string, LessonDto>();
        }
    }

    public void ClearCoursesCache()
    {
        _cache.Remove("all_courses");
        _logger.LogInformation("Кэш курсов очищен");
    }
}