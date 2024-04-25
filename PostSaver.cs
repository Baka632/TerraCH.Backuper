using System.Net;
using System.Net.Http.Headers;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static TerraCH.Backuper.CommonValues;

namespace TerraCH.Backuper;

internal static class PostSaver
{
    private static readonly HtmlParser parser = new();

    public static async Task SavePosts(Config config, string postPath, int maxPost = 29725)
    {
        int loopCount = 0;

        while (config.PostPosition < maxPost)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SaveSinglePost(config.PostPosition, postPath);
                config.PostPosition++;
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

    public static async Task SaveSinglePost(int postId, string postPath)
    {
        int page = 1;
        string targetUrl = Path.Combine(TerraCHPageBase, $"{postId}.html");

        using HttpResponseMessage message = await RequestClient.GetAsync(targetUrl);

        if (message.IsSuccessStatusCode)
        {
            string html = await message.Content.ReadAsStringAsync();

            string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHBaseUri.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                postId.ToString();

            string postDir = Path.Combine(postPath, folderName);
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            string targetPath = Path.Combine(postDir, $"{page}.html");
            File.WriteAllText(targetPath, html);
            Console.WriteLine($"已保存：{targetPath} | 状态码：{message.StatusCode}");

            using IHtmlDocument document = parser.ParseDocument(html);

            bool isBeeped = false;
            if (document.GetElementsByClassName("follow-see").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine("\t此页面存在需关注才能查看的内容。");
            }

            if (document.GetElementsByClassName("comment-see").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine("\t此页面存在需回复才能查看的内容。");
            }

            if (document.GetElementsByClassName("jinsom-tips").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine($"\t此页面存在登陆/购买才能查看的内容。");
            }

            if (document.GetElementsByClassName("jinsom-posts-list").Length != 0)
            {
                // 动态/文章
                if (document.GetElementsByClassName(" jinsom-post-comment-more").Length != 0)
                {
                    // 有下一页
                    await SaveDynamicComment(postId, postDir);
                }
                // 没下一页直接走
            }
            else
            {
                // 帖子
                if (document.GetElementsByClassName("jinsom-bbs-comment-list-page").Length != 0)
                {
                    // 有下一页
                    IElement bbs_header = document.GetElementsByClassName("jinsom-bbs-single-header").First();
                    string? bbs_id = bbs_header.GetAttribute("data");

                    await SavePostComment(postId, bbs_id, postDir);
                }
                // 同样，没下一页直接走
            }
        }
        else if (message.StatusCode == HttpStatusCode.NotFound)
        {
            string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHBaseUri.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                postId.ToString();

            string postDir = Path.Combine(postPath, $"[404]{folderName}");
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            Console.WriteLine($"此页未找到：{targetUrl} | 状态码：{message.StatusCode}");
        }
        else
        {
            Console.WriteLine($"HTTP 错误：{message.StatusCode}");
        }
    }

    private static async Task SavePostComment(int postId, string? bbs_id, string postDir)
    {
        MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

        int commentPage = 2;
        int commentLoopCount = 0;
        string content = string.Empty;
        do
        {
            try
            {
                StringContent postContent = new($"page={commentPage}&post_id={postId}&number=10&bbs_id={bbs_id}");
                postContent.Headers.ContentType = postMimeType;

                HttpResponseMessage result = await RequestClient.PostAsync(TerraCHPostCommentUrl, postContent);
                content = await result.Content.ReadAsStringAsync();
                SaveCommentCore(content, commentPage, postDir);
                commentPage++;
                commentLoopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                if (HandleCommentSaveProcessError(ref commentLoopCount, ex))
                {
                    break;
                }
            }
        } while (content != "0" && string.IsNullOrWhiteSpace(content) != true);
    }
    
    private static async Task SaveDynamicComment(int postId, string postDir)
    {
        MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

        int commentPage = 2;
        int commentLoopCount = 0;
        string content = string.Empty;
        do
        {
            try
            {
                StringContent postContent = new($"post_id={postId}&page={commentPage}");
                postContent.Headers.ContentType = postMimeType;

                HttpResponseMessage result = await RequestClient.PostAsync(TerraCHDynamicCommentUrl, postContent);
                content = await result.Content.ReadAsStringAsync();
                SaveCommentCore(content, commentPage, postDir);
                commentPage++;
                commentLoopCount = 0;
            }
            catch (HttpRequestException ex)
            {
                if (HandleCommentSaveProcessError(ref commentLoopCount, ex))
                {
                    break;
                }
            }
        } while (content != "0" && string.IsNullOrWhiteSpace(content) != true);
    }

    private static void SaveCommentCore(string content, int commentPage, string postDir)
    {
        bool isBeeped = false;

        if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
        {
            using IHtmlDocument commentDoc = parser.ParseDocument(content);
            if (commentDoc.GetElementsByClassName("follow-see").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine($"\t\t第 {commentPage} 页存在需关注才能查看的内容。");
            }

            if (commentDoc.GetElementsByClassName("comment-see").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine($"\t\t第 {commentPage} 页存在需回复才能查看的内容。");
            }

            if (commentDoc.GetElementsByClassName("jinsom-tips").Length != 0)
            {
                if (!isBeeped)
                {
                    Console.Beep();
                    isBeeped = true;
                }

                Console.WriteLine($"\t\t第 {commentPage} 页存在登陆/购买才能查看的内容。");
            }

            string commentTargetPath = Path.Combine(postDir, $"{commentPage}.html");
            File.WriteAllText(commentTargetPath, content);
            Console.WriteLine($"\t已下载此页的评论（第 {commentPage} 页）");
        }
    }

    private static bool HandleCommentSaveProcessError(ref int commentLoopCount, HttpRequestException ex)
    {
        Console.WriteLine($"\t下载此页评论失败，正在重试.....（{commentLoopCount + 1} of 5）\n\t异常：{ex.Message}");
        commentLoopCount++;

        if (commentLoopCount >= 5)
        {
            Console.Beep();
            Console.WriteLine($"\t下载此页评论失败，正在跳转到下一个帖子.....\n\t异常：{ex.Message}");
            return true;
        }

        return false;
    }
}
