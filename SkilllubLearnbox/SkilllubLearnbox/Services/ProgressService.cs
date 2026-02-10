using Microsoft.Extensions.Logging;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using System.Text;

namespace SkilllubLearnbox.Services;

public class ProgressService
{
    private readonly ILogger<ProgressService> _logger;
    private readonly Supabase.Client _client;

    public ProgressService(ILogger<ProgressService> logger, Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<bool> IsUserEnrolledInCourseAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return false;

            await _client.InitializeAsync();

            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            return response?.Models?.Any() ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке записи на курс");
            return false;
        }
    }

    public async Task<bool> EnrollUserInCourseAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return false;

            if (await IsUserEnrolledInCourseAsync(userId, courseId))
            {
                _logger.LogInformation("Пользователь {UserId} уже записан на курс {CourseId}", userId, courseId);
                return true;
            }

            var userCourse = new UserCourse
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CourseId = courseId,
                EnrolledAt = DateTime.UtcNow,
                Progress = 0,
                Completed = false
            };

            await _client.InitializeAsync();
            await _client.From<UserCourse>().Insert(userCourse);

            _logger.LogInformation("Пользователь {UserId} успешно записан на курс {CourseId}", userId, courseId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при записи пользователя на курс");
            return false;
        }
    }

    public async Task<int> GetUserCourseProgressAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return 0;

            await _client.InitializeAsync();

            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            var userCourse = response?.Models?.FirstOrDefault();
            return userCourse?.Progress ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении прогресса курса");
            return 0;
        }
    }

    public async Task UpdateUserProgressAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var lessonsResponse = await _client.From<Lesson>().Get();
            var lesson = lessonsResponse?.Models?.FirstOrDefault(l => l.Id == lessonId);

            if (lesson == null)
            {
                _logger.LogWarning("Урок {LessonId} не найден", lessonId);
                return;
            }

            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse?.Models?.FirstOrDefault(m => m.Id == lesson.ModuleId);

            if (module == null)
            {
                _logger.LogWarning("Модуль для урока {LessonId} не найден", lessonId);
                return;
            }

            await UpdateCourseProgressAsync(userId, module.CourseId);

            await UpdateLessonProgressAsync(userId, lessonId);

            await CompleteModuleIfAllLessonsDoneAsync(userId, lessonId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса");
        }
    }

    private async Task UpdateLessonProgressAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client.From<UserProgress>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            var userProgress = response?.Models?
                .FirstOrDefault(up => up.UserId != null && up.UserId == userId);

            if (userProgress == null)
            {
                userProgress = new UserProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    LessonId = lessonId,
                    Completed = true,
                    LastAttempt = DateTime.UtcNow,
                    AttemptsCount = 1
                };
                await _client.From<UserProgress>().Insert(userProgress);
            }
            else
            {
                userProgress.Completed = true;
                userProgress.LastAttempt = DateTime.UtcNow;
                userProgress.AttemptsCount++;
                await _client.From<UserProgress>().Update(userProgress);
            }

            _logger.LogInformation("Прогресс урока обновлен для пользователя {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса урока");
        }
    }

    private async Task UpdateCourseProgressAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return;

            await _client.InitializeAsync();

            var userProgressResponse = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.Completed == true)
                .Get();

            var completedLessons = userProgressResponse?.Models?
                .Where(up => !string.IsNullOrEmpty(up.LessonId))
                .Select(up => up.LessonId)
                .ToHashSet() ?? new HashSet<string>();

            var modulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Get();

            if (modulesResponse?.Models == null || !modulesResponse.Models.Any())
                return;

            var courseModuleIds = modulesResponse.Models
                .Select(m => m.Id)
                .ToList();

            var lessonsResponse = await _client
                .From<Lesson>()
                .Where(l => courseModuleIds.Contains(l.ModuleId))
                .Get();

            if (lessonsResponse?.Models == null)
                return;

            var courseLessons = lessonsResponse.Models.ToList();

            if (!courseLessons.Any())
                return;

            var completedCourseLessons = courseLessons
                .Count(l => !string.IsNullOrEmpty(l.Id) && completedLessons.Contains(l.Id));

            var progress = courseLessons.Count > 0
                ? (int)Math.Round((double)completedCourseLessons / courseLessons.Count * 100)
                : 0;

            var userCourseResponse = await _client
                .From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Single();

            if (userCourseResponse == null)
                return;

            userCourseResponse.Progress = progress;
            userCourseResponse.Completed = progress >= 100;
            userCourseResponse.LastAccessed = DateTime.UtcNow;

            await _client.From<UserCourse>().Update(userCourseResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса курса");
        }
    }

    public async Task<bool> IsLessonCompletedAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return false;

            await _client.InitializeAsync();

            var response = await _client.From<UserProgress>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            if (response?.Models == null)
                return false;

            return response.Models
                .Where(up => up.UserId != null && up.UserId == userId && up.Completed)
                .Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения урока");
            return false;
        }
    }

    public async Task CheckAndUpdateUserProgress(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var response = await _client.From<UserProgress>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            var existingProgress = response?.Models?
                .FirstOrDefault(up => up.UserId != null && up.UserId == userId);

            if (existingProgress != null) return;

            var newProgress = new UserProgress
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                LessonId = lessonId,
                Completed = false,
                LastAttempt = DateTime.UtcNow,
                AttemptsCount = 0
            };

            await _client.From<UserProgress>().Insert(newProgress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании прогресса");
        }
    }

    public async Task<List<UserCourse>> GetUserCoursesAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
                return new List<UserCourse>();

            await _client.InitializeAsync();

            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId)
                .Get();

            return response?.Models?.ToList() ?? new List<UserCourse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов пользователя");
            return new List<UserCourse>();
        }
    }

    public async Task<bool> IsModuleAccessibleAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return false;

            await _client.InitializeAsync();

            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse?.Models?.FirstOrDefault(m => m.Id == moduleId);

            if (module == null) return false;

            var isEnrolled = await IsUserEnrolledInCourseAsync(userId, module.CourseId);
            if (!isEnrolled) return false;

            if (module.ModuleOrder == 1) return true;

            var courseModulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == module.CourseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse?.Models?.ToList() ?? new List<Module>();

            var currentModuleIndex = courseModules.FindIndex(m => m.Id == moduleId);
            if (currentModuleIndex <= 0) return true;

            var previousModule = courseModules[currentModuleIndex - 1];
            return await IsModuleCompletedAsync(userId, previousModule.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке доступности модуля");
            return false;
        }
    }

    public async Task<bool> IsModuleCompletedAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return false;

            await _client.InitializeAsync();

            var response = await _client.From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Single();

            if (response != null)
                return response.IsCompleted;

            return await CheckModuleCompletionByLessonsAsync(userId, moduleId);
        }
        catch
        {

            return await CheckModuleCompletionByLessonsAsync(userId, moduleId);
        }
    }

    private async Task<bool> CheckModuleCompletionByLessonsAsync(string userId, string moduleId)
    {
        try
        {
            var lessonsResponse = await _client.From<Lesson>()
                .Where(l => l.ModuleId == moduleId)
                .Get();

            var moduleLessons = lessonsResponse?.Models?.ToList() ?? new List<Lesson>();
            if (!moduleLessons.Any()) return false;

            var completedLessons = await GetCompletedLessonsInModuleAsync(userId, moduleId);

            bool isCompleted = completedLessons.Count >= moduleLessons.Count;

            if (isCompleted)
            {
                var moduleResponse = await _client.From<Module>().Get();
                var module = moduleResponse?.Models?.FirstOrDefault(m => m.Id == moduleId);

                if (module != null)
                {
                    var progress = new UserModuleProgress
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ModuleId = moduleId,
                        CourseId = module.CourseId,
                        IsCompleted = true,
                        CompletedAt = DateTime.UtcNow
                    };

                    await _client.From<UserModuleProgress>().Insert(progress);
                    _logger.LogInformation("Создана запись о завершении модуля {ModuleId}", moduleId);
                }
            }

            return isCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля по урокам");
            return false;
        }
    }

    public async Task CompleteModuleAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return;

            await _client.InitializeAsync();
            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse?.Models?.FirstOrDefault(m => m.Id == moduleId);

            if (module == null) return;

            var progressResponse = await _client.From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Single();

            UserModuleProgress progress;

            if (progressResponse == null)
            {
                progress = new UserModuleProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    ModuleId = moduleId,
                    CourseId = module.CourseId,
                    IsCompleted = true,
                    CompletedAt = DateTime.UtcNow
                };
                await _client.From<UserModuleProgress>().Insert(progress);
            }
            else
            {
                progress = progressResponse;
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
                await _client.From<UserModuleProgress>().Update(progress);
            }

            _logger.LogInformation("Модуль {ModuleId} завершен пользователем {UserId}", moduleId, userId);

            await UnlockNextModuleAsync(userId, module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении модуля");
        }
    }

    public async Task CompleteModuleIfAllLessonsDoneAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var lessonsResponse = await _client.From<Lesson>().Get();
            var lesson = lessonsResponse?.Models?.FirstOrDefault(l => l.Id == lessonId);

            if (lesson == null) return;

            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse?.Models?.FirstOrDefault(m => m.Id == lesson.ModuleId);

            if (module == null) return;

            var completedLessons = await GetCompletedLessonsInModuleAsync(userId, module.Id);
            var totalLessons = await GetTotalLessonsInModuleAsync(module.Id);

            _logger.LogInformation("Модуль {ModuleId}: завершено {Completed}/{Total} уроков",
                module.Id, completedLessons.Count, totalLessons);

            if (totalLessons > 0 && completedLessons.Count >= totalLessons)
            {
                await CompleteModuleAsync(userId, module.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля");
        }
    }

    private async Task UnlockNextModuleAsync(string userId, Module currentModule)
    {
        try
        {
            _logger.LogInformation("Поиск следующего модуля после {CurrentModuleId}", currentModule.Id);

            var courseModulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == currentModule.CourseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse?.Models?.ToList() ?? new List<Module>();
            var currentIndex = courseModules.FindIndex(m => m.Id == currentModule.Id);

            if (currentIndex >= 0 && currentIndex < courseModules.Count - 1)
            {
                var nextModule = courseModules[currentIndex + 1];
                _logger.LogInformation("Найден следующий модуль: {NextModuleId}", nextModule.Id);

                var nextModuleProgressResponse = await _client.From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == nextModule.Id)
                    .Single();

                if (nextModuleProgressResponse == null)
                {
                    var nextProgress = new UserModuleProgress
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ModuleId = nextModule.Id,
                        CourseId = nextModule.CourseId,
                        IsCompleted = false
                    };

                    await _client.From<UserModuleProgress>().Insert(nextProgress);

                    _logger.LogInformation("✅ Создана запись о следующем модуле {ModuleId} для пользователя {UserId}",
                        nextModule.Id, userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при разблокировке следующего модуля");
        }
    }

    private async Task<List<string>> GetCompletedLessonsInModuleAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            // Получаем все уроки модуля
            var lessonsResponse = await _client.From<Lesson>().Get();
            var moduleLessons = lessonsResponse?.Models?
                .Where(l => l.ModuleId == moduleId)
                .Select(l => l.Id)
                .ToList() ?? new List<string>();

            if (!moduleLessons.Any()) return new List<string>();

            // Получаем завершенные уроки пользователя в этом модуле
            var userProgressResponse = await _client.From<UserProgress>()
                .Where(up => up.UserId == userId && up.Completed == true)
                .Get();

            if (userProgressResponse?.Models == null)
                return new List<string>();

            return userProgressResponse.Models
                .Where(up => !string.IsNullOrEmpty(up.LessonId) && moduleLessons.Contains(up.LessonId))
                .Select(up => up.LessonId)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении завершенных уроков модуля");
            return new List<string>();
        }
    }

    private async Task<int> GetTotalLessonsInModuleAsync(string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            var lessonsResponse = await _client.From<Lesson>().Get();
            var moduleLessons = lessonsResponse?.Models?
                .Where(l => l.ModuleId == moduleId)
                .ToList() ?? new List<Lesson>();

            return moduleLessons.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении уроков модуля");
            return 0;
        }
    }

    public async Task InitializeModuleProgress(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return;

            await _client.InitializeAsync();

            var modulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == courseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var modules = modulesResponse?.Models?.ToList() ?? new List<Module>();

            foreach (var module in modules)
            {
                var existingResponse = await _client.From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == module.Id)
                    .Single();

                if (existingResponse == null)
                {
                    var progress = new UserModuleProgress
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ModuleId = module.Id,
                        CourseId = module.CourseId,
                        IsCompleted = false
                    };

                    await _client.From<UserModuleProgress>().Insert(progress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации прогресса модулей");
        }
    }
}