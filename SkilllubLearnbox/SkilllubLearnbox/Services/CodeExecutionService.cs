using System.Text;
using System.Text.Json;
using SkilllubLearnbox.DTOs;
using SkilllubLearnbox.Models;
using Supabase;
using Supabase.Postgrest;

namespace SkilllubLearnbox.Services;

public class CodeExecutionService
{
    private readonly ILogger<CodeExecutionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Supabase.Client _supabaseClient;
    private readonly ProgressService _progressService;
    private readonly string _compilerUrl;
    private readonly Supabase.Client _client; 


    public CodeExecutionService(
        ILogger<CodeExecutionService> logger,
        IHttpClientFactory httpClientFactory,
        Supabase.Client supabaseClient,
        ProgressService progressService,
        IConfiguration configuration, Supabase.Client client)
    {
        _logger = logger;
        _client = client;
        _httpClient = httpClientFactory.CreateClient();
        _supabaseClient = supabaseClient;
        _progressService = progressService;
        _compilerUrl = configuration["CompilerService:Url"] ?? "http://localhost:8000";

    }
    

    public async Task<CodeExecutionResultDto> ExecuteCodeAsync(CodeExecuteDto dto)
    {
        try
        {
            _logger.LogInformation("Executing code for lesson {LessonId}", dto.LessonId);

            var request = new
            {
                code = dto.Code,
                language = dto.Language,
                stdin = dto.Stdin ?? "",
                time_limit = dto.TimeLimit ?? 5,
                memory_limit_mb = 256
            };

            var response = await _httpClient.PostAsJsonAsync($"{_compilerUrl}/execute", request);

            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("\n========== ОТВЕТ ОТ КОМПИЛЯТОРА (RAW) ==========");
            Console.WriteLine(responseBody);
            Console.WriteLine("================================================\n");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Compiler error: {Error}", errorContent);
                return new CodeExecutionResultDto
                {
                    Success = false,
                    Error = "Ошибка выполнения кода"
                };
            }

            var result = JsonSerializer.Deserialize<CodeExecutionResultDto>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            await SaveSubmissionAsync(dto, result);

            return result ?? new CodeExecutionResultDto { Success = false, Error = "Пустой ответ от компилятора" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing code");
            return new CodeExecutionResultDto
            {
                Success = false,
                Error = "Ошибка сервера компиляции"
            };
        }
    }

