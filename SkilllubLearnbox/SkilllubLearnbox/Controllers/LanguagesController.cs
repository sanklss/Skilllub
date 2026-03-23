using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkilllubLearnbox.Models;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/languages")]
[Authorize]
public class LanguagesController : ControllerBase
{
    private readonly ILogger<LanguagesController> _logger;
    private readonly Supabase.Client _client;

    public LanguagesController(ILogger<LanguagesController> logger, Supabase.Client client)
    {
        _logger = logger;
        _client = client;
    }

    [HttpGet]
    public async Task<IActionResult> GetLanguages()
    {
        try
        {
            await _client.InitializeAsync();

            var response = await _client
                .From<ProgrammingLanguage>()
                .Where(l => l.Enabled == true)
                .Order(l => l.Name, Ordering.Ascending)
                .Get();

            var languages = new List<object>();

            if (response.Models != null)
            {
                foreach (var lang in response.Models)
                {
                    languages.Add(new
                    {
                        id = lang.Id,
                        name = lang.Name,
                        fileExtension = lang.FileExtension,
                        monacoLanguageId = lang.MonacoLanguageId
                    });
                }
            }

            return Ok(new
            {
                success = true,
                languages = languages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения языков программирования");
            return StatusCode(500, new { success = false, error = "Ошибка сервера" });
        }
    }
}