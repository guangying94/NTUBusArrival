using Microsoft.Bot.Builder.Dialogs;

//For internal shuttle bus
public class Route
{
    public int id { get; set; }
    public string name { get; set; }
    public string short_name { get; set; }
}

public class Forecast
{
    public double forecast_seconds { get; set; }
    public Route route { get; set; }
    public int rv_id { get; set; }
    public double total_pass { get; set; }
    public string vehicle { get; set; }
    public int vehicle_id { get; set; }
}

public class Rootobject2
{
    public string external_id { get; set; }
    public IList<Forecast> forecast { get; set; }
    public int id { get; set; }
    public string name { get; set; }
    public string name_en { get; set; }
    public string name_ru { get; set; }
    public string nameslug { get; set; }
    public string resource_uri { get; set; }
}