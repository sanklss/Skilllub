using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;

namespace SkilllubLearnbox.Controllers;

[ApiController]
[Route("api/session-events")]
public class SessionEventsController : ControllerBase
{
    private static readonly Dictionary<string, List<HttpResponse>> _userConnections = new();
    private static readonly object _lockObject = new object();
    private readonly ILogger<SessionEventsController> _logger;

    public SessionEventsController(ILogger<SessionEventsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task GetSessionEvents([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");
        await Response.Body.FlushAsync();

        lock (_lockObject)
        {
            if (!_userConnections.ContainsKey(userId))
            {
                _userConnections[userId] = new List<HttpResponse>();
            }
            _userConnections[userId].Add(Response);
        }

        _logger.LogInformation("SSE connection established for user {UserId}", userId);

        try
        {
            await WriteSseEvent(Response, "connected", new { message = "Connected to session events" });

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(30000);
                await WriteSseEvent(Response, "ping", new { timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSE connection closed for user {UserId}", userId);
        }
        finally
        {
            lock (_lockObject)
            {
                if (_userConnections.ContainsKey(userId))
                {
                    _userConnections[userId].Remove(Response);
                    if (_userConnections[userId].Count == 0)
                    {
                        _userConnections.Remove(userId);
                    }
                }
            }
        }
    }

    public static async Task NotifyUserSessionRevoked(string userId)
    {
        List<HttpResponse> connections;
        lock (_lockObject)
        {
            if (!_userConnections.ContainsKey(userId))
                return;

            connections = new List<HttpResponse>(_userConnections[userId]);
        }

        var notification = new
        {
            revoked = true,
            timestamp = DateTime.UtcNow,
            message = "Ваша сессия была завершена администратором"
        };

        foreach (var response in connections)
        {
            try
            {
                await WriteSseEvent(response, "session_revoked", notification);
            }
            catch
            {

            }
        }
    }

    private static async Task WriteSseEvent(HttpResponse response, string eventType, object data)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var eventData = $"event: {eventType}\ndata: {json}\n\n";

            var bytes = Encoding.UTF8.GetBytes(eventData);
            await response.Body.WriteAsync(bytes);
            await response.Body.FlushAsync();
        }
        catch
        {

        }
    }
}