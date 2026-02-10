using Microsoft.Extensions.Logging;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;

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
            await _client.InitializeAsync();
            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            return response.Models?.Any() ?? false;
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
            await _client.InitializeAsync();
            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            var userCourse = response.Models?.FirstOrDefault();
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
            await _client.InitializeAsync();

            // Получаем урок
            var lessonsResponse = await _client.From<Lesson>().Get();
            var lesson = lessonsResponse.Models?.FirstOrDefault(l => l.Id == lessonId);
            if (lesson == null) return;

            // Обновляем прогресс курса
            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse.Models?.FirstOrDefault(m => m.Id == lesson.ModuleId);
            if (module == null) return;

            await UpdateCourseProgressAsync(userId, module.CourseId);

            // Создаем или обновляем запись о прогрессе урока
            await UpdateLessonProgressAsync(userId, lessonId);
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
            var response = await _client.From<UserProgress>()
                .Where(up => up.UserId == userId && up.LessonId == lessonId)
                .Get();

            var userProgress = response.Models?.FirstOrDefault();

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
            var modulesResponse = await _client.From<Module>().Get();
            var courseModules = modulesResponse.Models?.Where(m => m.CourseId == courseId).ToList() ?? new List<Module>();

            var lessonsResponse = await _client.From<Lesson>().Get();
            var courseLessons = lessonsResponse.Models?
                .Where(l => courseModules.Any(m => m.Id == l.ModuleId))
                .ToList() ?? new List<Lesson>();

            if (courseLessons.Count == 0) return;

            // Получаем завершенные уроки пользователя
            var userProgressResponse = await _client.From<UserProgress>()
                .Where(up => up.UserId == userId && up.Completed)
                .Get();

            var completedLessonIds = userProgressResponse.Models?
                .Select(up => up.LessonId)
                .ToList() ?? new List<string>();

            // Считаем прогресс
            var completedCourseLessons = courseLessons.Count(l => completedLessonIds.Contains(l.Id));
            var progress = courseLessons.Count > 0 ? (completedCourseLessons * 100) / courseLessons.Count : 0;

            var userCourseResponse = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            var userCourse = userCourseResponse.Models?.FirstOrDefault();
            if (userCourse != null)
            {
                userCourse.Progress = progress;
                userCourse.Completed = progress >= 100;
                userCourse.LastAccessed = DateTime.UtcNow;
                await _client.From<UserCourse>().Update(userCourse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса курса");
        }
    }

    public async Task<List<UserCourse>> GetUserCoursesAsync(string userId)
    {
        try
        {
            await _client.InitializeAsync();
            var response = await _client.From<UserCourse>()
                .Where(x => x.UserId == userId)
                .Get();

            return response.Models?.ToList() ?? new List<UserCourse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении курсов пользователя");
            return new List<UserCourse>();
        }
    }

    public async Task<List<Course>> GetUserCoursesWithDetailsAsync(string userId)
    {
        try
        {
            var userCourses = await GetUserCoursesAsync(userId);
            if (!userCourses.Any()) return new List<Course>();

            await _client.InitializeAsync();
            var coursesResponse = await _client.From<Course>().Get();
            var allCourses = coursesResponse.Models?.ToList() ?? new List<Course>();

            var userCourseIds = userCourses.Select(uc => uc.CourseId).ToList();
            return allCourses.Where(c => userCourseIds.Contains(c.Id)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении деталей курсов пользователя");
            return new List<Course>();
        }
    }

    // ===================== НОВЫЕ МЕТОДЫ ДЛЯ МОДУЛЕЙ =====================

    public async Task<bool> IsModuleAccessibleAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            // Получаем модуль
            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse.Models?.FirstOrDefault(m => m.Id == moduleId);
            if (module == null) return false;

            // Проверяем, записан ли пользователь на курс
            var isEnrolled = await IsUserEnrolledInCourseAsync(userId, module.CourseId);
            if (!isEnrolled) return false;

            // Если это первый модуль, он доступен
            var courseModulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == module.CourseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse.Models?.ToList() ?? new List<Module>();
            var firstModule = courseModules.FirstOrDefault();

            if (firstModule?.Id == moduleId) return true;

            // Проверяем порядок модулей
            var currentModuleIndex = courseModules.FindIndex(m => m.Id == moduleId);
            if (currentModuleIndex <= 0) return true;

            // Проверяем, завершен ли предыдущий модуль
            var previousModule = courseModules[currentModuleIndex - 1];
            var isPreviousModuleCompleted = await IsModuleCompletedAsync(userId, previousModule.Id);

            return isPreviousModuleCompleted;
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
            await _client.InitializeAsync();

            var response = await _client.From<UserModuleProgress>()
                .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                .Get();

            var progress = response.Models?.FirstOrDefault();

            // Если записи нет, создаем ее
            if (progress == null)
            {
                // Получаем модуль для course_id
                var moduleResponse = await _client.From<Module>().Get();
                var module = moduleResponse.Models?.FirstOrDefault(m => m.Id == moduleId);

                if (module == null) return false;

                progress = new UserModuleProgress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    ModuleId = moduleId,
                    CourseId = module.CourseId,
                    IsCompleted = false
                };

                await _client.From<UserModuleProgress>().Insert(progress);
                return false;
            }

            return progress.IsCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке завершения модуля");
            return false;
        }
    }

    public async Task CompleteModuleAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            // Получаем модуль
            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse.Models?.FirstOrDefault(m => m.Id == moduleId);
            if (module == null) return;

            // Проверяем, все ли уроки модуля завершены
            var completedLessons = await GetCompletedLessonsInModuleAsync(userId, moduleId);
            var totalLessons = await GetTotalLessonsInModuleAsync(moduleId);

            // Если все уроки завершены, отмечаем модуль как завершенный
            if (totalLessons > 0 && completedLessons.Count >= totalLessons)
            {
                var progressResponse = await _client.From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == moduleId)
                    .Get();

                var progress = progressResponse.Models?.FirstOrDefault();

                if (progress != null)
                {
                    progress.IsCompleted = true;
                    progress.CompletedAt = DateTime.UtcNow;
                    await _client.From<UserModuleProgress>().Update(progress);

                    _logger.LogInformation("Модуль {ModuleId} завершен пользователем {UserId}", moduleId, userId);

                    // Открываем следующий модуль
                    await UnlockNextModuleAsync(userId, module);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при завершении модуля");
        }
    }

    private async Task<List<string>> GetCompletedLessonsInModuleAsync(string userId, string moduleId)
    {
        try
        {
            await _client.InitializeAsync();

            var lessonsResponse = await _client.From<Lesson>().Get();
            var moduleLessons = lessonsResponse.Models?
                .Where(l => l.ModuleId == moduleId)
                .Select(l => l.Id)
                .ToList() ?? new List<string>();

            if (moduleLessons.Count == 0) return new List<string>();

            var userProgressResponse = await _client.From<UserProgress>()
                .Where(up => up.UserId == userId && moduleLessons.Contains(up.LessonId) && up.Completed)
                .Get();

            return userProgressResponse.Models?.Select(up => up.LessonId).ToList() ?? new List<string>();
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
            var moduleLessons = lessonsResponse.Models?
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

    private async Task UnlockNextModuleAsync(string userId, Module currentModule)
    {
        try
        {
            var courseModulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == currentModule.CourseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var courseModules = courseModulesResponse.Models?.ToList() ?? new List<Module>();
            var currentIndex = courseModules.FindIndex(m => m.Id == currentModule.Id);

            if (currentIndex >= 0 && currentIndex < courseModules.Count - 1)
            {
                var nextModule = courseModules[currentIndex + 1];

                // Создаем запись для следующего модуля (если еще не создана)
                var nextModuleProgressResponse = await _client.From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == nextModule.Id)
                    .Get();

                if (nextModuleProgressResponse.Models?.Any() == false)
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
                }

                _logger.LogInformation("Следующий модуль {ModuleId} теперь доступен", nextModule.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при разблокировке следующего модуля");
        }
    }

    public async Task CompleteModuleIfAllLessonsDoneAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            // Получаем урок
            var lessonsResponse = await _client.From<Lesson>().Get();
            var lesson = lessonsResponse.Models?.FirstOrDefault(l => l.Id == lessonId);
            if (lesson == null) return;

            // Получаем модуль
            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse.Models?.FirstOrDefault(m => m.Id == lesson.ModuleId);
            if (module == null) return;

            // Проверяем, все ли уроки модуля завершены
            var completedLessons = await GetCompletedLessonsInModuleAsync(userId, module.Id);
            var totalLessons = await GetTotalLessonsInModuleAsync(module.Id);

            // Если все уроки завершены, отмечаем модуль как завершенный
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

    public async Task InitializeModuleProgress(string userId, string courseId)
    {
        try
        {
            await _client.InitializeAsync();

            // Получаем все модули курса
            var modulesResponse = await _client.From<Module>()
                .Where(m => m.CourseId == courseId)
                .Order(m => m.ModuleOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var modules = modulesResponse.Models?.ToList() ?? new List<Module>();

            // Создаем записи прогресса для всех модулей
            foreach (var module in modules)
            {
                var existingResponse = await _client.From<UserModuleProgress>()
                    .Where(x => x.UserId == userId && x.ModuleId == module.Id)
                    .Get();

                if (existingResponse.Models?.Any() == false)
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