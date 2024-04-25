using TerraCH.Backuper;
using System.CommandLine;
using static TerraCH.Backuper.CommonValues;

Console.CancelKeyPress += OnConsoleCancelKeyPress;

RootCommand rootCommand = new("Terra Communication Hub Backuper");
Command saveSinglePostCommand = new("single-post", "备份单个帖子/动态/文章");
Command savePostsCommand = new("posts", "备份帖子（包含动态与文章）");
Command saveAuthorsCommand = new("authors", "备份用户页面。");

Option<int?> maxPostOption = new("--max-post", "指示在到达哪个帖子后就停止备份。");
Option<int> targetPostOption = new("--target-post", "指示备份哪个帖子。")
{
    IsRequired = true,
};

Option<string> configPathOption = new("--config-path", "配置文件文件夹。")
{
    IsRequired = true,
};
configPathOption.AddValidator(result =>
{
    string? path = result.Tokens[0].Value;

    if (Directory.Exists(path) != true)
    {
        result.ErrorMessage = "目标文件夹不存在。";
    }
});

Option<string> pathOption = new("--path", "保存目标使用的文件夹的路径。")
{
    IsRequired = true,
};
pathOption.AddValidator(result =>
{
    string? path = result.Tokens[0].Value;

    if (Directory.Exists(path) != true)
    {
        result.ErrorMessage = "目标文件夹不存在。";
    }
});

savePostsCommand.AddOption(maxPostOption);
savePostsCommand.AddOption(configPathOption);

saveSinglePostCommand.AddOption(targetPostOption);

saveAuthorsCommand.AddOption(configPathOption);

savePostsCommand.AddOption(pathOption);
saveSinglePostCommand.AddOption(pathOption);
saveAuthorsCommand.AddOption(pathOption);

rootCommand.AddCommand(savePostsCommand);
rootCommand.AddCommand(saveAuthorsCommand);
rootCommand.AddCommand(saveSinglePostCommand);

savePostsCommand.SetHandler(async (postPath, maxPost, configPath) =>
{
    Config config = new(configPath);
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine("当前进度");
    Console.WriteLine($"帖子: {config.PostPosition}");
    Console.WriteLine("==========");
    if (maxPost.HasValue)
    {
        await PostSaver.StartBackup(config, postPath, maxPost.Value);
    }
    else
    {
        await PostSaver.StartBackup(config, postPath);
    }
}, pathOption, maxPostOption, configPathOption);

saveSinglePostCommand.SetHandler(async (savePath, targetPost) =>
{
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine($"目标帖子: {targetPost}");
    await PostSaver.BackupTarget(targetPost, savePath);
}, pathOption, targetPostOption);

saveAuthorsCommand.SetHandler((authorsPath, configPath) =>
{
    Config config = new(configPath);
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine("当前进度");
    Console.WriteLine($"用户: {config.AuthorPosition}");
    Console.WriteLine("==========");
    //TODO:
}, pathOption, configPathOption);

await rootCommand.InvokeAsync(args);

Console.WriteLine("再见。");

static void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("请等待当前操作完成。");
    CancelToken.Cancel();
    e.Cancel = true;
}