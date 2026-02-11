using Microsoft.Extensions.Logging;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    public async Task<bool> IsModuleCompletedAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return false;

            await _client.InitializeAsync();

            var moduleProgressResponse = await _client
                .From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Get();

            var moduleProgress = moduleProgressResponse?.Models?.FirstOrDefault();

            if (moduleProgress != null && moduleProgress.IsCompleted)
            {
                _logger.LogInformation("Модуль {ModuleId} помечен как завершенный в user_module_progress", moduleId);
                return true;
            }

            var isCompletedByLessons = await CheckModuleCompletionByLessonsAsync(userId, moduleId);

            if (isCompletedByLessons && moduleProgress == null)
            {
                await CreateModuleCompletionRecord(userId, moduleId);
            }

            return isCompletedByLessons;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля {ModuleId}", moduleId);
            return false;
        }
    }

    private async Task<bool> CheckModuleCompletionByLessonsAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            var lessonsResponse = await _client
                .From<Lesson>()
                .Where(l => l.ModuleId == moduleId)
                .Get();

            var moduleLessons = lessonsResponse?.Models?.ToList() ?? new List<Lesson>();

            if (!moduleLessons.Any())
            {
                _logger.LogWarning("Модуль {ModuleId} не содержит уроков", moduleId);
                return false;
            }

            var userProgressResponse = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.Completed == true)
                .Get();

            var completedLessonIds = userProgressResponse?.Models?
                .Where(up => !string.IsNullOrEmpty(up.LessonId))
                .Select(up => up.LessonId)
                .ToHashSet() ?? new HashSet<string>();

            var moduleLessonIds = moduleLessons.Select(l => l.Id).ToHashSet();

            var completedInThisModule = completedLessonIds.Intersect(moduleLessonIds).Count();

            _logger.LogInformation("Модуль {ModuleId}: завершено {Completed}/{Total} уроков",
                moduleId, completedInThisModule, moduleLessons.Count);

            return completedInThisModule >= moduleLessons.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля по урокам");
            return false;
        }
    }

    private async Task CreateModuleCompletionRecord(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            var moduleResponse = await _client
                .From<Module>()
                .Where(m => m.Id == moduleId)
                .Get();

            var module = moduleResponse?.Models?.FirstOrDefault();
            if (module == null) return;

            var existingResponse = await _client
                .From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Get();

            var existing = existingResponse?.Models?.FirstOrDefault();

            if (existing == null)
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
                _logger.LogInformation("✅ Создана запись о завершении модуля {ModuleId}", moduleId);
            }
            else if (!existing.IsCompleted)
            {
                existing.IsCompleted = true;
                existing.CompletedAt = DateTime.UtcNow;
                await _client.From<UserModuleProgress>().Update(existing);
                _logger.LogInformation("✅ Обновлена запись о завершении модуля {ModuleId}", moduleId);
            }

            await UnlockNextModuleAsync(userId, module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании записи о завершении модуля");
        }
    }

    public async Task<bool> IsModuleAccessibleAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return false;

            await _client.InitializeAsync();

            var moduleResponse = await _client
                .From<Module>()
                .Where(m => m.Id == moduleId)
                .Get();

            var module = moduleResponse?.Models?.FirstOrDefault();
            if (module == null) return false;

            var isEnrolled = await IsUserEnrolledInCourseAsync(userId, module.CourseId);
            if (!isEnrolled) return false;

            if (module.ModuleOrder == 1) return true;

            var courseModulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == module.CourseId)
                .Order(m => m.ModuleOrder, Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse?.Models?.ToList() ?? new List<Module>();

            var currentIndex = courseModules.FindIndex(m => m.Id == moduleId);
            if (currentIndex <= 0) return true;

            var previousModule = courseModules[currentIndex - 1];
            var isPreviousCompleted = await IsModuleCompletedAsync(userId, previousModule.Id);

            _logger.LogInformation("Доступ к модулю {ModuleId}: предыдущий модуль завершен = {IsCompleted}",
                moduleId, isPreviousCompleted);

            return isPreviousCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке доступности модуля");
            return false;
        }
    }

    public async Task CompleteLessonAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var lessonResponse = await _client
                .From<Lesson>()
                .Where(l => l.Id == lessonId)
                .Get();

            var lesson = lessonResponse?.Models?.FirstOrDefault();
            if (lesson == null)
            {
                _logger.LogWarning("Урок {LessonId} не найден", lessonId);
                return;
            }

            var moduleResponse = await _client
                .From<Module>()
                .Where(m => m.Id == lesson.ModuleId)
                .Get();

            var module = moduleResponse?.Models?.FirstOrDefault();
            if (module == null)
            {
                _logger.LogWarning("Модуль для урока {LessonId} не найден", lessonId);
                return;
            }

            await MarkLessonAsCompletedAsync(userId, lessonId);

            await UpdateCourseProgressAsync(userId, module.CourseId);

            await CheckAndCompleteModuleAsync(userId, module.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении урока");
        }
    }

    private async Task MarkLessonAsCompletedAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var progressResponse = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.LessonId == lessonId)
                .Get();

            var userProgress = progressResponse?.Models?.FirstOrDefault();

            if (userProgress == null)
            {
                userProgress = new UserProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    LessonId = lessonId,
                    Completed = true,
                    LastAttempt = DateTime.UtcNow,
                    AttemptsCount = 1,
                    TimeSpentMs = 0,
                    BestScore = 100
                };
                await _client.From<UserProgress>().Insert(userProgress);
                _logger.LogInformation("✅ Урок {LessonId} отмечен как завершенный", lessonId);
            }
            else if (!userProgress.Completed)
            {
                userProgress.Completed = true;
                userProgress.LastAttempt = DateTime.UtcNow;
                userProgress.AttemptsCount++;
                await _client.From<UserProgress>().Update(userProgress);
                _logger.LogInformation("✅ Урок {LessonId} отмечен как завершенный", lessonId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отметке урока как завершенного");
        }
    }

    public async Task CheckAndCompleteModuleAsync(string userId, string moduleId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(moduleId))
                return;

            var isCompleted = await CheckModuleCompletionByLessonsAsync(userId, moduleId);

            if (isCompleted)
            {
                _logger.LogInformation("✅ Модуль {ModuleId} может быть завершен", moduleId);
                await CreateModuleCompletionRecord(userId, moduleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке и завершении модуля");
        }
    }

    public async Task<bool> IsUserEnrolledInCourseAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return false;

            await _client.InitializeAsync();

            var response = await _client
                .From<UserCourse>()
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
                return true;

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

            await InitializeModuleProgressAsync(userId, courseId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при записи на курс");
            return false;
        }
    }

    public async Task InitializeModuleProgressAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return;

            await _client.InitializeAsync();

            var modulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Order(m => m.ModuleOrder, Constants.Ordering.Ascending)
                .Get();

            var modules = modulesResponse?.Models?.ToList() ?? new List<Module>();

            foreach (var module in modules)
            {
                var existingResponse = await _client
                    .From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == module.Id)
                    .Get();

                if (existingResponse?.Models?.FirstOrDefault() == null)
                {
                    var progress = new UserModuleProgress
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ModuleId = module.Id,
                        CourseId = module.CourseId,
                        IsCompleted = false,
                        CompletedAt = null
                    };

                    await _client.From<UserModuleProgress>().Insert(progress);
                    _logger.LogInformation("Инициализирован прогресс модуля {ModuleId}", module.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации прогресса модулей");
        }
    }

    private async Task UpdateCourseProgressAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return;

            await _client.InitializeAsync();

            var modulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Get();

            var courseModules = modulesResponse?.Models?.ToList() ?? new List<Module>();
            if (!courseModules.Any()) return;

            var moduleIds = courseModules.Select(m => m.Id).ToList();

            var lessonsResponse = await _client
                .From<Lesson>()
                .Where(l => moduleIds.Contains(l.ModuleId))
                .Get();

            var courseLessons = lessonsResponse?.Models?.ToList() ?? new List<Lesson>();
            if (!courseLessons.Any()) return;

            var userProgressResponse = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.Completed == true)
                .Get();

            var completedLessonIds = userProgressResponse?.Models?
                .Where(up => !string.IsNullOrEmpty(up.LessonId))
                .Select(up => up.LessonId)
                .ToHashSet() ?? new HashSet<string>();

            var courseLessonIds = courseLessons.Select(l => l.Id).ToHashSet();
            var completedInThisCourse = completedLessonIds.Intersect(courseLessonIds).Count();

            var progress = courseLessons.Count > 0
                ? (int)Math.Round((double)completedInThisCourse / courseLessons.Count * 100)
                : 0;

            var userCourseResponse = await _client
                .From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            var userCourse = userCourseResponse?.Models?.FirstOrDefault();

            if (userCourse != null)
            {
                userCourse.Progress = progress;
                userCourse.Completed = progress >= 100;
                userCourse.LastAccessed = DateTime.UtcNow;

                await _client.From<UserCourse>().Update(userCourse);
                _logger.LogInformation("Прогресс курса {CourseId}: {Progress}%", courseId, progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса курса");
        }
    }

    private async Task UnlockNextModuleAsync(string userId, Module currentModule)
    {
        try
        {
            if (currentModule?.CourseId == null) return;

            var courseModulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == currentModule.CourseId)
                .Order(m => m.ModuleOrder, Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse?.Models?.ToList() ?? new List<Module>();

            var currentIndex = courseModules.FindIndex(m => m.Id == currentModule.Id);

            if (currentIndex >= 0 && currentIndex < courseModules.Count - 1)
            {
                var nextModule = courseModules[currentIndex + 1];

                var nextModuleProgressResponse = await _client
                    .From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == nextModule.Id)
                    .Get();

                var nextModuleProgress = nextModuleProgressResponse?.Models?.FirstOrDefault();

                if (nextModuleProgress == null)
                {
                    var progress = new UserModuleProgress
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ModuleId = nextModule.Id,
                        CourseId = nextModule.CourseId,
                        IsCompleted = false,
                        CompletedAt = null
                    };

                    await _client.From<UserModuleProgress>().Insert(progress);
                    _logger.LogInformation("🔓 Модуль {ModuleId} разблокирован", nextModule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при разблокировке следующего модуля");
        }
    }


    public async Task<int> GetUserCourseProgressAsync(string userId, string courseId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(courseId))
                return 0;

            await _client.InitializeAsync();

            var response = await _client
                .From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            return response?.Models?.FirstOrDefault()?.Progress ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении прогресса курса");
            return 0;
        }
    }

    public async Task<List<UserCourse>> GetUserCoursesAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
                return new List<UserCourse>();

            await _client.InitializeAsync();

            var response = await _client
                .From<UserCourse>()
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

    public async Task<bool> IsLessonCompletedAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return false;

            await _client.InitializeAsync();

            var response = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.LessonId == lessonId && up.Completed == true)
                .Get();

            return response?.Models?.Any() ?? false;
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

            var response = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.LessonId == lessonId)
                .Get();

            if (response?.Models?.FirstOrDefault() != null)
                return;

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

    public async Task UpdateUserProgressAsync(string userId, string lessonId)
    {
        await CompleteLessonAsync(userId, lessonId);
    }

    public async Task CompleteModuleIfAllLessonsDoneAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var lessonResponse = await _client
                .From<Lesson>()
                .Where(l => l.Id == lessonId)
                .Get();

            var lesson = lessonResponse?.Models?.FirstOrDefault();
            if (lesson == null) return;

            await CheckAndCompleteModuleAsync(userId, lesson.ModuleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля");
        }
    }

    // ============ ОТЛАДОЧНЫЕ МЕТОДЫ ============

    public async Task ResetModuleProgressAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            var moduleProgressResponse = await _client
                .From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Get();

            var moduleProgress = moduleProgressResponse?.Models?.FirstOrDefault();
            if (moduleProgress != null)
            {
                await _client.From<UserModuleProgress>().Delete(moduleProgress);
                _logger.LogWarning("Сброшен прогресс модуля {ModuleId}", moduleId);
            }

            var lessonsResponse = await _client
                .From<Lesson>()
                .Where(l => l.ModuleId == moduleId)
                .Get();

            var lessonIds = lessonsResponse?.Models?.Select(l => l.Id).ToList() ?? new List<string>();

            foreach (var lessonId in lessonIds)
            {
                var userProgressResponse = await _client
                    .From<UserProgress>()
                    .Where(up => up.UserId == userId && up.LessonId == lessonId)
                    .Get();

                var userProgress = userProgressResponse?.Models?.FirstOrDefault();
                if (userProgress != null)
                {
                    userProgress.Completed = false;
                    await _client.From<UserProgress>().Update(userProgress);
                }
            }

            _logger.LogWarning("Сброшены все уроки модуля {ModuleId}", moduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе прогресса модуля");
        }
    }
}