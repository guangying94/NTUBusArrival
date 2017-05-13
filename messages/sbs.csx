using Microsoft.Bot.Builder.Dialogs;

public class Rootobject
{
    public Service[] services { get; set; }
}

public class Service
{
    public string no { get; set; }
    public Next next { get; set; }
    public Subsequent subsequent { get; set; }
}

public class Next
{
    public DateTime? time { get; set; }
    public object duration_ms { get; set; }
}

public class Subsequent
{
    public object time { get; set; }
    public object duration_ms { get; set; }
}