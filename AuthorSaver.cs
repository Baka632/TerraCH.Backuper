using System.Net;
using System.Net.Http.Headers;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static TerraCH.Backuper.CommonValues;

namespace TerraCH.Backuper;

internal static class AuthorSaver
{
    private static readonly HtmlParser parser = new();

    public static async Task SaveAuthors(Config config, string authorPath, int maxAuthor = 21500)
    {
        int loopCount = 0;

        while (config.AuthorPosition < maxAuthor)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SaveSingleAuthor(config.AuthorPosition, authorPath);
                config.AuthorPosition++;
                loopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                Console.Beep();
                Console.WriteLine($"\t下载此页面失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount > 5)
                {
                    Console.WriteLine("\t下载此页面失败。");
                    break;
                }
            }
        }
    }

    public static async Task SaveSingleAuthor(int authorId, string authorPath)
    {
        string targetUrl = Path.Combine(TerraCHAuthorBase, authorId.ToString());
        using HttpResponseMessage message = await RequestClient.GetAsync(targetUrl);

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
            Console.WriteLine($"已保存：{targetPath} | 状态码：{message.StatusCode}");

            using IHtmlDocument document = parser.ParseDocument(html);

            if (document.GetElementsByClassName("jinsom-more-posts").Length != 0)
            {
                // 有下一页动态
                await SaveDynamic(authorId, authorDir);
            }

            await SaveFollowing(authorId, authorDir);
            await SaveForward(authorId, authorDir);

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

            await SaveLikes(authorId, authorDir, isLikesPrivate);
        }
        else if (message.StatusCode == HttpStatusCode.NotFound)
        {
            string postDir = Path.Combine(authorPath, $"[404]{folderName}");
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            Console.WriteLine($"此用户未找到：{targetUrl} | 状态码：{message.StatusCode}");
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
            catch (HttpRequestException ex)
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

    private static async Task SaveDynamic(int authorId, string authorDir)
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
                    Console.WriteLine($"\t已下载此用户的动态（第 {page} 页）");
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"\t下载此用户动态失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载此用户动态失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }

    private static async Task SaveFollowing(int authorId, string authorDir)
    {
        string contentFolder = Path.Combine(authorDir, "following");
        MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");
        StringContent mainPostContent = new($"author_id={authorId}");
        mainPostContent.Headers.ContentType = postMimeType;

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
            Console.WriteLine($"\t已保存：{mainContentPath} | 状态码：{mainContentMessage.StatusCode}");

            string followingFolder = Path.Combine(contentFolder, "following");
            string fansFolder = Path.Combine(contentFolder, "fans");

            {
                int followingPage = 1;
                int followingLoopCount = 0;
                while (true)
                {
                    StringContent followingContent = new($"page={followingPage}&user_id={authorId}&type=following&number=30");
                    followingContent.Headers.ContentType = postMimeType;

                    try
                    {
                        HttpResponseMessage message = await RequestClient.PostAsync("https://terrach.net/wp-content/themes/LightSNS/mobile/module/user/follower.php", mainPostContent);

                        if (message.IsSuccessStatusCode)
                        {

                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"\t\t下载此用户关注项失败，正在重试.....（{followingLoopCount + 1} of 5）\n\t\t异常：{ex.Message}");
                        followingLoopCount++;

                        if (followingLoopCount >= 5)
                        {
                            Console.Beep();
                            Console.WriteLine($"\t\t下载此用户关注项失败，正在跳转到下一个项目.....\n\t\t异常：{ex.Message}");
                            break;
                        }
                    }
                }
            }

            //TODO:
        }
        else
        {
            Console.WriteLine($"\t下载此用户关注项失败 | 状态码：{mainContentMessage.StatusCode}");
        }
    }

    private static async Task SaveForward(int authorId, string authorDir)
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
                        Console.WriteLine($"\t已下载此用户的转发项（内容为空）");
                        break;
                    }
                    else
                    {
                        string filePath = Path.Combine(contentFolder, $"{page}.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载此用户的转发项（第 {page} 页）");
                    }
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"\t下载此用户转发项失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载此用户转发项失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }

    private static async Task SaveLikes(int authorId, string authorDir, bool isPrivate)
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
                        Console.WriteLine($"\t已下载此用户的喜欢项（内容为空）");
                        break;
                    }
                    else
                    {
                        string filePath = Path.Combine(contentFolder, $"{page}.html");
                        File.WriteAllText(filePath, content);
                        Console.WriteLine($"\t已下载此用户的喜欢项（第 {page} 页）");
                    }
                }
                else
                {
                    break;
                }

                page++;
                loopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"\t下载此用户喜欢项失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
                loopCount++;

                if (loopCount >= 5)
                {
                    Console.Beep();
                    Console.WriteLine($"\t下载此用户喜欢项失败，正在跳转到下一个项目.....\n\t异常：{ex.Message}");
                    break;
                }
            }
        }
    }
}
