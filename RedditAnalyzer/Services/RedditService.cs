using System.Text.Json;
using RedditAnalyzer.Models;
using Microsoft.Extensions.Logging;

namespace RedditAnalyzer.Services;

public interface IRedditService
{
    Task<Dictionary<string, List<PostResult>>> AnalyzeAsync(AnalyzeRequest request);
}

public class RedditService : IRedditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RedditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    public RedditService(HttpClient httpClient, ILogger<RedditService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, List<PostResult>>> AnalyzeAsync(AnalyzeRequest request)
    {
        _logger.LogInformation("Starting analysis for {Count} subreddit(s), limit={Limit}",
            request.Items.Count, request.Limit);

        // Run all subreddit fetches in parallel
        var tasks = request.Items.Select(item => FetchAndFilterAsync(item, request.Limit));
        var results = await Task.WhenAll(tasks);

        var response = new Dictionary<string, List<PostResult>>();
        foreach (var (key, posts) in results)
            response[key] = posts;

        _logger.LogInformation("Analysis complete. Total subreddits processed: {Count}", response.Count);
        return response;
    }

    private async Task<(string Key, List<PostResult> Posts)> FetchAndFilterAsync(
        SubredditQuery query, int limit)
    {
        var name = query.Subreddit.TrimStart('/').TrimStart('r', 'R').TrimStart('/');
        var key = $"/r/{name}";

        _logger.LogInformation("Fetching posts from {Subreddit} (limit={Limit})", key, limit);

        try
        {
            var url = $"https://www.reddit.com/r/{name}.json?limit={limit}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch {Subreddit}: HTTP {StatusCode}", key, response.StatusCode);
                return (key, new List<PostResult>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var redditResponse = JsonSerializer.Deserialize<RedditResponse>(json, JsonOptions);

            var posts = redditResponse?.Data?.Children?
                .Select(c => c.Data)
                .Where(p => p != null)
                .ToList() ?? new List<RedditPost?>();

            _logger.LogInformation("Fetched {Count} posts from {Subreddit}", posts.Count, key);

            var filtered = posts
                .Where(post => MatchesKeywords(post!, query.Keywords))
                .Select(post => new PostResult
                {
                    Title = post!.Title,
                    HasImage = DetectImage(post)
                })
                .ToList();

            _logger.LogInformation("Filtered to {Count} matching posts in {Subreddit}", filtered.Count, key);
            return (key, filtered);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching {Subreddit}", key);
            return (key, new List<PostResult>());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse response from {Subreddit}", key);
            return (key, new List<PostResult>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing {Subreddit}", key);
            return (key, new List<PostResult>());
        }
    }

    private static bool MatchesKeywords(RedditPost post, List<string> keywords)
    {
        if (keywords.Count == 0) return true;

        var title = post.Title ?? string.Empty;
        var body = post.Selftext ?? string.Empty;

        return keywords.Any(kw =>
            title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            body.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DetectImage(RedditPost post)
    {
        // post_hint == "image" is the most reliable signal
        if (post.Post_Hint == "image") return true;

        // Check if URL points to a known image extension
        if (!string.IsNullOrEmpty(post.Url))
        {
            try
            {
                var ext = Path.GetExtension(new Uri(post.Url).AbsolutePath);
                if (ImageExtensions.Contains(ext)) return true;
            }
            catch { /* ignore malformed URLs */ }
        }

        // Check thumbnail (reddit sets it to "self", "default", "nsfw" for non-images)
        if (!string.IsNullOrEmpty(post.Thumbnail) &&
            post.Thumbnail != "self" &&
            post.Thumbnail != "default" &&
            post.Thumbnail != "nsfw" &&
            post.Thumbnail != "spoiler" &&
            post.Thumbnail.StartsWith("http"))
            return true;

        return false;
    }
}
