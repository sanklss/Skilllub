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

                    var lessonTemplate = dto.Modules.Count > i &&
                                         dto.Modules[i].Lessons.Count > j
                        ? dto.Modules[i].Lessons[j]
                        : null;

                    var lesson = new Lesson
                    {
                        Id = Guid.NewGuid().ToString(),
                        ModuleId = module.Id,
                        Title = lessonTitle,
                        Description = $"Урок {j + 1} модуля {moduleTitle}",
                        Content = "Содержание урока будет добавлено позже",
                        LessonOrder = j + 1,
                        Difficulty = "easy",
                        CreatedAt = DateTime.UtcNow,
                        HasQuiz = lessonTemplate?.HasQuiz ?? false,
                        HasCode = lessonTemplate?.HasCode ?? false
                    };

                    await _client.From<Lesson>().Insert(lesson);
                    _logger.LogInformation("    📝 Создан урок: {LessonTitle}", lesson.Title);
                    totalLessons++;

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

    public async Task<string> GetLessonTheoryAsync(string teacherId, string courseId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null) return "";

            return lesson.Content ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения теории");
            return "";
        }
    }

    public async Task<bool> UpdateLessonTheoryAsync(string teacherId, string courseId, string lessonId, string content)
    {
        try
        {
            await _client.InitializeAsync();

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null) return false;

            lesson.Content = content;
            await _client.From<Lesson>().Update(lesson);

            _logger.LogInformation("✅ Теория для урока {LessonId} сохранена", lessonId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения теории");
            return false;
        }
    }

    public async Task<object> GetLessonQuizAsync(string teacherId, string courseId, string lessonId)
    {
        try
        {
            await _client.InitializeAsync();

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null) return null;

            var quizResponse = await _client
                .From<QuizQuestion>()
                .Where(q => q.LessonId == lessonId)
                .Get();

            var quiz = quizResponse.Models?.FirstOrDefault();
            if (quiz == null) return null;

            return new
            {
                quiz.Id,
                quiz.QuestionText,
                quiz.Option1,
                quiz.Option2,
                quiz.Option3,
                quiz.Option4,
                quiz.CorrectOption,
                quiz.Explanation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения теста");
            return null;
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
            _logger.LogWarning("========== СОХРАНЕНИЕ ТЕСТА ==========");
            _logger.LogWarning("LessonId: {LessonId}", lessonId);
            _logger.LogWarning("Данные теста: Вопрос='{Q}', Вариант1='{O1}'",
                quiz.QuestionText, quiz.Option1);

            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
            {
                _logger.LogWarning("❌ Курс не найден");
                return false;
            }

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null)
            {
                _logger.LogWarning("❌ Урок не найден");
                return false;
            }

            var existingQuestions = await _client
                .From<QuizQuestion>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            if (existingQuestions.Models != null && existingQuestions.Models.Any())
            {
                _logger.LogWarning("🗑️ Удаляем {Count} старых вопросов", existingQuestions.Models.Count);
                foreach (var q in existingQuestions.Models)
                {
                    await _client.From<QuizQuestion>().Delete(q);
                }
            }

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
                Explanation = quiz.Explanation
            };

            await _client.From<QuizQuestion>().Insert(quizQuestion);
            _logger.LogWarning("✅ Новый вопрос создан с ID: {QuestionId}", quizQuestion.Id);

            var check = await _client
                .From<QuizQuestion>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Get();

            _logger.LogWarning("🔍 ПРОВЕРКА: в БД {Count} вопросов", check.Models?.Count ?? 0);
            _logger.LogWarning("========== КОНЕЦ СОХРАНЕНИЯ ТЕСТА ==========");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения теста");
            return false;
        }
    }

    public async Task<object> GetLessonCodeAsync(string teacherId, string courseId, string lessonId)
    {
        try
        {
            _logger.LogWarning("========== ЗАГРУЗКА КОДА ==========");
            _logger.LogWarning("LessonId: {LessonId}", lessonId);
            _logger.LogWarning("CourseId: {CourseId}", courseId);

            await _client.InitializeAsync();

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null)
            {
                _logger.LogWarning("❌ Урок не найден или не принадлежит курсу");
                return null;
            }

            var pythonLang = await GetPythonLanguageId();
            if (string.IsNullOrEmpty(pythonLang))
            {
                _logger.LogWarning("❌ Язык Python не найден");
                return null;
            }
            _logger.LogWarning("✅ Python language ID: {LanguageId}", pythonLang);

            var templateResponse = await _client
                .From<CodeTemplate>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Filter("language_id", Supabase.Postgrest.Constants.Operator.Equals, pythonLang)
                .Get();

            var template = templateResponse.Models?.FirstOrDefault();

            _logger.LogWarning("🔍 Поиск шаблона: LessonId={LessonId}, LanguageId={LangId}", lessonId, pythonLang);

            if (template != null)
            {
                _logger.LogWarning("✅ Найден шаблон с ID: {TemplateId}", template.Id);
                _logger.LogWarning("  TemplateCode: '{TemplateCode}'", template.TemplateCode);
                _logger.LogWarning("  StarterCode: '{StarterCode}'", template.StarterCode);
            }
            else
            {
                _logger.LogWarning("❌ Шаблон не найден");

                var allTemplates = await _client
                    .From<CodeTemplate>()
                    .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                    .Get();

                _logger.LogWarning("📊 Всего записей для урока: {Count}", allTemplates.Models?.Count ?? 0);
            }

            var testsResponse = await _client
                .From<Test>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Filter("language_id", Supabase.Postgrest.Constants.Operator.Equals, pythonLang)
                .Order("test_order", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var tests = new List<object>();
            if (testsResponse.Models != null && testsResponse.Models.Any())
            {
                _logger.LogWarning("✅ Найдено тестов: {Count}", testsResponse.Models.Count);
                foreach (var t in testsResponse.Models)
                {
                    tests.Add(new
                    {
                        Input = t.Input,
                        ExpectedOutput = t.ExpectedOutput,
                        IsHidden = t.IsHidden
                    });
                    _logger.LogWarning("  Тест: Input='{Input}', Output='{Output}', Hidden={Hidden}",
                        t.Input, t.ExpectedOutput, t.IsHidden);
                }
            }
            else
            {
                _logger.LogWarning("❌ Тесты не найдены");
            }

            var result = new
            {
                success = true,
                code = template != null ? new
                {
                    template.Id,
                    template.LessonId,
                    template.LanguageId,
                    template.TemplateCode,
                    template.StarterCode,
                    template.SolutionCode,
                    TestCases = tests
                } : null
            };

            _logger.LogWarning("📦 Возвращаем результат: {Result}", System.Text.Json.JsonSerializer.Serialize(result));
            _logger.LogWarning("==========================================");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения кода");
            return new { success = false, error = ex.Message };
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
            _logger.LogWarning("========== СОХРАНЕНИЕ КОДА ==========");
            _logger.LogWarning("LessonId: {LessonId}", lessonId);
            _logger.LogWarning("CourseId: {CourseId}", courseId);
            _logger.LogWarning("TeacherId: {TeacherId}", teacherId);

            _logger.LogWarning("TaskDescription length: {Length}", code.TaskDescription?.Length ?? 0);
            _logger.LogWarning("StarterCode length: {Length}", code.StarterCode?.Length ?? 0);
            _logger.LogWarning("SolutionCode length: {Length}", code.SolutionCode?.Length ?? 0);
            _logger.LogWarning("TestCases count: {Count}", code.TestCases?.Count ?? 0);

            if (code.TestCases != null && code.TestCases.Any())
            {
                for (int i = 0; i < code.TestCases.Count; i++)
                {
                    var test = code.TestCases[i];
                    _logger.LogWarning("  Тест {0}: Input='{1}', Output='{2}', Hidden={3}",
                        i + 1, test.Input ?? "(пусто)", test.ExpectedOutput ?? "(пусто)", test.IsHidden);
                }
            }

            await _client.InitializeAsync();

            var course = await VerifyCourseOwnership(teacherId, courseId);
            if (course == null)
            {
                _logger.LogWarning("❌ Курс не найден или не принадлежит преподавателю");
                return false;
            }
            _logger.LogWarning("✅ Курс проверен");

            var lesson = await VerifyLessonBelongsToCourse(lessonId, courseId);
            if (lesson == null)
            {
                _logger.LogWarning("❌ Урок не найден или не принадлежит курсу");
                return false;
            }
            _logger.LogWarning("✅ Урок проверен");

            var pythonLang = await GetPythonLanguageId();
            if (string.IsNullOrEmpty(pythonLang))
            {
                _logger.LogError("❌ Язык Python не найден в БД");
                return false;
            }
            _logger.LogWarning("✅ Python language ID: {LanguageId}", pythonLang);

            var existingTemplates = await _client
                .From<CodeTemplate>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Filter("language_id", Supabase.Postgrest.Constants.Operator.Equals, pythonLang)
                .Get();

            var template = existingTemplates.Models?.FirstOrDefault();

            if (template == null)
            {
                _logger.LogWarning("🆕 Создаем новый шаблон кода");
                template = new CodeTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    LessonId = lessonId,
                    LanguageId = pythonLang,
                    TemplateCode = code.TaskDescription,
                    StarterCode = code.StarterCode,
                    SolutionCode = code.SolutionCode,
                };
                await _client.From<CodeTemplate>().Insert(template);
                _logger.LogWarning("✅ Шаблон создан, ID: {TemplateId}", template.Id);
            }
            else
            {
                _logger.LogWarning("🔄 Обновляем существующий шаблон (ID: {TemplateId})", template.Id);
                template.TemplateCode = code.TaskDescription;
                template.StarterCode = code.StarterCode;
                template.SolutionCode = code.SolutionCode;
                await _client.From<CodeTemplate>().Update(template);
                _logger.LogWarning("✅ Шаблон обновлен");
            }

            var existingTests = await _client
                .From<Test>()
                .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                .Filter("language_id", Supabase.Postgrest.Constants.Operator.Equals, pythonLang)
                .Get();

            if (existingTests.Models != null && existingTests.Models.Any())
            {
                _logger.LogWarning("🗑️ Удаляем {Count} старых тестов", existingTests.Models.Count);
                foreach (var test in existingTests.Models)
                {
                    await _client.From<Test>().Delete(test);
                }
            }

            if (code.TestCases != null && code.TestCases.Any())
            {
                _logger.LogWarning("📝 СОХРАНЯЕМ {Count} ТЕСТОВ", code.TestCases.Count);

                int order = 1;
                foreach (var testCase in code.TestCases)
                {
                    _logger.LogWarning("  Тест {0}: Input='{1}', Output='{2}'",
                        order, testCase.Input, testCase.ExpectedOutput);

                    var test = new Test
                    {
                        Id = Guid.NewGuid().ToString(),
                        LessonId = lessonId,
                        LanguageId = pythonLang,
                        Input = testCase.Input ?? "",
                        ExpectedOutput = testCase.ExpectedOutput,
                        TestOrder = order++,
                        IsHidden = testCase.IsHidden,
                        TimeoutMs = 5000,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _client.From<Test>().Insert(test);
                    _logger.LogWarning("    ✅ Тест сохранен с ID: {TestId}", test.Id);
                }

                var checkTests = await _client
                    .From<Test>()
                    .Filter("lesson_id", Supabase.Postgrest.Constants.Operator.Equals, lessonId)
                    .Get();

                _logger.LogWarning("🔍 ПРОВЕРКА: в БД {Count} тестов", checkTests.Models?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning("⚠️ Нет тестов для сохранения");
            }

            _logger.LogWarning("✅ Кодовое задание для урока {LessonId} успешно сохранено", lessonId);
            _logger.LogWarning("==========================================");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения кодового задания");
            _logger.LogError("==========================================");
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
            {
                _logger.LogWarning("❌ Курс {CourseId} не найден или не принадлежит преподавателю {TeacherId}", courseId, teacherId);
                return null;
            }

            var modulesResponse = await _client
                .From<Module>()
                .Where(m => m.CourseId == courseId)
                .Order(m => m.ModuleOrder, Ordering.Ascending)
                .Get();

            var modules = modulesResponse.Models?.ToList() ?? new List<Module>();

            _logger.LogInformation("📦 Найдено модулей: {Count}", modules.Count);

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

            foreach (var module in modules)
            {
                var lessonsResponse = await _client
                    .From<Lesson>()
                    .Where(l => l.ModuleId == module.Id)
                    .Order(l => l.LessonOrder, Ordering.Ascending)
                    .Get();

                var lessons = lessonsResponse.Models?.ToList() ?? new List<Lesson>();

                _logger.LogInformation("  📚 Модуль {ModuleTitle} содержит {Count} уроков", module.Title, lessons.Count);

                var moduleData = new
                {
                    module.Id,
                    module.Title,
                    module.ModuleOrder,
                    lessons = new List<object>()
                };

                foreach (var lesson in lessons)
                {
                    moduleData.lessons.Add(new
                    {
                        lesson.Id,
                        lesson.Title,
                        lesson.LessonOrder,
                        hasTheory = true,
                        hasQuiz = lesson.HasQuiz, 
                        hasCode = lesson.HasCode  
                    });

                    _logger.LogInformation("    Урок {LessonTitle}: Quiz={HasQuiz}, Code={HasCode}",
                        lesson.Title, lesson.HasQuiz, lesson.HasCode);
                }

                result.modules.Add(moduleData);
            }

            _logger.LogInformation("✅ Структура курса {CourseId} загружена", courseId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка получения структуры курса {CourseId}", courseId);
            return null;
        }
    }

    private async Task<Course?> VerifyCourseOwnership(string teacherId, string courseId)
    {
        var response = await _client
            .From<Course>()
            .Where(c => c.Id == courseId)
            .Get();

        var course = response.Models?.FirstOrDefault();

        if (course == null)
        {
            _logger.LogWarning("❌ Курс {CourseId} не найден", courseId);
            return null;
        }

        if (course.CreatedBy != teacherId)
        {
            _logger.LogWarning("❌ Курс {CourseId} не принадлежит преподавателю {TeacherId}", courseId, teacherId);
            return null;
        }

        return course;
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
        var response = await _client
            .From<ProgrammingLanguage>()
            .Filter("name", Supabase.Postgrest.Constants.Operator.Equals, "python")
            .Get();

        return response.Models?.FirstOrDefault()?.Id ?? "";
    }
}