using System.Net;
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
                    if (element.HasAttribute(attr))
                    {
                        return true;
                    }
                }

                return false;
            });

        foreach (IElement item in elements)
        {
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            string? href = item.GetAttribute("href");
            string? src = item.GetAttribute("src");
            string? link = item.GetAttribute("link");
            Uri uri;

            if (href != null)
            {
                uri = new(href, UriKind.RelativeOrAbsolute);
            }
            else if (src != null)
            {
                uri = new(src, UriKind.RelativeOrAbsolute);
            }
            else if (link != null)
            {
                uri = new(link, UriKind.RelativeOrAbsolute);
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

            if (matchAuthor.IsMatch(uriString) || uriString == "https://terrach.net/contents.css" || uriString == "https://terrach.net/tableselection.css")
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

            string targetDir = Path.Join(outputFolderPath, uri.Host, uriPart);
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
                    filePath = Path.Join(filePath, "index");
                }
            }

            if (File.Exists(filePath))
            {
                continue;
            }

            try
            {
                using HttpResponseMessage result = await RequestClient.GetAsync(uri);

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
