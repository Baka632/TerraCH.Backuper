namespace TerraCH.Backuper;

public static class CommonValues
{
    public const string TerraCHCommentUrl = "https://terrach.net/wp-content/themes/LightSNS/module/more/comment.php";
    public const string TerraCHPageBase = "https://terrach.net/";
    public const string TerraCHAuthorBase = "https://terrach.net/author/";

    public static readonly Uri TerraCHBase = new("https://terrach.net/");
    public static readonly HttpClient RequestClient = new();
    public static readonly CancellationTokenSource CancelToken = new();
}
