using System.Net;
using TerraCH.Backuper;
using static TerraCH.Backuper.CommonValues;

Console.CancelKeyPress += Console_CancelKeyPress;

Console.WriteLine("备份操作已开始......");
Config config = new(@"E:\Backups[Local Computer]\TerraCH Backup\Config");
string postPath = @"E:\Backups[Local Computer]\TerraCH Backup\Posts";
string authorsPath = @"E:\Backups[Local Computer]\TerraCH Backup\Authors";

Console.WriteLine("当前进度");
Console.WriteLine($"Post: {config.PostPosition}");
Console.WriteLine($"Author: {config.AuthorPosition}");

int loopCount = 0;
while (config.PostPosition < 30000)
{
    if (CancelToken.IsCancellationRequested)
    {
        Console.WriteLine("再见。");
        break;
    }

    string targetUrl = Path.Combine(TerraCHPageBase, $"{config.PostPosition}.html");

    try
    {
        int page = 1;
        using HttpResponseMessage message = await RequestClient.GetAsync(targetUrl);

        if (message.IsSuccessStatusCode || message.StatusCode == HttpStatusCode.NotFound)
        {
            using Stream stream = await message.Content.ReadAsStreamAsync();
            stream.Seek(0, SeekOrigin.Begin);

            string folderName = message.RequestMessage?.RequestUri is not null ?
                TerraCHBase.MakeRelativeUri(message.RequestMessage.RequestUri).ToString() :
                config.PostPosition.ToString();

            string postDir = Path.Combine(postPath, folderName);
            if (Directory.Exists(postDir) != true)
            {
                Directory.CreateDirectory(postDir);
            }

            string targetPath = Path.Combine(postDir, $"{page}.html");
            using FileStream targetStream = File.Create(targetPath);
            await stream.CopyToAsync(targetStream);

            config.PostPosition++;

            Console.WriteLine($"已保存：{targetPath} | 状态码：{message.StatusCode}");
            Thread.Sleep(5000);
        }
        else
        {
            Console.WriteLine($"HTTP 错误：{message.StatusCode}");
        }
    }
    catch (Exception)
    {
        loopCount++;

        if (loopCount > 5)
        {
            config.PostPosition++;
            loopCount = 0;
        }
    }
}

static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("请等待当前操作完成。");
    CancelToken.Cancel();
    e.Cancel = true;
}