using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static TerraCH.Backuper.CommonValues;

namespace TerraCH.Backuper;

public static class StaticFileDownloader
{
    private static readonly HtmlParser Parser = new();
    private static readonly string[] OutReferenceAttributes = ["href", "src", "link"];

    public static async Task DownloadFileFromHtml(string html, string outputFolderPath)
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
            string? href = item.GetAttribute("href");
            string? src = item.GetAttribute("src");
            string? link = item.GetAttribute("link");
            Uri uri;

            if (href != null)
            {
                uri = new(href);
            }
            else if (src != null)
            {
                uri = new(src);
            }
            else if (link != null)
            {
                uri = new(link);
            }
            else
            {
                throw new InvalidOperationException("序列包含链接为空的元素");
            }

            string hostFolder = uri.IsAbsoluteUri ? uri.Host : "terrach.net";
            string targetDir = Path.Join(outputFolderPath, hostFolder, string.Join(string.Empty, uri.Segments[..^1]));

            if (Directory.Exists(targetDir) != true)
            {
                Directory.CreateDirectory(targetDir);
            }

            try
            {
                using HttpResponseMessage result = await RequestClient.GetAsync(targetDir);

                if (result.IsSuccessStatusCode)
                {
                    byte[] content = await result.Content.ReadAsByteArrayAsync();

                    string filePath = Path.Join(targetDir, uri.Segments[^1]);
                    File.WriteAllBytes(filePath, content);
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }
        }
    }
}
