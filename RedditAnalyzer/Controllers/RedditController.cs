using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using RedditAnalyzer.Models;
using RedditAnalyzer.Services;

namespace RedditAnalyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RedditController : ControllerBase
{
    private readonly IRedditService _redditService;
    private readonly ILogger<RedditController> _logger;

    public RedditController(IRedditService redditService, ILogger<RedditController> logger)
    {
        _redditService = redditService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze posts from given subreddits filtered by keywords.
    /// Returns JSON response.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null) return BadRequest(new { error = validationError });

        _logger.LogInformation("POST /api/reddit/analyze — {Count} subreddits, limit={Limit}",
            request.Items.Count, request.Limit);

        var result = await _redditService.AnalyzeAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Analyze posts and return result as a downloadable JSON file.
    /// </summary>
    [HttpPost("analyze/download")]
    public async Task<IActionResult> AnalyzeDownload([FromBody] AnalyzeRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null) return BadRequest(new { error = validationError });

        _logger.LogInformation("POST /api/reddit/analyze/download — {Count} subreddits, limit={Limit}",
            request.Items.Count, request.Limit);

        var result = await _redditService.AnalyzeAsync(request);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileName = $"reddit_analysis_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

        return File(bytes, "application/json", fileName);
    }

    private static string? ValidateRequest(AnalyzeRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return "At least one subreddit item is required.";
        if (request.Limit <= 0 || request.Limit > 100)
            return "Limit must be between 1 and 100.";
        if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.Subreddit)))
            return "Subreddit name cannot be empty.";
        return null;
    }
}