    public async Task<CodeExecutionResultDto> RunCodeTestsAsync(string lessonId, string code, string language, string userId)
    {
        try
        {
            _logger.LogInformation("Running tests for lesson {LessonId}, user {UserId}", lessonId, userId);

            var tests = await GetTestsForLessonAsync(lessonId, language);

            if (tests.Count == 0)
            {
                _logger.LogWarning("⚠️ No tests found for lesson {LessonId}", lessonId);
                return new CodeExecutionResultDto
                {
                    Success = true,
                    Output = "Для этого урока нет автоматических тестов",
                    PassedTests = 0,
                    TotalTests = 0,
                    Score = 0
                };
            }
            var request = new
            {
                code = code,
                language = language,
                stdin = "",
                timeout = 5
            };

            var response = await _httpClient.PostAsJsonAsync($"{_compilerUrl}/execute", request);
            var result = await response.Content.ReadFromJsonAsync<CodeExecutionResultDto>();

            if (result != null)
            {
                result.TotalTests = tests.Count;

                var testResults = new List<TestResultDto>();
                int passedCount = 0;

                foreach (var test in tests)
                {
                    bool passed = false;

                    if (string.IsNullOrEmpty(test.Input))
                    {
                        passed = (result.Output ?? "").TrimEnd() == test.ExpectedOutput.TrimEnd();
                    }
                    else
                    {
                        passed = false;
                    }

                    testResults.Add(new TestResultDto
                    {
                        TestId = testResults.Count,
                        Passed = passed,
                        Input = test.Input,
                        ExpectedOutput = test.ExpectedOutput,
                        ActualOutput = result.Output ?? "",
                        ExecutionTimeMs = result.ExecutionTimeMs,
                        IsHidden = test.IsHidden,
                        Weight = test.Weight
                    });

                    if (passed) passedCount++;
                }

                result.TestResults = testResults;
                result.PassedTests = passedCount;
                result.Score = passedCount * 100 / tests.Count;

                if (passedCount == tests.Count)
                {
                    result.Output = $"Все тесты пройдены! ({passedCount}/{tests.Count})";
                }
                else
                {
                    result.Output = $"Пройдено {passedCount} из {tests.Count} тестов";
                }

                var submission = await SaveSubmissionWithTestsAsync(
                    userId, lessonId, language, code, result, tests);

                if (result.PassedTests == result.TotalTests && result.TotalTests > 0)
                {
                    _logger.LogInformation("All tests passed for lesson {LessonId}, marking practice as completed", lessonId);
                    await _progressService.MarkPracticeAsCompletedAsync(userId, lessonId, result.Score ?? 100);
                }
            }

            return result ?? new CodeExecutionResultDto { Success = false, Error = "Пустой ответ от компилятора" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error running tests for lesson {LessonId}", lessonId);
            return new CodeExecutionResultDto
            {
                Success = false,
                Error = "Ошибка при запуске тестов"
            };
        }
    }

    private async Task<List<TestDto>> GetTestsForLessonAsync(string lessonId, string languageName)
    {
        try
        {
            await _client.InitializeAsync();

            Console.WriteLine($"🔍 Поиск тестов для урока {lessonId}");

            var allLanguages = await _client
                .From<ProgrammingLanguage>()
                .Get();

            var language = allLanguages.Models?
                .FirstOrDefault(l => l.Name.ToLower() == languageName.ToLower());

            if (language == null)
            {
                Console.WriteLine("❌ Язык не найден");
                return new List<TestDto>();
            }

            Console.WriteLine($"✅ Язык найден: {language.Name} (ID: {language.Id})");

            var allTests = await _client
                .From<Test>()
                .Get();

            var tests = allTests.Models?
                .Where(t => t.LessonId == lessonId && t.LanguageId == language.Id)
                .ToList() ?? new List<Test>();

            Console.WriteLine($"📊 Найдено тестов: {tests.Count}");

            if (tests.Count > 0)
            {
                Console.WriteLine($"✅ Первый тест: ожидается '{tests[0].ExpectedOutput}'");
            }

            return tests.Select(t => new TestDto
            {
                Id = t.Id,
                Input = t.Input ?? "",
                ExpectedOutput = t.ExpectedOutput,
                IsHidden = t.IsHidden,
                TimeoutMs = t.TimeoutMs,
                Weight = t.Weight
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return new List<TestDto>();
        }
    }

    private int CalculateScore(List<TestResultDto>? testResults, List<TestDto> tests)
    {
        if (testResults == null || testResults.Count == 0 || tests.Count == 0)
            return 0;

        var totalWeight = tests.Sum(t => t.Weight);
        if (totalWeight == 0) totalWeight = tests.Count;

        var earnedWeight = 0;
        foreach (var testResult in testResults)
        {
            var test = tests.ElementAtOrDefault(testResult.TestId);
            if (test != null && testResult.Passed)
            {
                earnedWeight += test.Weight > 0 ? test.Weight : 1;
            }
        }

        return (int)Math.Round((double)earnedWeight / totalWeight * 100);
    }

    private async Task SaveSubmissionAsync(CodeExecuteDto dto, CodeExecutionResultDto? result)
    {
        try
        {
            await _supabaseClient.InitializeAsync();

            var submission = new Submission
            {
                Id = Guid.NewGuid().ToString(),
                UserId = dto.UserId,
                LessonId = dto.LessonId,
                LanguageId = dto.LanguageId,
                Code = dto.Code,
                Status = result?.Success == true ? "success" : "failed",
                Output = result?.Output,
                ExecutionTimeMs = (int?)result?.ExecutionTimeMs,
                CreatedAt = DateTime.UtcNow
            };

            await _supabaseClient.From<Submission>().Insert(submission);
            _logger.LogInformation("Submission saved for user {UserId}, lesson {LessonId}",
                dto.UserId, dto.LessonId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving submission");
        }
    }

    private async Task<Submission> SaveSubmissionWithTestsAsync(
    string userId,
    string lessonId,
    string language,
    string code,
    CodeExecutionResultDto result,
    List<TestDto> tests)
    {
        try
        {
            await _supabaseClient.InitializeAsync();

            var allLanguages = await _supabaseClient
                .From<ProgrammingLanguage>()
                .Get();

            var languageObj = allLanguages.Models?
                .FirstOrDefault(l => l.Name.ToLower() == language.ToLower());

            var languageId = languageObj?.Id ?? "11111111-1111-1111-1111-111111111111";

            var submission = new Submission
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                LessonId = lessonId,
                LanguageId = languageId,
                Code = code,
                Status = result.Success ? "success" : "failed",
                Output = result.Output,
                ExecutionTimeMs = (int?)result.ExecutionTimeMs,
                MemoryKb = (int?)result.MemoryKb,
                TestsPassed = result.PassedTests,
                TestsTotal = result.TotalTests,
                Score = result.Score,
                CreatedAt = DateTime.UtcNow
            };

            await _supabaseClient.From<Submission>().Insert(submission);

            if (result.TestResults != null)
            {
                foreach (var testResult in result.TestResults)
                {
                    var test = tests.ElementAtOrDefault(testResult.TestId);
                    if (test != null)
                    {
                        var submissionTest = new SubmissionTest
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubmissionId = submission.Id,
                            TestId = test.Id,
                            Passed = testResult.Passed,
                            ActualOutput = testResult.ActualOutput,
                            ExpectedOutput = test.ExpectedOutput,
                            ExecutionTimeMs = (int?)testResult.ExecutionTimeMs,
                            ErrorMessage = testResult.ErrorMessage,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _supabaseClient.From<SubmissionTest>().Insert(submissionTest);
                    }
                }
            }

            _logger.LogInformation("Submission with tests saved, id: {SubmissionId}", submission.Id);
            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving submission with tests");
            throw;
        }
    }

    public async Task<List<SubmissionDto>> GetUserSubmissionsAsync(string userId, string lessonId)
    {
        try
        {
            await _supabaseClient.InitializeAsync();

            var allSubmissions = await _supabaseClient
                .From<Submission>()
                .Order(s => s.CreatedAt, Constants.Ordering.Descending)
                .Get();

            var submissions = allSubmissions.Models?
                .Where(s => s.UserId == userId && s.LessonId == lessonId)
                .Take(10)
                .ToList() ?? new List<Submission>();

            var allLanguages = await _supabaseClient
                .From<ProgrammingLanguage>()
                .Get();

            var languageMap = allLanguages.Models?
                .ToDictionary(l => l.Id, l => l.Name) ?? new Dictionary<string, string>();

            return submissions.Select(s => new SubmissionDto
            {
                Id = s.Id,
                LessonId = s.LessonId,
                Language = languageMap.GetValueOrDefault(s.LanguageId, "unknown"),
                Status = s.Status,
                Score = s.Score,
                TestsPassed = s.TestsPassed,
                TestsTotal = s.TestsTotal,
                ExecutionTimeMs = s.ExecutionTimeMs,
                CreatedAt = s.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting submissions");
            return new List<SubmissionDto>();
        }
    }
}