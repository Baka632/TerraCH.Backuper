using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static TerraCH.Backuper.CommonValues;

namespace TerraCH.Backuper;

public static partial class StaticFileSaver
{
    private static readonly HtmlParser Parser = new();
    private static readonly string[] OutReferenceAttributes = ["href", "src", "link"];
    private static readonly Uri WaterAreaCategoryUri = new("https://terrach.net/category/水区");

    public static async Task SaveFiles(string targetFolderPath, string saveFolderPath)
    {
        try
        {
            DirectoryInfo directory = new(targetFolderPath);

            foreach (FileInfo file in directory.EnumerateFiles("*.html"))
            {
                if (CancelToken.IsCancellationRequested)
                {
                    break;
                }

                Console.WriteLine($"正在处理文件：{file.FullName}");

                string html = File.ReadAllText(file.FullName);
                await SaveSingleFileFromHtml(html, saveFolderPath);
            }

            foreach (DirectoryInfo dir in directory.EnumerateDirectories())
            {
                if (CancelToken.IsCancellationRequested)
                {
                    break;
                }

                await SaveFiles(dir.FullName, saveFolderPath);
            }
        }
        catch (Exception)
        {
            Console.WriteLine("操作终止。");
        }
    }

    public static async Task SaveSingleFileFromHtml(string html, string outputFolderPath)
    {
        using IHtmlDocument document = Parser.ParseDocument(html);

        IEnumerable<IElement> elements = document.All
            .Where(element =>
            {
                foreach (string attr in OutReferenceAttributes)
                {
                    if (element.HasAttribute(attr) && element.TagName != "IFRAME")
                    {
                        return true;
                    }
                }

                return false;
            });

        bool shouldDelay = false;

        foreach (IElement item in elements)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            string? href = item.GetAttribute("href");
            string? src = item.GetAttribute("src");
            string? link = item.GetAttribute("link");
            Uri? uri;

            if (href != null)
            {
                if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out uri) != true)
                {
                    Console.WriteLine($"\t链接格式错误：{uri}");
                    continue;
                }
            }
            else if (src != null)
            {
                if (Uri.TryCreate(src, UriKind.RelativeOrAbsolute, out uri) != true)
                {
                    Console.WriteLine($"\t链接格式错误：{uri}");
                    continue;
                }
            }
            else if (link != null)
            {
                if (Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out uri) != true)
                {
                    Console.WriteLine($"\t链接格式错误：{uri}");
                    continue;
                }
            }
            else
            {
                throw new InvalidOperationException("序列包含链接为空的元素");
            }

            if (uri.IsAbsoluteUri != true)
            {
                uri = new Uri(TerraCHBaseUri, uri.ToString());
            }

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                continue;
            }

            if (uri.Host != "terrach.net" && uri.Host != "ark-dev-1256540909.file.myqcloud.com")
            {
                continue;
            }

            //bool shouldSkip = false;
            //foreach (string exclude in ExcludeHosts)
            //{
            //    if (uri.Host.Equals(exclude, StringComparison.OrdinalIgnoreCase))
            //    {
            //        shouldSkip = true;
            //        break;
            //    }
            //}

            //if (shouldSkip)
            //{
            //    continue;
            //}

            if (uri.Host == "terrach.net" && uri.Segments.Length > 1 && uri.Segments[1].EndsWith(".html"))
            {
                continue;
            }

            Regex matchAuthor = GetMatchAuthorPageRegex();
            string uriString = uri.ToString();

            if (matchAuthor.IsMatch(uriString)
                || uriString == "https://terrach.net/contents.css"
                || uriString == "https://terrach.net/tableselection.css"
                || uriString.Contains("泰拉通讯枢纽_files"))
            {
                continue;
            }

            bool isDir = false;
            string uriPart;
            if (uri.Segments[^1].EndsWith('/'))
            {
                uriPart = string.Join(string.Empty, uri.Segments);
                isDir = true;
            }
            else
            {
                uriPart = string.Join(string.Empty, uri.Segments[..^1]);
            }

            string targetDir = WebUtility.UrlDecode(Path.Join(outputFolderPath, uri.Host, uriPart));
            string filePath;
            if (isDir)
            {
                filePath = Path.Join(targetDir, "index");
            }
            else
            {
                filePath = Path.Join(targetDir, WebUtility.UrlDecode(uri.Segments[^1]));
                if (Path.GetFileName(filePath) == string.Empty)
                {
                    filePath = Path.Join(filePath, "index.html");
                }
                else if (Path.GetExtension(filePath) == string.Empty)
                {
                    filePath += ".html";
                }
            }

            if (File.Exists(filePath) && File.ReadAllBytes(filePath).Length > 0)
            {
                continue;
            }

            try
            {
                HttpRequestMessage requestMessage;

                if (uri.Host == "ark-dev-1256540909.file.myqcloud.com")
                {
                    if (shouldDelay)
                    {
                        await Task.Delay(1500);
                        shouldDelay = false;
                    }
                    else
                    {
                        shouldDelay = true;
                    }

                    uri = new Uri(TerraCHCdnBaseUri, uri.ToString()[TerraCHImageUriBase.Length..]);
                    requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

                    requestMessage.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0");
                    requestMessage.Headers.Referrer = TerraCHBaseUri;
                    requestMessage.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                    shouldDelay = true;
                }
                else
                {
                    requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                }

                using HttpResponseMessage result = await RequestClient.SendAsync(requestMessage);

                if (result.IsSuccessStatusCode)
                {
                    if (Directory.Exists(targetDir) != true)
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    byte[] content = await result.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(filePath, content);
                    Console.WriteLine($"\t已保存：{filePath}");
                    Console.WriteLine($"\t\t目标来自：{uri}");
                }
                else if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"\t未找到目标：{uri}");
                }
                else
                {
                    Console.WriteLine($"\t访问 {uri} 时出现 HTTP 错误：{result.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\t未能下载 {uri}，因为出现异常：{ex.Message}");
                continue;
            }
        }
    }

    [GeneratedRegex(@"https://terrach\.net/author/(.*)")]
    private static partial Regex GetMatchAuthorPageRegex();
}
