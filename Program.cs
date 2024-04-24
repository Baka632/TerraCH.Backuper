using System.Net;
using System.Net.Http.Headers;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using TerraCH.Backuper;
using static TerraCH.Backuper.CommonValues;

Console.CancelKeyPress += OnConsoleCancelKeyPress;

Console.WriteLine("备份操作已开始......");
Config config = new(@"E:\Backups[Local Computer]\TerraCH Backup\Config");
string postPath = @"E:\Backups[Local Computer]\TerraCH Backup\Posts";
string authorsPath = @"E:\Backups[Local Computer]\TerraCH Backup\Authors";

Console.WriteLine("当前进度");
Console.WriteLine($"Post: {config.PostPosition}");
Console.WriteLine($"Author: {config.AuthorPosition}");
Console.WriteLine("==========");

int loopCount = 0;
HtmlParser parser = new();
while (config.PostPosition < 29800)
{
    if (CancelToken.IsCancellationRequested)
    {
        Console.WriteLine("再见。");
        break;
    }

    string targetUrl = Path.Combine(TerraCHPageBase, $"{config.PostPosition}.html");
    MediaTypeHeaderValue postMimeType = new("application/x-www-form-urlencoded", "UTF-8");

    try
    {
        int page = 1;
        using HttpResponseMessage message = await RequestClient.GetAsync(targetUrl);

        if (message.IsSuccessStatusCode)
        {
            string html = await message.Content.ReadAsStringAsync();

            string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHBase.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                config.PostPosition.ToString();

            string postDir = Path.Combine(postPath, folderName);
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            string targetPath = Path.Combine(postDir, $"{page}.html");
            File.WriteAllText(targetPath, html);
            Console.WriteLine($"已保存：{targetPath} | 状态码：{message.StatusCode}");

            using IHtmlDocument document = parser.ParseDocument(html);

            if (document.GetElementsByClassName("follow-see").Length != 0)
            {
                Console.Beep();
                Console.WriteLine("\t此页面存在需关注才能查看的内容。");
            }
            
            if (document.GetElementsByClassName("comment-see").Length != 0)
            {
                Console.Beep();
                Console.WriteLine("\t此页面存在需回复才能查看的内容。");
            }

            if (document.GetElementsByClassName("jinsom-tips").Length != 0)
            {
                Console.Beep();
                Console.WriteLine($"\t此页面存在登陆/购买才能查看的内容。");
            }

            if (document.GetElementsByClassName("jinsom-posts-list").Length != 0)
            {
                // 动态/文章

                if (document.GetElementsByClassName(" jinsom-post-comment-more").Length != 0)
                {
                    // 有下一页
                    int commentPage = 2;
                    int commentLoopCount = 0;

                    string content = string.Empty;
                    do
                    {
                        try
                        {
                            StringContent postContent = new($"post_id={config.PostPosition}&page={commentPage}");
                            postContent.Headers.ContentType = postMimeType;
                            HttpResponseMessage result = await RequestClient.PostAsync(TerraCHDynamicCommentUrl, postContent);
                            content = await result.Content.ReadAsStringAsync();
                            if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
                            {
                                using IHtmlDocument commentDoc = parser.ParseDocument(content);
                                if (commentDoc.GetElementsByClassName("follow-see").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t第 {commentPage} 页存在需关注才能查看的内容。");
                                }

                                if (commentDoc.GetElementsByClassName("comment-see").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t第 {commentPage} 页存在需回复才能查看的内容。");
                                }
                                
                                if (commentDoc.GetElementsByClassName("jinsom-tips").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t\t第 {commentPage} 页存在登陆/购买才能查看的内容。");
                                }

                                string commentTargetPath = Path.Combine(postDir, $"{commentPage}.html");
                                File.WriteAllText(commentTargetPath, content);
                                Console.WriteLine($"\t已下载此页的评论（第 {commentPage} 页）");
                                commentPage++;
                            }
                        }
                        catch (HttpRequestException)
                        {
                            Console.WriteLine($"\t下载此页评论失败，正在重试.....（{loopCount} of 5）");
                            commentLoopCount++;

                            if (commentLoopCount > 5)
                            {
                                Console.WriteLine($"\t下载此页评论失败，正在跳转到下一个帖子.....");
                                break;
                            }
                        }

                        //Thread.Sleep(WaitTimeMilliseconds);
                    } while (content != "0" && string.IsNullOrWhiteSpace(content) != true);
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
                    int commentPage = 2;
                    int commentLoopCount = 0;

                    string content = string.Empty;
                    do
                    {
                        try
                        {
                            StringContent postContent = new($"page={commentPage}&post_id={config.PostPosition}&number=10&bbs_id={bbs_id}");
                            postContent.Headers.ContentType = postMimeType;
                            HttpResponseMessage result = await RequestClient.PostAsync(TerraCHPostCommentUrl, postContent);
                            content = await result.Content.ReadAsStringAsync();
                            if (content != "0" && string.IsNullOrWhiteSpace(content) != true)
                            {
                                using IHtmlDocument commentDoc = parser.ParseDocument(content);
                                if (commentDoc.GetElementsByClassName("follow-see").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t\t第 {commentPage} 页存在需关注才能查看的内容。");
                                }

                                if (commentDoc.GetElementsByClassName("comment-see").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t\t第 {commentPage} 页存在需回复才能查看的内容。");
                                }

                                if (commentDoc.GetElementsByClassName("jinsom-tips").Length != 0)
                                {
                                    Console.Beep();
                                    Console.WriteLine($"\t\t第 {commentPage} 页存在登陆/购买才能查看的内容。");
                                }

                                string commentTargetPath = Path.Combine(postDir, $"{commentPage}.html");
                                File.WriteAllText(commentTargetPath, content);
                                Console.WriteLine($"\t已下载此页的评论（第 {commentPage} 页）");
                                commentPage++;
                            }
                        }
                        catch (HttpRequestException)
                        {
                            Console.WriteLine($"\t下载此页评论失败，正在重试.....（{loopCount} of 5）");
                            commentLoopCount++;

                            if (commentLoopCount > 5)
                            {
                                Console.WriteLine($"\t下载此页评论失败，正在跳转到下一个帖子.....");
                                break;
                            }
                        }

                        //Thread.Sleep(WaitTimeMilliseconds);
                    } while (content != "0" && string.IsNullOrWhiteSpace(content) != true);
                }
                // 同样，没下一页直接走
            }

            config.PostPosition++;
        }
        else if (message.StatusCode == HttpStatusCode.NotFound)
        {
            string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHBase.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                config.PostPosition.ToString();

            string postDir = Path.Combine(postPath, $"[404]{folderName}");
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            Console.WriteLine($"此页未找到：{targetUrl} | 状态码：{message.StatusCode}");
            config.PostPosition++;
        }
        else
        {
            Console.WriteLine($"HTTP 错误：{message.StatusCode}");
            config.PostPosition++;
        }
    }
    catch (HttpRequestException ex)
    {
        Console.Beep();
        Console.WriteLine($"\t下载此页面失败，正在重试.....（{loopCount + 1} of 5）\n\t异常：{ex.Message}");
        loopCount++;

        if (loopCount > 5)
        {
            Console.WriteLine("下载此页面失败。");
            break;
        }
    }

    //Thread.Sleep(WaitTimeMilliseconds);
}

static void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("请等待当前操作完成。");
    CancelToken.Cancel();
    e.Cancel = true;
}