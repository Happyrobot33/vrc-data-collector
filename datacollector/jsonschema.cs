// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
using Newtonsoft.Json;

public class PlayerDataCollected
{
    [JsonProperty("display name")]
    public string displayname { get; set; }

    [JsonProperty("player id")]
    public int playerid { get; set; }
    public Position position { get; set; }
/*     [JsonProperty("left eye position")]
    public Position lefteyeposition { get; set; }
    [JsonProperty("left eye rotation")]
    public Quaternion lefteyerotation { get; set; }
    [JsonProperty("right eye position")]
    public Position righteyeposition { get; set; }
    [JsonProperty("right eye rotation")]
    public Quaternion righteyerotation { get; set; } */
    [JsonProperty("head position")]
    public Position headposition { get; set; }
    [JsonProperty("head rotation")]
    public Quaternion headrotation { get; set; }
    [JsonProperty("right hand position")]
    public Position righthandposition { get; set; }
    [JsonProperty("right hand rotation")]
    public Quaternion righthandrotation { get; set; }
    [JsonProperty("left hand position")]
    public Position lefthandposition { get; set; }
    [JsonProperty("left hand rotation")]
    public Quaternion lefthandrotation { get; set; }
    public double rotation { get; set; }
    public double size { get; set; }
    public bool vr { get; set; }
    public double fps { get; set; }
}

public class Position
{
    public double x { get; set; }
    public double y { get; set; }
    public double z { get; set; }
}

public class Quaternion
{
    public double x { get; set; }
    public double y { get; set; }
    public double z { get; set; }
    public double w { get; set; }
}

public class DataRoot
{
    [JsonProperty("utc time")]
    public long utctime { get; set; }

    [JsonProperty("total players")]
    public int totalplayers { get; set; }

    [JsonProperty("world id")]
    public string worldid { get; set; }

    [JsonProperty("player data collected")]
    public List<PlayerDataCollected> playerdatacollected { get; set; }
}
