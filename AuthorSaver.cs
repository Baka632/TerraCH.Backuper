using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static TerraCH.Backuper.CommonValues;

namespace TerraCH.Backuper;

internal static class AuthorSaver
{
    private static readonly HtmlParser parser = new();

    public static void SaveAuthors(Config config, string authorPath, int maxAuthor = 21500, string? cookie = null)
    {
        int loopCount = 0;
        bool shouldBreak = false;

        while (config.AuthorPosition < maxAuthor)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            int position = config.AuthorPosition;
            List<Task> tasks = new(10);
            for (int i = 0; i < 10; i++)
            {
                int taskPosition = position + i;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await SaveSingleAuthor(taskPosition, authorPath, cookie);
                        loopCount = 0;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        Console.Beep();
                        Console.WriteLine($"\t下载页面 {taskPosition} 失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                        loopCount++;

                        if (loopCount > 5)
                        {
                            Console.WriteLine($"\t下载页面 {taskPosition} 失败。");
                            shouldBreak = true;
                        }
                    }
                }));
            }

            Task.WaitAll([.. tasks]);
            if (shouldBreak)
            {
                break;
            }
            config.AuthorPosition += 10;
        }
    }

    public static async Task SaveSingleAuthor(int authorId, string authorPath, string? cookie = null)
    {
        string targetUrl = Path.Combine(TerraCHAuthorBase, authorId.ToString());
        HttpRequestMessage requestMessage = new(HttpMethod.Get, targetUrl);
        if (string.IsNullOrWhiteSpace(cookie) != true)
        {
            requestMessage.Headers.Add("Cookie", cookie);
        }
        using HttpResponseMessage message = await RequestClient.SendAsync(requestMessage);

        string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHAuthorBaseUri.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                authorId.ToString();

        if (message.IsSuccessStatusCode)
        {
            string html = await message.Content.ReadAsStringAsync();

            string authorDir = Path.Combine(authorPath, folderName);
            if (Directory.Exists(authorDir) != true)
            {
                Directory.CreateDirectory(authorDir);
            }

            string targetPath = Path.Combine(authorDir, "main.html");
            File.WriteAllText(targetPath, html);
            Console.WriteLine($"已保存 {authorId} 的用户页 | 状态码：{message.StatusCode}");

            using IHtmlDocument document = parser.ParseDocument(html);

            if (document.GetElementsByClassName("jinsom-more-posts").Length != 0)
            {
                // 有下一页动态
                await SaveDynamic(authorId, authorDir, cookie);
            }

            await SaveFollowing(authorId, authorDir, cookie);
            await SaveForward(authorId, authorDir, cookie);

            bool isLikesPrivate = !document.All.Any(element =>
            {
                if (element is IHtmlListItemElement li && li.HasAttribute("type"))
                {
                    string type = li.GetAttribute("type")!;
                    if (type.Equals("like", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });

            await SaveLikes(authorId, authorDir, isLikesPrivate, cookie);
        }
        else if (message.StatusCode == HttpStatusCode.NotFound)
        {
            string postDir = Path.Combine(authorPath, $"[404]{folderName}");
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            Console.WriteLine($"用户 {authorId} 未找到：{targetUrl} | 状态码：{message.StatusCode}");
        }
        else
        {
            Console.WriteLine($"HTTP 错误：{message.StatusCode}");
        }
    }

    public static async Task SaveAuthorCards(Config config, string authorPath, int maxAuthor = 21500)
    {
        int loopCount = 0;

        while (config.AuthorCardsPosition < maxAuthor)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SaveSingleAuthorCard(config.AuthorCardsPosition, authorPath);
                config.AuthorCardsPosition++;
                loopCount = 0;
            }
           catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.Beep();
                Console.WriteLine($"\t下载此用户的卡片失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount > 5)
                {
                    Console.WriteLine("\t下载此用户的卡片失败。");
                    break;
                }
            }
        }
    }

    public static async Task SaveSingleAuthorCard(int authorId, string authorCardPath)
    {
        MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");
        StringContent postContent = new($"author_id={authorId}&info_card=1");
        postContent.Headers.ContentType = postMimeType;

        HttpResponseMessage message = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/module/stencil/info-card.php", postContent);

        if (message.IsSuccessStatusCode)
        {
            string targetPath = Path.Combine(authorCardPath, $"{authorId}.html");
            string content = await message.Content.ReadAsStringAsync();
            File.WriteAllText(targetPath, content);
            Console.WriteLine($"已保存用户卡片：{targetPath}");
        }
        else
        {
            Console.Beep();
            Console.WriteLine($"未能保存用户 {authorId} 的卡片。");
        }
    }

    private static async Task SaveDynamic(int authorId, string authorDir, string? cookie)
    {
        string contentFolder = Path.Combine(authorDir, "dynamics");
        int page = 2;
        int loopCount = 0;

        while (true)
        {
            MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

            try
            {
                StringContent postContent = new($"type=all&page={page}&load_type=more&index=0&author_id={authorId}");
                postContent.Headers.ContentType = postMimeType;
                if (string.IsNullOrWhiteSpace(cookie) != true)
                {
                    postContent.Headers.Add("Cookie", cookie);
                }

                HttpResponseMessage result = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/module/data/post.php", postContent);
                string content = await result.Content.ReadAsStringAsync();

                if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
                {
                    if (Directory.Exists(contentFolder) != true)
                    {
                        Directory.CreateDirectory(contentFolder);
                    }

                    string filePath = Path.Combine(contentFolder, $"{page - 1}.html");
                    File.WriteAllText(filePath, content);
                    Console.WriteLine($"\t已下载用户 {authorId} 的动态（第 {page} 页）");
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"\t下载用户 {authorId} 动态失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载用户 {authorId} 动态失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }

    private static async Task SaveFollowing(int authorId, string authorDir, string? cookie)
    {
        string contentFolder = Path.Combine(authorDir, "follow");
        MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");
        StringContent mainPostContent = new($"author_id={authorId}");
        mainPostContent.Headers.ContentType = postMimeType;
        if (string.IsNullOrWhiteSpace(cookie) != true)
        {
            mainPostContent.Headers.Add("Cookie", cookie);
        }

        HttpResponseMessage mainContentMessage = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/module/stencil/member-follow.php", mainPostContent);

        if (mainContentMessage.IsSuccessStatusCode)
        {
            if (Directory.Exists(contentFolder) != true)
            {
                Directory.CreateDirectory(contentFolder);
            }

            string mainContentPath = Path.Combine(contentFolder, "main.html");
            string mainContent = await mainContentMessage.Content.ReadAsStringAsync();
            File.WriteAllText(mainContentPath, mainContent);
            Console.WriteLine($"\t已保存用户 {authorId} 的主关注页");

            string followingFolder = Path.Combine(contentFolder, "following");
            {
                int followingPage = 1;
                int followingLoopCount = 0;
                while (true)
                {
                    StringContent followingContent = new($"page={followingPage}&user_id={authorId}&type=following&number=30");
                    followingContent.Headers.ContentType = postMimeType;
                    if (string.IsNullOrWhiteSpace(cookie) != true)
                    {
                        followingContent.Headers.Add("Cookie", cookie);
                    }

                    try
                    {
                        HttpResponseMessage message = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/mobile/module/user/follower.php", followingContent);

                        if (message.IsSuccessStatusCode)
                        {
                            string json = await message.Content.ReadAsStringAsync();

                            using JsonDocument document = JsonDocument.Parse(json);
                            int code = document.RootElement.GetProperty("code"u8).GetInt32();

                            if (code == 0)
                            {
                                break;
                            }

                            if (Directory.Exists(followingFolder) != true)
                            {
                                Directory.CreateDirectory(followingFolder);
                            }

                            string filePath = Path.Combine(followingFolder, $"{followingPage}.json");
                            File.WriteAllText(filePath, json);
                            Console.WriteLine($"\t\t已下载用户 {authorId} 的用户关注列表（第 {followingPage} 页）");
                            followingPage++;
                            followingLoopCount = 0;
                        }
                        else
                        {
                            Console.WriteLine($"\t\t下载用户 {authorId} 的关注项失败，HTTP 错误：{message.StatusCode}");
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        Console.WriteLine($"\t\t下载用户 {authorId} 的关注项失败，正在重试.....（{followingLoopCount + 1} of 5）\n\t\t异常：{ex.Message}");
                        followingLoopCount++;

                        if (followingLoopCount >= 5)
                        {
                            Console.Beep();
                            Console.WriteLine($"\t\t下载用户 {authorId} 的关注项失败，正在跳转到下一个项目.....\n\t\t异常：{ex.Message}");
                            break;
                        }
                    }
                }
            }

            string fansFolder = Path.Combine(contentFolder, "fans");
            {
                int fansPage = 1;
                int fansLoopCount = 0;

                while (true)
                {
                    StringContent fansContent = new($"page={fansPage}&user_id={authorId}&type=follower&number=30");
                    fansContent.Headers.ContentType = postMimeType;
                    if (string.IsNullOrWhiteSpace(cookie) != true)
                    {
                        fansContent.Headers.Add("Cookie", cookie);
                    }

                    try
                    {
                        HttpResponseMessage message = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/mobile/module/user/follower.php", fansContent);

                        if (message.IsSuccessStatusCode)
                        {
                            string json = await message.Content.ReadAsStringAsync();

                            using JsonDocument document = JsonDocument.Parse(json);
                            int code = document.RootElement.GetProperty("code"u8).GetInt32();

                            if (code == 0)
                            {
                                break;
                            }

                            if (Directory.Exists(fansFolder) != true)
                            {
                                Directory.CreateDirectory(fansFolder);
                            }

                            string filePath = Path.Combine(fansFolder, $"{fansPage}.json");
                            File.WriteAllText(filePath, json);
                            Console.WriteLine($"\t\t已下载用户 {authorId} 的粉丝列表（第 {fansPage} 页）");
                            fansPage++;
                            fansLoopCount = 0;
                        }
                        else
                        {
                            Console.WriteLine($"\t\t下载用户 {authorId} 的粉丝项失败，HTTP 错误：{message.StatusCode}");
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        Console.WriteLine($"\t\t下载用户 {authorId} 的粉丝项失败，正在重试.....（{fansLoopCount + 1} of 5）\n\t\t异常：{ex.Message}");
                        fansLoopCount++;

                        if (fansLoopCount >= 5)
                        {
                            Console.Beep();
                            Console.WriteLine($"\t\t下载用户 {authorId} 的粉丝项失败，正在跳转到下一个项目.....\n\t\t异常：{ex.Message}");
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"\t下载用户 {authorId} 的关注项失败 | 状态码：{mainContentMessage.StatusCode}");
        }
    }

    private static async Task SaveForward(int authorId, string authorDir, string? cookie)
    {
        string contentFolder = Path.Combine(authorDir, "forwards");

        int page = 1;
        int loopCount = 0;

        while (true)
        {
            MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

            try
            {
                StringContent postContent = new($"type=reprint&page={page}&load_type={(page == 1 ? "ajax" : "more")}&index=2&author_id={authorId}");
                postContent.Headers.ContentType = postMimeType;
                if (string.IsNullOrWhiteSpace(cookie) != true)
                {
                    postContent.Headers.Add("Cookie", cookie);
                }

                HttpResponseMessage result = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/module/data/post.php", postContent);
                string content = await result.Content.ReadAsStringAsync();

                if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
                {
                    if (Directory.Exists(contentFolder) != true)
                    {
                        Directory.CreateDirectory(contentFolder);
                    }

                    using IHtmlDocument doc = parser.ParseDocument(content);

                    if (doc.GetElementsByClassName("jinsom-empty-page").Length != 0)
                    {
                        // 空的
                        string filePath = Path.Combine(contentFolder, "empty.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载用户 {authorId} 的转发项（内容为空）");
                        break;
                    }
                    else
                    {
                        string filePath = Path.Combine(contentFolder, $"{page}.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载用户 {authorId} 的转发项（第 {page} 页）");
                    }
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"\t下载用户 {authorId} 的转发项失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载用户 {authorId} 的转发项失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }

    private static async Task SaveLikes(int authorId, string authorDir, bool isPrivate, string? cookie)
    {
        string contentFolder = isPrivate
            ? Path.Combine(authorDir, "[private]likes")
            : Path.Combine(authorDir, "likes");

        int page = 1;
        int loopCount = 0;

        while (true)
        {
            MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

            try
            {
                StringContent postContent = new($"type=like&page={page}&load_type={(page == 1 ? "ajax" : "more")}&index=3&author_id={authorId}");
                postContent.Headers.ContentType = postMimeType;
                if (string.IsNullOrWhiteSpace(cookie) != true)
                {
                    postContent.Headers.Add("Cookie", cookie);
                }

                HttpResponseMessage result = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/module/data/post.php", postContent);
                string content = await result.Content.ReadAsStringAsync();

                if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
                {
                    if (Directory.Exists(contentFolder) != true)
                    {
                        Directory.CreateDirectory(contentFolder);
                    }

                    using IHtmlDocument doc = parser.ParseDocument(content);

                    if (doc.GetElementsByClassName("jinsom-empty-page").Length != 0)
                    {
                        // 空的
                        string filePath = Path.Combine(contentFolder, "empty.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载用户 {authorId} 的喜欢项（内容为空）");
                        break;
                    }
                    else
                    {
                        string filePath = Path.Combine(contentFolder, $"{page}.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载用户 {authorId} 的喜欢项（第 {page} 页）");
                    }
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
           catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"\t下载用户 {authorId} 的喜欢项失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载用户 {authorId} 的喜欢项失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }
}
