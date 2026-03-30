namespace RedditAnalyzer.Models;

public class AnalyzeRequest
{
    public List<SubredditQuery> Items { get; set; } = new();
    public int Limit { get; set; } = 25;
}

public class SubredditQuery
{
    public string Subreddit { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
}

public class PostResult
{
    public string Title { get; set; } = string.Empty;
    public bool HasImage { get; set; }
}

// Internal Reddit JSON models
public class RedditResponse
{
    public RedditData? Data { get; set; }
}

public class RedditData
{
    public List<RedditPostWrapper>? Children { get; set; }
}

public class RedditPostWrapper
{
    public RedditPost? Data { get; set; }
}

public class RedditPost
{
    public string Title { get; set; } = string.Empty;
    public string? Selftext { get; set; }
    public string? Url { get; set; }
    public bool Is_Video { get; set; }
    public string? Post_Hint { get; set; }
    public string? Thumbnail { get; set; }
}
