using TerraCH.Backuper;
using System.CommandLine;
using static TerraCH.Backuper.CommonValues;

Console.CancelKeyPress += OnConsoleCancelKeyPress;

RootCommand rootCommand = new("Terra Communication Hub Backuper");
Command saveSinglePostCommand = new("single-post", "备份单个帖子/动态/文章");
Command savePostsCommand = new("posts", "备份帖子（包含动态与文章）");
Command saveSingleAuthorCommand = new("single-author", "备份单个用户页面。");
Command saveAuthorsCommand = new("authors", "备份用户页面。");
Command saveAuthorCardsCommand = new("author-cards", "备份用户卡片。");
Command saveSingleAuthorCardCommand = new("single-author-card", "备份单个用户卡片。");
Command saveStaticContentCommand = new("static-file", "备份静态文件。");

Option<string?> cookieOption = new("--cookie", "配置请求时所使用的 Cookie。");
Option<int?> maxIdOption = new("--max-id", "指示在到达哪个 ID 后就停止备份。");
Option<int> targetIdOption = new("--target-id", "指示备份哪个 ID 所代表的内容。")
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

Option<string> targetPathOption = new("--target-path", "存有 HTML 文件的文件夹。")
{
    IsRequired = true,
};
targetPathOption.AddValidator(result =>
{
    string? path = result.Tokens[0].Value;

    if (Directory.Exists(path) != true)
    {
        result.ErrorMessage = "目标文件夹不存在。";
    }
});

savePostsCommand.AddOption(maxIdOption);
savePostsCommand.AddOption(configPathOption);
saveAuthorsCommand.AddOption(maxIdOption);
saveAuthorsCommand.AddOption(configPathOption);
saveAuthorCardsCommand.AddOption(maxIdOption);
saveAuthorCardsCommand.AddOption(configPathOption);

saveSinglePostCommand.AddOption(targetIdOption);
saveSingleAuthorCommand.AddOption(targetIdOption);
saveSingleAuthorCardCommand.AddOption(targetIdOption);

savePostsCommand.AddOption(pathOption);
saveSinglePostCommand.AddOption(pathOption);
saveAuthorsCommand.AddOption(pathOption);
saveSingleAuthorCommand.AddOption(pathOption);
saveAuthorCardsCommand.AddOption(pathOption);
saveSingleAuthorCardCommand.AddOption(pathOption);
saveStaticContentCommand.AddOption(pathOption);
savePostsCommand.AddOption(cookieOption);
saveSinglePostCommand.AddOption(cookieOption);
saveAuthorsCommand.AddOption(cookieOption);
saveSingleAuthorCommand.AddOption(cookieOption);

saveStaticContentCommand.AddOption(targetPathOption);

rootCommand.AddCommand(savePostsCommand);
rootCommand.AddCommand(saveAuthorsCommand);
rootCommand.AddCommand(saveSinglePostCommand);
rootCommand.AddCommand(saveSingleAuthorCommand);
rootCommand.AddCommand(saveAuthorCardsCommand);
rootCommand.AddCommand(saveSingleAuthorCardCommand);
rootCommand.AddCommand(saveStaticContentCommand);

savePostsCommand.SetHandler(async (postPath, maxPost, configPath, cookie) =>
{
    Config config = new(configPath);
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine("当前进度");
    Console.WriteLine($"帖子: {config.PostPosition}");
    Console.WriteLine("==========");
    if (maxPost.HasValue)
    {
        await PostSaver.SavePosts(config, postPath, maxPost.Value, cookie);
    }
    else
    {
        await PostSaver.SavePosts(config, postPath, cookie: cookie);
    }
}, pathOption, maxIdOption, configPathOption, cookieOption);

saveSinglePostCommand.SetHandler(async (savePath, targetPost, cookie) =>
{
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine($"目标帖子: {targetPost}");
    await PostSaver.SaveSinglePost(targetPost, savePath, cookie);
}, pathOption, targetIdOption, cookieOption);

saveAuthorsCommand.SetHandler((authorsPath, configPath, maxId, cookie) =>
{
    Config config = new(configPath);
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine("当前进度");
    Console.WriteLine($"用户: {config.AuthorPosition}");
    Console.WriteLine("==========");

    if (maxId.HasValue)
    {
        AuthorSaver.SaveAuthors(config, authorsPath, maxId.Value, cookie);
    }
    else
    {
        AuthorSaver.SaveAuthors(config, authorsPath, cookie: cookie);
    }
}, pathOption, configPathOption, maxIdOption, cookieOption);

saveSingleAuthorCommand.SetHandler(async (authorsPath, targetId, cookie) =>
{
    Console.WriteLine("备份操作已开始......");
    Console.WriteLine($"目标用户: {targetId}");
    await AuthorSaver.SaveSingleAuthor(targetId, authorsPath, cookie);
}, pathOption, targetIdOption, cookieOption);

saveAuthorCardsCommand.SetHandler(async (authorsPath, configPath, maxId) =>
{
    Config config = new(configPath);
    Console.WriteLine("保存卡片操作已开始......");
    Console.WriteLine("当前进度");
    Console.WriteLine($"用户: {config.AuthorCardsPosition}");
    Console.WriteLine("==========");

    if (maxId.HasValue)
    {
        await AuthorSaver.SaveAuthorCards(config, authorsPath, maxId.Value);
    }
    else
    {
        await AuthorSaver.SaveAuthorCards(config, authorsPath);
    }
}, pathOption, configPathOption, maxIdOption);

saveSingleAuthorCardCommand.SetHandler(async (authorsPath, targetId) =>
{
    Console.WriteLine("保存卡片操作已开始......");
    Console.WriteLine($"目标用户: {targetId}");
    await AuthorSaver.SaveSingleAuthorCard(targetId, authorsPath);
}, pathOption, targetIdOption);

saveStaticContentCommand.SetHandler(async (targetPath, savePath) =>
{
    Console.WriteLine("保存静态文件操作已开始......");
    await StaticFileSaver.SaveFiles(targetPath, savePath);
}, targetPathOption, pathOption);

await rootCommand.InvokeAsync(args);

Console.WriteLine("再见。");

static void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("请等待当前操作完成。");
    CancelToken.Cancel();
    e.Cancel = true;
}