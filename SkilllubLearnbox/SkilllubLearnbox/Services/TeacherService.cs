using Microsoft.Extensions.Logging;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace SkilllubLearnbox.Services;

public class TeacherService
{
    private readonly ILogger<TeacherService> _logger;
    private readonly Supabase.Client _client;
    private readonly ProgressService _progressService;
    private readonly CourseService _courseService;

    public TeacherService(
        ILogger<TeacherService> logger,
        Supabase.Client client,
        ProgressService progressService,
        CourseService courseService)
    {
        _logger = logger;
        _client = client;
        _progressService = progressService;
        _courseService = courseService;
    }

    public async Task<TeacherDashboardDto> GetTeacherDashboardAsync(string teacherId)
    {
        try
        {
            var dashboard = new TeacherDashboardDto
            {
                TotalStudents = 0,
                ActiveCourses = 0,
                TotalLessonsCompleted = 0,
                Courses = new List<TeacherCourseDto>()
            };

            await _client.InitializeAsync();

            var coursesResponse = await _client
                .From<Course>()
                .Filter("created_by", Operator.Equals, teacherId)
                .Get();

            var teacherCourses = coursesResponse.Models?.ToList() ?? new List<Course>();
            dashboard.ActiveCourses = teacherCourses.Count;

            if (!teacherCourses.Any())
                return dashboard;

            var courseIds = teacherCourses.Select(c => c.Id).ToList();

            var enrollmentsResponse = await _client
                .From<UserCourse>()
                .Filter("course_id", Operator.In, courseIds)
                .Get();

            var enrollments = enrollmentsResponse.Models?.ToList() ?? new List<UserCourse>();
            var studentIds = enrollments.Select(e => e.UserId).Distinct().ToList();
            dashboard.TotalStudents = studentIds.Count;

            if (studentIds.Any())
            {
                var allUserProgressResponse = await _client
                    .From<UserProgress>()
                    .Filter("user_id", Operator.In, studentIds)
                    .Filter("completed", Operator.Equals, "true")
                    .Get();

                dashboard.TotalLessonsCompleted = allUserProgressResponse.Models?.Count ?? 0;
            }

            foreach (var course in teacherCourses)
            {
                var courseEnrollments = enrollments.Where(e => e.CourseId == course.Id).ToList();

                var modules = await _courseService.GetCourseModulesAsync(course.Id);
                var modulesCount = modules.Count;

                var lessonsCount = 0;
                foreach (var module in modules)
                {
                    var lessons = await _courseService.GetModuleLessonsAsync(module.Id);
                    lessonsCount += lessons.Count;
                }

                double avgProgress = 0;
                if (courseEnrollments.Any())
                {
                    avgProgress = courseEnrollments.Average(e => e.Progress);
                }

                dashboard.Courses.Add(new TeacherCourseDto
                {
                    Id = course.Id,
                    Title = course.Title,
                    Description = course.Description ?? "",
                    StudentCount = courseEnrollments.Count,
                    ModulesCount = modulesCount,
                    LessonsCount = lessonsCount,
                    AverageProgress = avgProgress,
                    CreatedAt = course.CreatedAt
                });
            }

            _logger.LogInformation("Дашборд для преподавателя {TeacherId}: курсов={Courses}, студентов={Students}",
                teacherId, dashboard.ActiveCourses, dashboard.TotalStudents);

            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения дашборда преподавателя {TeacherId}", teacherId);
            return new TeacherDashboardDto
            {
                TotalStudents = 0,
                ActiveCourses = 0,
                TotalLessonsCompleted = 0,
                Courses = new List<TeacherCourseDto>()
            };
        }
    }

