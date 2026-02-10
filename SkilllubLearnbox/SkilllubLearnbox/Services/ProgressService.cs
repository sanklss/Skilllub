using SkilllubLearnbox.Models;
using Microsoft.Extensions.Logging;
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

            var lessonsResponse = await _client.From<Lesson>().Get();
            var lesson = lessonsResponse.Models?.FirstOrDefault(l => l.Id == lessonId);
            if (lesson == null) return;

            var modulesResponse = await _client.From<Module>().Get();
            var module = modulesResponse.Models?.FirstOrDefault(m => m.Id == lesson.ModuleId);
            if (module == null) return;

            var courseId = module.CourseId;

            await UpdateCourseProgressAsync(userId, courseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении прогресса");
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

            var progress = 50;

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
}