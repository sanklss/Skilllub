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

    public async Task<List<ModuleDto>> GetCourseModulesAsync(string courseId, string userId = null)
    {
        try
        {
            // Создаем ключ кэша с userId
            var cacheKey = $"modules_{courseId}_{userId ?? "guest"}";

            // Пытаемся получить из кэша даже для авторизованных
            if (_cache.TryGetValue(cacheKey, out List<ModuleDto> cachedModules))
            {
                _logger.LogInformation("✅ Модули курса {CourseId} загружены из кэша (пользователь: {UserId})",
                    courseId, userId ?? "гость");
                return cachedModules;
            }

            _logger.LogInformation("Загрузка модулей курса {CourseId} для пользователя {UserId}", courseId, userId);
            await _client.InitializeAsync();

            var response = await _client.From<Module>().Get();
            var modules = response.Models?
                .Where(m => m.CourseId == courseId)
                .OrderBy(m => m.ModuleOrder)
                .ToList() ?? new List<Module>();

            var moduleDtos = new List<ModuleDto>();

            // Если пользователь не авторизован - возвращаем все модули доступными
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
                // Для авторизованных - проверяем доступность
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

            // Кэшируем на разное время
            var cacheTime = string.IsNullOrEmpty(userId)
                ? TimeSpan.FromMinutes(20)
                : TimeSpan.FromMinutes(5); // Для авторизованных меньше время

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(cacheTime);

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
            var cacheKey = $"lessons_{moduleId}_{userId ?? "guest"}";

            // Пытаемся получить из кэша
            if (_cache.TryGetValue(cacheKey, out List<LessonDto> cachedLessons))
            {
                _logger.LogInformation("✅ Уроки модуля {ModuleId} загружены из кэша (пользователь: {UserId})",
                    moduleId, userId ?? "гость");
                return cachedLessons;
            }

            // Проверяем доступность модуля для авторизованных пользователей
            if (!string.IsNullOrEmpty(userId))
            {
                var isModuleAccessible = await _progressService.IsModuleAccessibleAsync(userId, moduleId);
                if (!isModuleAccessible)
                {
                    _logger.LogInformation("Модуль {ModuleId} недоступен для пользователя {UserId}", moduleId, userId);
                    return new List<LessonDto>();
                }
            }

            _logger.LogInformation("Загрузка уроков модуля {ModuleId} для пользователя {UserId}", moduleId, userId);
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

            // Кэшируем
            var cacheTime = string.IsNullOrEmpty(userId)
                ? TimeSpan.FromMinutes(15)
                : TimeSpan.FromMinutes(3);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(cacheTime);

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
            var cacheKey = $"lesson_{lessonId}_{userId ?? "guest"}";

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

            // Проверяем доступность модуля для авторизованных пользователей
            if (!string.IsNullOrEmpty(userId))
            {
                var isModuleAccessible = await _progressService.IsModuleAccessibleAsync(userId, lesson.ModuleId);
                if (!isModuleAccessible)
                {
                    _logger.LogInformation("Урок {LessonId} недоступен для пользователя {UserId}", lessonId, userId);
                    return null;
                }
            }

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
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(string.IsNullOrEmpty(userId) ? 15 : 3));

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

            // Для авторизованных пользователей проверяем доступность урока
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

            // 1. Загружаем курс (если еще не в кэше)
            tasks.Add(this.GetCourseByIdAsync(courseId));

            // 2. Загружаем модули
            tasks.Add(this.GetCourseModulesAsync(courseId, userId));

            // 3. Получаем модули для предзагрузки уроков
            var modules = await this.GetCourseModulesAsync(courseId, userId);
            var moduleIds = modules.Select(m => m.Id).ToList();

            if (moduleIds.Any())
            {
                // 4. Загружаем уроки для каждого модуля ПАРАЛЛЕЛЬНО
                var lessonTasks = moduleIds.Select(moduleId =>
                    this.GetModuleLessonsAsync(moduleId, userId)).ToList();
                tasks.AddRange(lessonTasks);
            }

            // Выполняем все задачи параллельно
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

            // Сначала пытаемся загрузить из кэша
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

            // Если все в кэше - возвращаем
            if (!lessonsToLoad.Any())
                return result;

            // Загружаем оставшиеся из БД одним запросом
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

                // Кэшируем
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