    public async Task<List<StudentProgressDto>> GetCourseStudentsAsync(string courseId, string teacherId)
    {
        try
        {
            await _client.InitializeAsync();

            var courseResponse = await _client
                .From<Course>()
                .Filter("id", Operator.Equals, courseId)
                .Filter("created_by", Operator.Equals, teacherId)
                .Get();

            var course = courseResponse.Models?.FirstOrDefault();
            if (course == null)
            {
                _logger.LogWarning("Курс {CourseId} не найден или не принадлежит преподавателю {TeacherId}",
                    courseId, teacherId);
                return new List<StudentProgressDto>();
            }

            var enrollmentsResponse = await _client
                .From<UserCourse>()
                .Filter("course_id", Operator.Equals, courseId)
                .Get();

            var enrollments = enrollmentsResponse.Models?.ToList() ?? new List<UserCourse>();
            var studentIds = enrollments.Select(e => e.UserId).ToList();

            if (!studentIds.Any())
                return new List<StudentProgressDto>();

            var usersResponse = await _client
                .From<User>()
                .Filter("id", Operator.In, studentIds)
                .Get();

            var users = usersResponse.Models?.ToList() ?? new List<User>();
            var userDict = users.ToDictionary(u => u.Id);

            var modules = await _courseService.GetCourseModulesAsync(courseId);
            var result = new List<StudentProgressDto>();

            foreach (var enrollment in enrollments)
            {
                var user = userDict.GetValueOrDefault(enrollment.UserId);
                if (user == null) continue;

                var studentProgress = new StudentProgressDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    EnrolledAt = enrollment.EnrolledAt,
                    CourseProgress = enrollment.Progress,
                    Modules = new List<ModuleProgressDto>()
                };

                foreach (var module in modules)
                {
                    var moduleLessons = await _courseService.GetModuleLessonsAsync(module.Id);

                    var moduleProgress = new ModuleProgressDto
                    {
                        ModuleId = module.Id,
                        ModuleTitle = module.Title,
                        ModuleOrder = module.Order,
                        IsCompleted = false,
                        Lessons = new List<LessonProgressDto>()
                    };

                    foreach (var lesson in moduleLessons)
                    {
                        var progress = await _progressService.GetUserProgressAsync(user.Id, lesson.Id);
                        var requirements = await _progressService.GetLessonRequirementsAsync(lesson.Id);
                        bool hasQuiz = requirements.HasQuiz;
                        bool hasCode = requirements.HasCodeExercise;

                        var submissionsResponse = await _client
                            .From<Submission>()
                            .Filter("user_id", Operator.Equals, user.Id)
                            .Filter("lesson_id", Operator.Equals, lesson.Id)
                            .Order("created_at", Constants.Ordering.Descending)
                            .Get();

                        var submissions = submissionsResponse.Models?.ToList() ?? new List<Submission>();

                        var lessonProgress = new LessonProgressDto
                        {
                            LessonId = lesson.Id,
                            LessonTitle = lesson.Title,
                            LessonOrder = lesson.Order,
                            IsCompleted = progress?.Completed ?? false,
                            TheoryCompleted = progress?.TheoryCompleted ?? false,
                            QuizCompleted = progress?.QuizCompleted ?? false,
                            CodeCompleted = progress?.CodeCompleted ?? false,
                            BestScore = progress?.BestScore,
                            LastAttempt = progress?.LastAttempt,
                            HasQuiz = hasQuiz,
                            HasCodeExercise = hasCode,
                            Submissions = submissions.Select(s => new SubmissionDto
                            {
                                Id = s.Id,
                                Status = s.Status ?? "",
                                Score = s.Score,
                                TestsPassed = s.TestsPassed,
                                TestsTotal = s.TestsTotal,
                                CreatedAt = s.CreatedAt
                            }).ToList()
                        };

                        moduleProgress.Lessons.Add(lessonProgress);
                    }

                    moduleProgress.IsCompleted = moduleProgress.Lessons.All(l => l.IsCompleted);
                    studentProgress.Modules.Add(moduleProgress);
                }

                result.Add(studentProgress);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения студентов курса {CourseId}", courseId);
            return new List<StudentProgressDto>();
        }
    }

    public async Task<bool> PerformTeacherActionAsync(string teacherId, TeacherActionDto action)
    {
        try
        {
            await _client.InitializeAsync();

            var lessonResponse = await _client
                .From<Lesson>()
                .Filter("id", Operator.Equals, action.LessonId)
                .Get();

            var lesson = lessonResponse.Models?.FirstOrDefault();
            if (lesson == null) return false;

            var moduleResponse = await _client
                .From<Module>()
                .Filter("id", Operator.Equals, lesson.ModuleId)
                .Get();

            var module = moduleResponse.Models?.FirstOrDefault();
            if (module == null) return false;

            var courseResponse = await _client
                .From<Course>()
                .Filter("id", Operator.Equals, module.CourseId)
                .Filter("created_by", Operator.Equals, teacherId)
                .Get();

            if (courseResponse.Models == null || !courseResponse.Models.Any())
            {
                _logger.LogWarning("Преподаватель {TeacherId} не имеет доступа к уроку {LessonId}",
                    teacherId, action.LessonId);
                return false;
            }

            switch (action.Action?.ToLower())
            {
                case "complete":
                case "complete_lesson":
                    await _progressService.CompleteLessonAsync(action.UserId, action.LessonId);
                    _logger.LogInformation("Преподаватель {TeacherId} завершил урок {LessonId} для студента {UserId}",
                        teacherId, action.LessonId, action.UserId);
                    break;

                case "reset":
                case "reset_lesson":
                    await ResetStudentLessonAsync(action.UserId, action.LessonId);
                    _logger.LogInformation("Преподаватель {TeacherId} сбросил прогресс урока {LessonId} для студента {UserId}",
                        teacherId, action.LessonId, action.UserId);
                    break;

                case "mark_theory":
                    await _progressService.MarkTheoryAsCompletedAsync(action.UserId, action.LessonId);
                    break;

                case "mark_quiz":
                    await _progressService.MarkQuizAsCompletedAsync(action.UserId, action.LessonId, action.Score ?? 100);
                    break;

                case "mark_code":
                    await _progressService.MarkCodeAsCompletedAsync(action.UserId, action.LessonId, action.Score ?? 100);
                    break;

                default:
                    _logger.LogWarning("Неизвестное действие: {Action}", action.Action);
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения действия преподавателя");
            return false;
        }
    }

    private async Task ResetStudentLessonAsync(string userId, string lessonId)
    {
        try
        {
            var progress = await _progressService.GetUserProgressAsync(userId, lessonId);
            if (progress != null)
            {
                progress.Completed = false;
                progress.TheoryCompleted = false;
                progress.QuizCompleted = false;
                progress.CodeCompleted = false;
                progress.BestScore = 0;

                await _client.From<UserProgress>().Update(progress);
            }

            var submissionsResponse = await _client
                .From<Submission>()
                .Filter("user_id", Operator.Equals, userId)
                .Filter("lesson_id", Operator.Equals, lessonId)
                .Get();

            if (submissionsResponse.Models != null)
            {
                foreach (var submission in submissionsResponse.Models)
                {
                    await _client.From<Submission>().Delete(submission);
                }
            }

            _logger.LogInformation("Сброшен прогресс студента {UserId} по уроку {LessonId}", userId, lessonId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сброса прогресса студента");
        }
    }
}