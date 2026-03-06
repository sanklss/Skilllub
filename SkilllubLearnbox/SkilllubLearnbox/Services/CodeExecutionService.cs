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
            _logger.LogWarning("🔥 Sending to Docker - stdin: '{Stdin}'", dto.Stdin);
            _logger.LogWarning("🔥 stdin length: {Length}", dto.Stdin?.Length ?? 0);
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

    public async Task<CodeExecutionResultDto> RunCodeTestsAsync(string lessonId, string code, string language, string userId, string stdin = "")
    {
        try
        {
            _logger.LogInformation("Running tests for lesson {LessonId}, user {UserId}", lessonId, userId);

            var tests = await GetTestsForLessonAsync(lessonId, language);

            var passedTestIds = await GetPassedTestIdsAsync(userId, lessonId);

            var nextTest = tests.FirstOrDefault(t => !passedTestIds.Contains(t.Id));

            if (nextTest == null)
            {
                return new CodeExecutionResultDto
                {
                    Success = true,
                    Output = "Все тесты уже пройдены!",
                    PassedTests = tests.Count,
                    TotalTests = tests.Count,
                    Score = 100,
                    TestResults = tests.Select((t, i) => new TestResultDto
                    {
                        TestId = i,
                        Passed = true,
                        Input = t.Input,
                        ExpectedOutput = t.ExpectedOutput,
                        ActualOutput = "✅",
                        IsHidden = t.IsHidden
                    }).ToList()
                };
            }

            string processedStdin = stdin?.Replace("\\n", "\n") ?? "";

            var request = new
            {
                code = code,
                language = language,
                stdin = processedStdin,
                timeout = nextTest.TimeoutMs / 1000
            };

            var response = await _httpClient.PostAsJsonAsync($"{_compilerUrl}/execute", request);
            var result = await response.Content.ReadFromJsonAsync<CodeExecutionResultDto>();

            bool passed = false;
            if (result?.Success == true)
            {
                string normalizedExpected = nextTest.ExpectedOutput.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                string normalizedActual = (result.Output ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();

                if (lessonId == "10000001-0000-0000-0000-000000000021")
                {
                    passed = normalizedActual.Contains("Угадал") || normalizedActual.Contains("угадал");
                }
                else
                {
                    passed = normalizedActual == normalizedExpected;
                }
            }

            var testResults = new List<TestResultDto>();
            int currentTestIndex = tests.FindIndex(t => t.Id == nextTest.Id);

            for (int i = 0; i < tests.Count; i++)
            {
                var test = tests[i];
                bool isPassed = passedTestIds.Contains(test.Id) || (i == currentTestIndex && passed);

                testResults.Add(new TestResultDto
                {
                    TestId = i,
                    Passed = isPassed,
                    Input = test.Input,
                    ExpectedOutput = test.ExpectedOutput,
                    ActualOutput = isPassed ? "✅" : (i == currentTestIndex ? result?.Output ?? "" : "⏳"),
                    ExecutionTimeMs = i == currentTestIndex ? result?.ExecutionTimeMs ?? 0 : 0,
                    IsHidden = test.IsHidden,
                    Weight = test.Weight  
                });
            }

            int passedCount = testResults.Count(tr => tr.Passed);
            int totalTests = tests.Count;
            int score = totalTests > 0 ? (passedCount * 100 / totalTests) : 0;

            var finalResult = new CodeExecutionResultDto
            {
                Success = passed,
                TestResults = testResults,
                PassedTests = passedCount,
                TotalTests = totalTests,
                Score = score,
                Output = passed ?
                    $"✅ Тест {currentTestIndex + 1} пройден! Осталось {tests.Count - passedCount} тестов." :
                    $"❌ Тест {currentTestIndex + 1} не пройден. Попробуйте еще раз."
            };

            var submission = await SaveSubmissionWithTestsAsync(userId, lessonId, language, code, finalResult, tests);

            if (passedCount == totalTests)
            {
                _logger.LogInformation("All tests passed for lesson {LessonId}, marking code as completed", lessonId);
                await _progressService.MarkCodeAsCompletedAsync(userId, lessonId, score);
            }

            return finalResult;
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
    private async Task<HashSet<string>> GetPassedTestIdsAsync(string userId, string lessonId)
    {
        try
        {
            await _supabaseClient.InitializeAsync();

            var submissions = await _supabaseClient
                .From<Submission>()
                .Where(s => s.UserId == userId && s.LessonId == lessonId)
                .Select("id")
                .Get();

            var submissionIds = submissions.Models?.Select(s => s.Id).ToList() ?? new List<string>();

            if (!submissionIds.Any())
                return new HashSet<string>();

            var passedTests = new HashSet<string>();

            foreach (var submissionId in submissionIds)
            {
                var testResults = await _supabaseClient
                    .From<SubmissionTest>()
                    .Where(st => st.SubmissionId == submissionId && st.Passed == true)
                    .Select("test_id")
                    .Get();

                foreach (var test in testResults.Models ?? new List<SubmissionTest>())
                {
                    passedTests.Add(test.TestId);
                }
            }

            _logger.LogInformation("Found {Count} passed tests for user {UserId}", passedTests.Count, userId);
            return passedTests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passed test ids");
            return new HashSet<string>();
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