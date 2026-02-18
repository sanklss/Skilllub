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

    public ProgressService(
        ILogger<ProgressService> logger,
        Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    private async Task<(bool HasQuiz, bool HasCodeExercise)> GetLessonRequirementsAsync(string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var quizResponse = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();
            bool hasQuiz = quizResponse.Models?.Any() ?? false;

            var codeResponse = await _client
                .From<CodeTemplate>()
                .Where(ct => ct.LessonId == lessonId)
                .Get();
            bool hasCodeExercise = codeResponse.Models?.Any() ?? false;

            _logger.LogInformation("Урок {LessonId}: квиз={HasQuiz}, код={HasCodeExercise}",
                lessonId, hasQuiz, hasCodeExercise);

            return (hasQuiz, hasCodeExercise);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке требований урока {LessonId}", lessonId);
            return (false, false);
        }
    }

    private async Task<bool> IsQuizPassedAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var quizAttempts = await _client
                .From<QuizAttempt>()
                .Where(qa => qa.UserId == userId && qa.LessonId == lessonId && qa.IsPassed == true)
                .Get();

            return quizAttempts.Models?.Any() ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке прохождения квиза");
            return false;
        }
    }

    private async Task<bool> IsCodeExerciseCompletedAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var submissions = await _client
                .From<Submission>()
                .Where(s => s.UserId == userId && s.LessonId == lessonId)
                .Order(s => s.CreatedAt, Constants.Ordering.Descending)
                .Get();

            var successfulSubmission = submissions.Models?
                .FirstOrDefault(s => s.TestsPassed == s.TestsTotal && s.TestsTotal > 0);

            return successfulSubmission != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке выполнения кода");
            return false;
        }
    }

    private async Task<Module?> GetModuleByIdAsync(string moduleId)
    {
        try
        {
            var response = await _client
                .From<Module>()
                .Where(m => m.Id == moduleId)
                .Get();

            return response?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении модуля");
            return null;
        }
    }

    private async Task<Module?> GetModuleByLessonIdAsync(string lessonId)
    {
        try
        {
            var response = await _client
                .From<Lesson>()
                .Where(l => l.Id == lessonId)
                .Get();

            var lesson = response?.Models?.FirstOrDefault();
            if (lesson == null) return null;

            var moduleResponse = await _client
                .From<Module>()
                .Where(m => m.Id == lesson.ModuleId)
                .Get();

            return moduleResponse?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при получении модуля по уроку");
            return null;
        }
    }

    private async Task<(string ModuleId, bool HasQuiz, bool HasCodeExercise)?> GetLessonBasicInfoAsync(string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var lessonResponse = await _client
                .From<Lesson>()
                .Where(l => l.Id == lessonId)
                .Get();

            var lesson = lessonResponse?.Models?.FirstOrDefault();
            if (lesson == null) return null;

            var quizResponse = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();
            bool hasQuiz = quizResponse.Models?.Any() ?? false;

            bool hasCodeExercise = false;
            try
            {
                var pythonLangResponse = await _client
                    .From<ProgrammingLanguage>()
                    .Where(l => l.Name.ToLower() == "python")
                    .Get();

                var pythonLang = pythonLangResponse.Models?.FirstOrDefault();

                if (pythonLang != null)
                {
                    var codeTemplateResponse = await _client
                        .From<CodeTemplate>()
                        .Where(ct => ct.LessonId == lessonId && ct.LanguageId == pythonLang.Id)
                        .Get();
                    hasCodeExercise = codeTemplateResponse.Models?.Any() ?? false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Ошибка при проверке кодового задания: {Message}", ex.Message);
            }

            return (lesson.ModuleId, hasQuiz, hasCodeExercise);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении информации об уроке {LessonId}", lessonId);
            return null;
        }
    }
    public async Task MarkTheoryAsCompletedAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var progress = await GetOrCreateUserProgressAsync(userId, lessonId);

            if (!progress.TheoryCompleted)
            {
                progress.TheoryCompleted = true;
                progress.LastAttempt = DateTime.UtcNow;

                await _client.From<UserProgress>().Update(progress);
                _logger.LogInformation("✅ Теория урока {LessonId} отмечена как прочитанная", lessonId);

                await CheckAndUpdateLessonCompletionAsync(userId, lessonId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при отметке теории урока");
        }
    }

    public async Task MarkQuizAsCompletedAsync(string userId, string lessonId, int score = 100)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var progress = await GetOrCreateUserProgressAsync(userId, lessonId);

            if (!progress.QuizCompleted || score > progress.BestScore)
            {
                progress.QuizCompleted = true;
                progress.BestScore = Math.Max(progress.BestScore, score);
                progress.LastAttempt = DateTime.UtcNow;
                progress.AttemptsCount++;

                await _client.From<UserProgress>().Update(progress);
                _logger.LogInformation("✅ Квиз урока {LessonId} отмечен как пройденный", lessonId);

                await CheckAndUpdateLessonCompletionAsync(userId, lessonId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при отметке квиза");
        }
    }

    public async Task MarkCodeAsCompletedAsync(string userId, string lessonId, int score = 100)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var progress = await GetOrCreateUserProgressAsync(userId, lessonId);

            if (!progress.CodeCompleted || score > progress.BestScore)
            {
                progress.CodeCompleted = true;
                progress.BestScore = Math.Max(progress.BestScore, score);
                progress.LastAttempt = DateTime.UtcNow;
                progress.AttemptsCount++;

                await _client.From<UserProgress>().Update(progress);
                _logger.LogInformation("✅ Кодовое задание урока {LessonId} отмечено как выполненное", lessonId);

                await CheckAndUpdateLessonCompletionAsync(userId, lessonId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при отметке кодового задания");
        }
    }

    [Obsolete("Используйте MarkQuizAsCompletedAsync или MarkCodeAsCompletedAsync")]
    public async Task MarkPracticeAsCompletedAsync(string userId, string lessonId, int score = 100)
    {
        await MarkQuizAsCompletedAsync(userId, lessonId, score);
    }

    public async Task<UserProgress?> GetUserProgressAsync(string userId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client
                .From<UserProgress>()
                .Where(up => up.UserId == userId && up.LessonId == lessonId)
                .Get();

            return response?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при получении прогресса");
            return null;
        }
    }

    public async Task<LessonStatusDto> GetLessonStatusAsync(string userId, string lessonId)
    {
        try
        {
            var progress = await GetUserProgressAsync(userId, lessonId);
            var (hasQuiz, hasCodeExercise) = await GetLessonRequirementsAsync(lessonId);

            return new LessonStatusDto
            {
                LessonId = lessonId,
                TheoryCompleted = progress?.TheoryCompleted ?? false,
                QuizCompleted = progress?.QuizCompleted ?? false,
                CodeCompleted = progress?.CodeCompleted ?? false,
                IsCompleted = progress?.Completed ?? false,
                BestScore = progress?.BestScore ?? 0,
                AttemptsCount = progress?.AttemptsCount ?? 0,
                HasQuiz = hasQuiz,
                HasCodeExercise = hasCodeExercise
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при получении статуса урока");
            return new LessonStatusDto { LessonId = lessonId };
        }
    }

    public async Task<bool> IsLessonCompletedAsync(string userId, string lessonId)
    {
        try
        {
            var progress = await GetUserProgressAsync(userId, lessonId);
            return progress?.Completed ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при проверке завершения урока");
            return false;
        }
    }

    private async Task<UserProgress> GetOrCreateUserProgressAsync(string userId, string lessonId)
    {
        var progress = await GetUserProgressAsync(userId, lessonId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                LessonId = lessonId,
                Completed = false,
                TheoryCompleted = false,
                QuizCompleted = false,
                CodeCompleted = false,
                PracticeCompleted = false,
                LastAttempt = DateTime.UtcNow,
                AttemptsCount = 0,
                BestScore = 0,
                TimeSpentMs = 0
            };

            await _client.From<UserProgress>().Insert(progress);
            _logger.LogInformation("📝 Создана новая запись прогресса для урока {LessonId}", lessonId);
        }

        return progress;
    }

    private async Task CheckAndUpdateLessonCompletionAsync(string userId, string lessonId)
    {
        try
        {
            var progress = await GetUserProgressAsync(userId, lessonId);
            if (progress == null) return;

            var (hasQuiz, hasCodeExercise) = await GetLessonRequirementsAsync(lessonId);

            if (hasQuiz && !progress.QuizCompleted)
            {
                var quizPassed = await IsQuizPassedAsync(userId, lessonId);
                if (quizPassed)
                {
                    progress.QuizCompleted = true;
                }
            }

            if (hasCodeExercise && !progress.CodeCompleted)
            {
                var codeCompleted = await IsCodeExerciseCompletedAsync(userId, lessonId);
                if (codeCompleted)
                {
                    progress.CodeCompleted = true;
                }
            }

            bool shouldBeCompleted = progress.TheoryCompleted;

            if (hasQuiz)
            {
                shouldBeCompleted = shouldBeCompleted && progress.QuizCompleted;
            }

            if (hasCodeExercise)
            {
                shouldBeCompleted = shouldBeCompleted && progress.CodeCompleted;
            }

            _logger.LogInformation("Проверка урока {LessonId}: теория={Theory}, квиз={Quiz}, код={Code}, должен быть завершен={Should}",
                lessonId, progress.TheoryCompleted, progress.QuizCompleted, progress.CodeCompleted, shouldBeCompleted);

            if (shouldBeCompleted && !progress.Completed)
            {
                progress.Completed = true;
                progress.LastAttempt = DateTime.UtcNow;

                await _client.From<UserProgress>().Update(progress);
                _logger.LogInformation("🎉 Урок {LessonId} полностью завершен!", lessonId);

                await CheckAndCompleteModuleAsync(userId, lessonId);

                var module = await GetModuleByLessonIdAsync(lessonId);
                if (module != null)
                {
                    await UpdateCourseProgressAsync(userId, module.CourseId);
                }
            }
            else if (!shouldBeCompleted && progress.Completed)
            {
                progress.Completed = false;
                await _client.From<UserProgress>().Update(progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при проверке завершения урока");
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
            _logger.LogError(ex, "❌ Ошибка при создании прогресса");
        }
    }

    public async Task CompleteLessonAsync(string userId, string lessonId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lessonId))
                return;

            await _client.InitializeAsync();

            var progress = await GetOrCreateUserProgressAsync(userId, lessonId);

            progress.TheoryCompleted = true;
            progress.QuizCompleted = true;
            progress.CodeCompleted = true;
            progress.Completed = true;
            progress.LastAttempt = DateTime.UtcNow;
            progress.AttemptsCount++;

            await _client.From<UserProgress>().Update(progress);
            _logger.LogInformation("✅ Урок {LessonId} отмечен как завершенный (CompleteLessonAsync)", lessonId);

            var lessonInfo = await GetLessonBasicInfoAsync(lessonId);
            if (lessonInfo != null)
            {
                await CheckAndCompleteModuleAsync(userId, lessonInfo.Value.ModuleId);

                var module = await GetModuleByIdAsync(lessonInfo.Value.ModuleId);
                if (module != null && !string.IsNullOrEmpty(module.CourseId))
                {
                    await UpdateCourseProgressAsync(userId, module.CourseId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при завершении урока");
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
            _logger.LogError(ex, "❌ Ошибка при записи на курс");
            return false;
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
            _logger.LogError(ex, "❌ Ошибка при проверке записи на курс");
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

            var response = await _client
                .From<UserCourse>()
                .Where(x => x.UserId == userId && x.CourseId == courseId)
                .Get();

            return response?.Models?.FirstOrDefault()?.Progress ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при получении прогресса курса");
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
            _logger.LogError(ex, "❌ Ошибка при получении курсов пользователя");
            return new List<UserCourse>();
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
                _logger.LogInformation("📊 Прогресс курса {CourseId}: {Progress}%", courseId, progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при обновлении прогресса курса");
        }
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
                return true;
            }

            return await CheckModuleCompletionByLessonsAsync(userId, moduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при проверке завершения модуля");
            return false;
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

            var isEnrolled = await IsUserEnrolledInCourseAsync(userId, module.CourseId ?? "");
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

            return isPreviousCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при проверке доступности модуля");
            return false;
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
            _logger.LogError(ex, "❌ Ошибка при проверке и завершении модуля");
        }
    }

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
                _logger.LogWarning("🔄 Сброшен прогресс модуля {ModuleId}", moduleId);
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
                    userProgress.TheoryCompleted = false;
                    userProgress.QuizCompleted = false;
                    userProgress.CodeCompleted = false;
                    await _client.From<UserProgress>().Update(userProgress);
                }
            }

            _logger.LogWarning("🔄 Сброшены все уроки модуля {ModuleId}", moduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при сбросе прогресса модуля");
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
                return false;

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
            _logger.LogError(ex, "❌ Ошибка при проверке завершения модуля по урокам");
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
                    CourseId = module.CourseId ?? "",
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

            if (module.CourseId != null)
            {
                await UnlockNextModuleAsync(userId, module);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при создании записи о завершении модуля");
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
                        CourseId = nextModule.CourseId ?? "",
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
            _logger.LogError(ex, "❌ Ошибка при разблокировке следующего модуля");
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
                        CourseId = module.CourseId ?? "",
                        IsCompleted = false,
                        CompletedAt = null
                    };

                    await _client.From<UserModuleProgress>().Insert(progress);
                    _logger.LogInformation("📝 Инициализирован прогресс модуля {ModuleId}", module.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при инициализации прогресса модулей");
        }
    }
}