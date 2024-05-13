namespace TerraCH.Backuper;

public static class CommonValues
{
    /// <summary>
    /// 帖子评论
    /// </summary>
    public const string TerraCHPostCommentUrl = "https://terrach.net/wp-content/themes/LightSNS/module/more/comment.php";
    /// <summary>
    /// 文章/动态评论
    /// </summary>
    public const string TerraCHDynamicCommentUrl = "https://terrach.net/wp-content/themes/LightSNS/module/more/post-comment.php";
    public const string TerraCHPageBase = "https://terrach.net/";
    public const string TerraCHAuthorBase = "https://terrach.net/author/";
    public const string TerraCHImageUriBase = "https://ark-dev-1256540909.file.myqcloud.com/";
    public const int WaitTimeMilliseconds = 2000;

    public static readonly Uri TerraCHBaseUri = new("https://terrach.net/");
    public static readonly Uri TerraCHAuthorBaseUri = new("https://terrach.net/author/");
    public static readonly Uri TerraCHCdnBaseUri = new("https://cdn.terrach.net/");
    public static readonly HttpClient RequestClient = new();
    public static readonly CancellationTokenSource CancelToken = new();
    public static readonly Uri[] ExcludeLinks =
    [
        new Uri("https://terrach.net/category/水区"),
    ];
}
