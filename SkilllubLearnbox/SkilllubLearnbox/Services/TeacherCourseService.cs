using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using static Supabase.Postgrest.Constants;

namespace SkilllubLearnbox.Services;

public class TeacherCourseService
{
    private readonly ILogger<TeacherCourseService> _logger;
    private readonly Supabase.Client _client;

    public TeacherCourseService(
        ILogger<TeacherCourseService> logger,
        Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<CourseTemplateResponseDto> CreateCourseTemplateAsync(
        string teacherId,
        CreateCourseStructureDto dto)
    {
        try
        {
            await _client.InitializeAsync();

            var course = new Course
            {
                Id = Guid.NewGuid().ToString(),
                Title = dto.Title,
                Description = dto.Description,
                DifficultyLevel = dto.DifficultyLevel,
                IsPublished = false,
                CreatedBy = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            await _client.From<Course>().Insert(course);
            _logger.LogInformation("✅ Создан курс-черновик: {Title} (ID: {CourseId})", course.Title, course.Id);

            int totalLessons = 0;

            for (int i = 0; i < dto.ModulesCount; i++)
            {
                var moduleTitle = dto.Modules.Count > i && !string.IsNullOrEmpty(dto.Modules[i].Title)
                    ? dto.Modules[i].Title
                    : $"Модуль {i + 1}";

                var module = new Module
                {
                    Id = Guid.NewGuid().ToString(),
                    CourseId = course.Id,
                    Title = moduleTitle,
                    Description = $"Модуль {i + 1} курса {dto.Title}",
                    ModuleOrder = i + 1,
                    CreatedAt = DateTime.UtcNow
                };

                await _client.From<Module>().Insert(module);
                _logger.LogInformation("  📦 Создан модуль: {ModuleTitle}", module.Title);

                int lessonsCount = dto.Modules.Count > i ? dto.Modules[i].LessonsCount : 1;

                for (int j = 0; j < lessonsCount; j++)
                {
                    var lessonTitle = dto.Modules.Count > i &&
                                      dto.Modules[i].Lessons.Count > j &&
                                      !string.IsNullOrEmpty(dto.Modules[i].Lessons[j].Title)
                        ? dto.Modules[i].Lessons[j].Title
                        : $"Урок {j + 1}";

                    var lesson = new Lesson
                    {
                        Id = Guid.NewGuid().ToString(),
                        ModuleId = module.Id,
                        Title = lessonTitle,
                        Description = $"Урок {j + 1} модуля {moduleTitle}",
                        Content = "Содержание урока будет добавлено позже",
                        LessonOrder = j + 1,
                        Difficulty = "easy",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _client.From<Lesson>().Insert(lesson);
                    _logger.LogInformation("    📝 Создан урок: {LessonTitle}", lesson.Title);
                    totalLessons++;

                    var lessonTemplate = dto.Modules.Count > i &&
                                         dto.Modules[i].Lessons.Count > j
                        ? dto.Modules[i].Lessons[j]
                        : null;

                    if (lessonTemplate != null)
                    {
                        await SaveLessonMetadataAsync(lesson.Id, lessonTemplate);
                    }
                }
            }

            return new CourseTemplateResponseDto
            {
                CourseId = course.Id,
                Title = course.Title,
                ModulesCount = dto.ModulesCount,
                LessonsCount = totalLessons,
                IsDraft = true,
                Message = $"Создан черновик курса с {dto.ModulesCount} модулями и {totalLessons} уроками"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка создания шаблона курса");
            throw;
        }
    }

    private async Task SaveLessonMetadataAsync(string lessonId, LessonTemplateDto template)
    {
        try
        {
            _logger.LogInformation("      📊 Метаданные урока {LessonId}: Теория={HasTheory}, Квиз={HasQuiz}, Код={HasCode}",
                lessonId, template.HasTheory, template.HasQuiz, template.HasCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения метаданных урока");
        }
    }

    public async Task<bool> UpdateLessonQuizAsync(
        string teacherId,
        string courseId,
        string lessonId,
        QuizContentDto quiz)
    {
        try
        {
            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
                return false;

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null)
                return false;

            var quizQuestion = new QuizQuestion
            {
                Id = Guid.NewGuid().ToString(),
                LessonId = lessonId,
                QuestionText = quiz.QuestionText,
                Option1 = quiz.Option1,
                Option2 = quiz.Option2,
                Option3 = quiz.Option3,
                Option4 = quiz.Option4,
                CorrectOption = quiz.CorrectOption,
                Explanation = quiz.Explanation,
            };

            var existingQuestions = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();

            if (existingQuestions.Models != null)
            {
                foreach (var q in existingQuestions.Models)
                {
                    await _client.From<QuizQuestion>().Delete(q);
                }
            }

            await _client.From<QuizQuestion>().Insert(quizQuestion);

            _logger.LogInformation("✅ Тест для урока {LessonId} сохранен", lessonId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения теста");
            return false;
        }
    }

    public async Task<bool> UpdateLessonCodeAsync(
        string teacherId,
        string courseId,
        string lessonId,
        CodeContentDto code)
    {
        try
        {
            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
                return false;

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null)
                return false;

            var languages = await _client
                .From<ProgrammingLanguage>()
                .Where(l => l.Name.ToLower() == "python")
                .Get();

            var pythonLang = languages.Models?.FirstOrDefault();
            if (pythonLang == null)
            {
                _logger.LogError("Язык Python не найден в БД");
                return false;
            }

            var existingTemplates = await _client
                .From<CodeTemplate>()
                .Where(ct => ct.LessonId == lessonId && ct.LanguageId == pythonLang.Id)
                .Get();

            var template = existingTemplates.Models?.FirstOrDefault();

            if (template == null)
            {
                template = new CodeTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    LessonId = lessonId,
                    LanguageId = pythonLang.Id,
                    TemplateCode = code.TaskDescription,
                    StarterCode = code.StarterCode,
                    SolutionCode = code.SolutionCode,
                };
                await _client.From<CodeTemplate>().Insert(template);
            }
            else
            {
                template.TemplateCode = code.TaskDescription;
                template.StarterCode = code.StarterCode;
                template.SolutionCode = code.SolutionCode;
                await _client.From<CodeTemplate>().Update(template);
            }

            var existingTests = await _client
                .From<Test>()
                .Where(t => t.LessonId == lessonId && t.LanguageId == pythonLang.Id)
                .Get();

            if (existingTests.Models != null)
            {
                foreach (var test in existingTests.Models)
                {
                    await _client.From<Test>().Delete(test);
                }
            }

            int order = 1;
            foreach (var testCase in code.TestCases)
            {
                var test = new Test
                {
                    Id = Guid.NewGuid().ToString(),
                    LessonId = lessonId,
                    LanguageId = pythonLang.Id,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    TestOrder = order++,
                    IsHidden = testCase.IsHidden,
                    TimeoutMs = testCase.TimeoutMs,
                    Weight = testCase.Weight,
                    CreatedAt = DateTime.UtcNow
                };
                await _client.From<Test>().Insert(test);
            }

            _logger.LogInformation("✅ Кодовое задание для урока {LessonId} сохранено, добавлено тестов: {TestsCount}",
                lessonId, code.TestCases.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения кодового задания");
            return false;
        }
    }

    public async Task<bool> PublishCourseAsync(string teacherId, string courseId)
    {
        try
        {
            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
                return false;

            var modules = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Get();

            if (modules.Models == null || modules.Models.Count == 0)
                throw new Exception("Курс должен содержать хотя бы один модуль");

            course.IsPublished = true;
            await _client.From<Course>().Update(course);

            _logger.LogInformation("📢 Курс {CourseId} опубликован", courseId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка публикации курса");
            return false;
        }
    }

    public async Task<List<CourseDto>> GetDraftCoursesAsync(string teacherId)
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client
                .From<Course>()
                .Where(c => c.CreatedBy == teacherId && c.IsPublished == false)
                .Order(c => c.CreatedAt, Ordering.Descending)
                .Get();

            var drafts = response.Models?.Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description ?? "",
                DifficultyLevel = c.DifficultyLevel,
                IsPublished = c.IsPublished,
                CreatedBy = c.CreatedBy
            }).ToList() ?? new List<CourseDto>();

            return drafts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения черновиков");
            return new List<CourseDto>();
        }
    }

    public async Task<object> GetCourseStructureAsync(string teacherId, string courseId)
    {
        try
        {
            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
                return null;

            var modulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Order(m => m.ModuleOrder, Ordering.Ascending)
                .Get();

            var modules = modulesResponse.Models?.ToList() ?? new List<Module>();

            var result = new
            {
                course = new
                {
                    course.Id,
                    course.Title,
                    course.Description,
                    course.DifficultyLevel,
                    course.IsPublished
                },
                modules = new List<object>()
            };

            var pythonLang = await GetPythonLanguageId();

            foreach (var module in modules)
            {
                var lessonsResponse = await _client
                    .From<Lesson>()
                    .Where(l => l.ModuleId == module.Id)
                    .Order(l => l.LessonOrder, Ordering.Ascending)
                    .Get();

                var lessons = lessonsResponse.Models?.ToList() ?? new List<Lesson>();

                var moduleData = new
                {
                    module.Id,
                    module.Title,
                    module.ModuleOrder,
                    lessons = new List<object>()
                };

                foreach (var lesson in lessons)
                {
                    var quizResponse = await _client
                        .From<QuizQuestion>()
                        .Where(q => q.LessonId == lesson.Id)
                        .Get();

                    var hasQuiz = quizResponse.Models?.Any() ?? false;

                    var codeResponse = await _client
                        .From<CodeTemplate>()
                        .Where(ct => ct.LessonId == lesson.Id && ct.LanguageId == pythonLang)
                        .Get();

                    var hasCode = codeResponse.Models?.Any() ?? false;

                    moduleData.lessons.Add(new
                    {
                        lesson.Id,
                        lesson.Title,
                        lesson.LessonOrder,
                        hasTheory = true, 
                        hasQuiz,
                        hasCode
                    });
                }

                result.modules.Add(moduleData);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения структуры курса");
            return null;
        }
    }

    private async Task<Course?> VerifyCourseOwnership(string teacherId, string courseId)
    {
        var response = await _client
            .From<Course>()
            .Where(c => c.Id == courseId && c.CreatedBy == teacherId)
            .Get();

        return response.Models?.FirstOrDefault();
    }

    private async Task<Lesson?> VerifyLessonBelongsToCourse(string lessonId, string courseId)
    {
        var lessonResponse = await _client
            .From<Lesson>()
            .Where(l => l.Id == lessonId)
            .Get();

        var lesson = lessonResponse.Models?.FirstOrDefault();
        if (lesson == null) return null;

        var moduleResponse = await _client
            .From<Module>()
            .Where(m => m.Id == lesson.ModuleId && m.CourseId == courseId)
            .Get();

        return moduleResponse.Models?.Any() == true ? lesson : null;
    }

    private async Task<string> GetPythonLanguageId()
    {
        var languages = await _client
            .From<ProgrammingLanguage>()
            .Where(l => l.Name.ToLower() == "python")
            .Get();

        return languages.Models?.FirstOrDefault()?.Id ?? "";
    }
}