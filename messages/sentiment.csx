using Microsoft.Bot.Builder.Dialogs;

public class Document
{
    public double score { get; set; }
    public string id { get; set; }
}

public class Rootobject4
{
    public IList<Document> documents { get; set; }
    public IList<object> errors { get; set; }
}