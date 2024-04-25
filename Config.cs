namespace TerraCH.Backuper;

public class Config
{
    private readonly string postPath;
    private readonly string authorPath;
    private readonly string authorCardsPath;
    private int postPosition = 1;
    private int authorPosition = 1;
    private int authorCardsPosition = 1;

    public int PostPosition
    {
        get => postPosition;
        set
        {
            postPosition = value;
            File.WriteAllText(postPath, postPosition.ToString());
        }
    }

    public int AuthorPosition
    {
        get => authorPosition;
        set
        {
            authorPosition = value;
            File.WriteAllText(authorPath, authorPosition.ToString());
        }
    }
    
    public int AuthorCardsPosition
    {
        get => authorCardsPosition;
        set
        {
            authorCardsPosition = value;
            File.WriteAllText(authorCardsPath, authorCardsPosition.ToString());
        }
    }


    public Config(string configFolderPath)
    {
        if (Directory.Exists(configFolderPath))
        {
            {
                postPath = Path.Combine(configFolderPath, "post.txt");

                if (Path.Exists(postPath) != true)
                {
                    using FileStream stream = File.Create(postPath);
                    stream.Write("1"u8);
                }
                else
                {
                    string refStr = File.ReadAllText(postPath);
                    postPosition = int.Parse(refStr);
                }
            }

            {
                authorPath = Path.Combine(configFolderPath, "author.txt");
                if (Path.Exists(authorPath) != true)
                {
                    using FileStream stream = File.Create(authorPath);
                    stream.Write("1"u8);
                }
                else
                {
                    string refStr = File.ReadAllText(authorPath);
                    authorPosition = int.Parse(refStr);
                }
            }
            
            {
                authorCardsPath = Path.Combine(configFolderPath, "author_cards.txt");
                if (Path.Exists(authorCardsPath) != true)
                {
                    using FileStream stream = File.Create(authorCardsPath);
                    stream.Write("1"u8);
                }
                else
                {
                    string refStr = File.ReadAllText(authorCardsPath);
                    authorCardsPosition = int.Parse(refStr);
                }
            }
        }
        else
        {
            throw new DirectoryNotFoundException("找不到配置文件夹");
        }
    }
}
