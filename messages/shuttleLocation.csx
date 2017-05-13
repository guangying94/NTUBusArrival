using Microsoft.Bot.Builder.Dialogs;

public class Enterprise
{
    public int enterprise_id { get; set; }
    public string enterprise_name { get; set; }
}

public class Park
{
    public int park_id { get; set; }
    public string park_name { get; set; }
}

public class Position
{
    public int bearing { get; set; }
    public int device_ts { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }
    public int speed { get; set; }
    public int ts { get; set; }
}

public class Projection
{
    public string edge_distance { get; set; }
    public int edge_id { get; set; }
    public string edge_projection { get; set; }
    public int edge_start_node_id { get; set; }
    public int edge_stop_node_id { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }
    public string orig_lat { get; set; }
    public string orig_lon { get; set; }
    public int routevariant_id { get; set; }
    public int ts { get; set; }
}

public class Stats
{
    public string avg_speed { get; set; }
    public int bearing { get; set; }
    public string cumm_speed_10 { get; set; }
    public string cumm_speed_2 { get; set; }
    public int device_ts { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }
    public int speed { get; set; }
    public int ts { get; set; }
}

public class Vehicle
{
    public int bearing { get; set; }
    public DateTime device_ts { get; set; }
    public Enterprise enterprise { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }
    public Park park { get; set; }
    public Position position { get; set; }
    public Projection projection { get; set; }
    public string registration_code { get; set; }
    public int routevariant_id { get; set; }
    public string speed { get; set; }
    public Stats stats { get; set; }
    public DateTime ts { get; set; }
    public int vehicle_id { get; set; }
}

public class Rootobject3
{
    public object external_id { get; set; }
    public int id { get; set; }
    public string name { get; set; }
    public object name_en { get; set; }
    public object name_ru { get; set; }
    public object nameslug { get; set; }
    public string resource_uri { get; set; }
    public string routename { get; set; }
    public IList<Vehicle> vehicles { get; set; }
    public object via { get; set; }
}