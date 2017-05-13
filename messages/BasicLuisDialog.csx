#load "message.csx"
#load "sbs.csx"
#load "shuttleTime.csx"
#load "shuttleLocation.csx"
#load "sentiment.csx"
#load "vision.csx"

using System;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System.Collections.Generic;
using Microsoft.Bot.Connector;
using System.Net.Http.Headers;
using System.Web;
using System.Text;
using System.Globalization;
using System.Net;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

//Retrieve JSON for public bus
public class GetNextBusData
{
    public async static Task<Rootobject> GetBus(string BusStop)
    {
        var http = new HttpClient();
        string requestURL = "https://arrivelah.herokuapp.com/?id=" + BusStop;
        var response = await http.GetAsync(requestURL);
        var result = await response.Content.ReadAsStringAsync();
        Rootobject nextbusdata = JsonConvert.DeserializeObject<Rootobject>(result);
        return nextbusdata;
    }

    //Process public bus response
    public async static Task<string> BusTiming(string BusStop)
    {
        Rootobject nextBus = await GetNextBusData.GetBus(BusStop);
        string busResponse;
        if (Convert.ToInt32(nextBus.services[0].next.duration_ms) == 0)
        {
            busResponse = RandomPhrase.randomStopBus(nextBus.services[0].no);
        }
        else
        {
            double firstArrival = Convert.ToInt32(nextBus.services[0].next.duration_ms) / 60000;
            string busServiceNo = nextBus.services[0].no;
            if (firstArrival < 2)
            {
                double secondArrival = Convert.ToInt32(nextBus.services[0].subsequent.duration_ms) / 60000;
                if (secondArrival < 2)
                {
                    busResponse = RandomPhrase.randomTwoBusArriving(busServiceNo);
                }
                else
                {
                    string secondArrivalT = Math.Round(secondArrival, 0).ToString();
                    busResponse = RandomPhrase.randomOneBusArriving(busServiceNo, secondArrivalT);
                }
            }
            else
            {
                string firstArrivalT = Math.Round(firstArrival, 0).ToString();
                busResponse = RandomPhrase.randomOneBusArrival(busServiceNo, firstArrivalT);
            }
        }
        return busResponse;
    }
}

//Retrieve JSON for shuttle bus
public class GetShuttleBusData
{
    public async static Task<Rootobject2> GetShuttleBus(string BusStop)
    {
        var http = new HttpClient();
        string requestURL = "https://baseride.com/routes/api/platformbusarrival/" + BusStop + "/?format=json";
        var response = await http.GetAsync(requestURL);
        var result = await response.Content.ReadAsStringAsync();
        Rootobject2 nextbusdata = JsonConvert.DeserializeObject<Rootobject2>(result);
        return nextbusdata;
    }

    //Process shuttle bus ETA response
    public async static Task<string> ShuttleBusTiming(string ShuttleBusStop, string ShuttleBusColor)
    {
        Rootobject2 nextShuttleBus = await GetShuttleBusData.GetShuttleBus(ShuttleBusStop);
        int busCount = nextShuttleBus.forecast.Count;
        string shuttlebusResponse = "";
        List<double> forecastBus = new List<double>();
        int responseIndex = -1;
        for (int i = 0; i < busCount; i++)
        {
            if (nextShuttleBus.forecast[i].route.short_name.ToLower().Contains(ShuttleBusColor))
            {
                forecastBus.Add(nextShuttleBus.forecast[i].forecast_seconds / 60);
            }
            responseIndex = responseIndex + 1;
        }
        if (forecastBus.Count == 0)
        {
            shuttlebusResponse = "noBus";
        }
        else
        {
            if (forecastBus[0] < 2)
            {
                shuttlebusResponse = RandomPhrase.randomOneShuttleArriving(nextShuttleBus.name, nextShuttleBus.forecast[responseIndex].route.short_name);
            }
            else
            {
                shuttlebusResponse = RandomPhrase.randomOneShuttle(nextShuttleBus.name, nextShuttleBus.forecast[responseIndex].route.short_name, Math.Round(forecastBus[0], 0).ToString());
            }

            if (forecastBus.Count > 1)
            {
                int nextResponseCount = forecastBus.Count;
                string nextBusResponse = "";
                for (int j = 1; j < nextResponseCount; j++)
                {
                    nextBusResponse = nextBusResponse + " [" + Math.Round(forecastBus[j], 0).ToString() + "]";
                }
                shuttlebusResponse = shuttlebusResponse + $"  {Environment.NewLine} Upcoming bus (min): " + nextBusResponse;
            }
        }
        return shuttlebusResponse;
    }

    //Retrieve shuttle bus location
    public async static Task<Rootobject3> GetShuttleBusMap(string BusService)
    {
        var http = new HttpClient();
        string requestURL = "https://baseride.com/routes/apigeo/routevariantvehicle/" + BusService + "/?format=json";
        var response = await http.GetAsync(requestURL);
        var result = await response.Content.ReadAsStringAsync();
        Rootobject3 shuttleBusJSON = JsonConvert.DeserializeObject<Rootobject3>(result);
        return shuttleBusJSON;
    }
}

//Process map related response
public class CallBingMap
{
    //return map url, which will be used to reply as attachment
    public async static Task<string> ReturnShuttleBusURL(string BusColor)
    {
        Rootobject3 shuttleBusLocation = await GetShuttleBusData.GetShuttleBusMap(BusColor);
        int listCount = shuttleBusLocation.vehicles.Count;
        string shuttleBusURL = "";
        if (listCount == 0)
        {
            shuttleBusURL = "noBus";
        }
        else
        {
            string busLat;
            string busLon;
            string busSpeed;
            string busPP = "";
            string busIcon = "";
            string busAPIKey = "[Request Bing Map API key]";
            if (BusColor == "44480" || BusColor == "44481") //Brown line or green line
            {
                string mapRequestType = "http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/1.343388,103.688817/15?mapSize=520,400";
                if (BusColor == "44480")
                {
                    busIcon = "116";
                }
                else
                {
                    busIcon = "117";
                }
                for (int i = 0; i < listCount; i++)
                {
                    busLat = shuttleBusLocation.vehicles[i].lat;
                    busLon = shuttleBusLocation.vehicles[i].lon;
                    busSpeed = shuttleBusLocation.vehicles[i].speed;
                    double roundBusSpeed = Math.Round(Convert.ToDouble(busSpeed), 0);
                    busSpeed = roundBusSpeed.ToString();
                    busPP = busPP + "&pp=" + busLat + "," + busLon + ";" + busIcon + ";" + busSpeed;
                }
                shuttleBusURL = mapRequestType + busPP + "&key=" + busAPIKey;
            }
            else
            {
                string mapRequestType = "http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/1.347555,103.682993/15?mapSize=440,440";
                if (BusColor == "44478")
                {
                    busIcon = "7";
                }
                else
                {
                    busIcon = "1";
                }
                for (int i = 0; i < listCount; i++)
                {
                    busLat = shuttleBusLocation.vehicles[i].lat;
                    busLon = shuttleBusLocation.vehicles[i].lon;
                    busSpeed = shuttleBusLocation.vehicles[i].speed;
                    double roundBusSpeed = Math.Round(Convert.ToDouble(busSpeed), 0);
                    busSpeed = roundBusSpeed.ToString();
                    busPP = busPP + "&pp=" + busLat + "," + busLon + ";" + busIcon + ";" + busSpeed;
                }
                shuttleBusURL = mapRequestType + busPP + "&key=" + busAPIKey;
            }
        }
        return shuttleBusURL;
    }

    //Combined map for blue line and red line
    public async static Task<string> CombinedBusURL()
    {
        string shuttleBusURL = "";
        Rootobject3 shuttleBusLocationRed = await GetShuttleBusData.GetShuttleBusMap("44478");
        int listCountRed = shuttleBusLocationRed.vehicles.Count;
        Rootobject3 shuttleBusLocationBlue = await GetShuttleBusData.GetShuttleBusMap("44479");
        int listCountBlue = shuttleBusLocationBlue.vehicles.Count;
        string mapRequestType = "http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/1.347555,103.682993/15?mapSize=440,440";
        if (listCountRed + listCountBlue == 0)
        {
            shuttleBusURL = "noBus";
        }
        else
        {
            string busLat;
            string busLon;
            string busSpeed;
            string busPP = "";
            string busIcon = "";
            string busAPIKey = "[Request Bing Map API Key]";
            if (listCountRed != 0)
            {
                busIcon = "7";
                for (int i = 0; i < listCountRed; i++)
                {
                    busLat = shuttleBusLocationRed.vehicles[i].lat;
                    busLon = shuttleBusLocationRed.vehicles[i].lon;
                    busSpeed = shuttleBusLocationRed.vehicles[i].speed;
                    double roundBusSpeed = Math.Round(Convert.ToDouble(busSpeed), 0);
                    busSpeed = roundBusSpeed.ToString();
                    busPP = busPP + "&pp=" + busLat + "," + busLon + ";" + busIcon + ";" + busSpeed;
                }
            }
            if (listCountBlue != 0)
            {
                busIcon = "1";
                for (int i = 0; i < listCountBlue; i++)
                {
                    busLat = shuttleBusLocationBlue.vehicles[i].lat;
                    busLon = shuttleBusLocationBlue.vehicles[i].lon;
                    busSpeed = shuttleBusLocationBlue.vehicles[i].speed;
                    double roundBusSpeed = Math.Round(Convert.ToDouble(busSpeed), 0);
                    busSpeed = roundBusSpeed.ToString();
                    busPP = busPP + "&pp=" + busLat + "," + busLon + ";" + busIcon + ";" + busSpeed;
                }
            }
            shuttleBusURL = mapRequestType + busPP + "&key=" + busAPIKey;
        }
        return shuttleBusURL;
    }
}

//Random phrase library
public class RandomPhrase
{
    public static string randomGreeting()
    {
        List<string> replyGreeting = new List<string>();
        Random random = new Random();
        replyGreeting.Add("Hi, tell me a location and I'll tell you the \U0001F68C arrival time!");
        replyGreeting.Add("One stop bus service here! Tell me a bus stop I'll show you the \U0001F68C arrival time! ");
        int max = replyGreeting.Count;
        return replyGreeting[random.Next(0, max)];
    }

    public static string randomStopBus(string busServiceStop)
    {
        List<string> replyStopBus = new List<string>();
        Random random = new Random();
        replyStopBus.Add("Eh no " + busServiceStop + " bus now la what time izit ow");
        replyStopBus.Add("This timing bus drivers all rest liao lo. NO BUS! ");
        replyStopBus.Add("NO BUS I say NO BUS you understand?!");
        replyStopBus.Add("This timing...NO BUS!!");
        replyStopBus.Add(busServiceStop + " is not available now leh :(");
        replyStopBus.Add("LTA stopped all " + busServiceStop);
        int max = replyStopBus.Count;
        return replyStopBus[random.Next(0, max)];
    }
    public static string randomOneBusArrival(string busNo, string arrivalTiming)
    {
        List<string> replyOneBusArrive = new List<string>();
        Random random = new Random();
        replyOneBusArrive.Add($"Take your time.  {Environment.NewLine}" + busNo + " is arriving in " + arrivalTiming + " mins.");
        replyOneBusArrive.Add("Dude, " + busNo + " is coming in " + arrivalTiming + " mins.");
        replyOneBusArrive.Add("I repeat, " + busNo + " is reaching in " + arrivalTiming + " mins!");
        replyOneBusArrive.Add("Told ya, " + busNo + " is reaching in " + arrivalTiming + " mins.");
        replyOneBusArrive.Add("Alright! The " + busNo + " is arriving in " + arrivalTiming + " mins.");
        replyOneBusArrive.Add("Adacadabra! The " + busNo + " is arriving in " + arrivalTiming + "mins.");
        replyOneBusArrive.Add("Pika pika! " + busNo + " is reaching in " + arrivalTiming + " mins.");
        replyOneBusArrive.Add("Heyyy, " + busNo + " is arriving in " + arrivalTiming + " mins.");
        int max = replyOneBusArrive.Count;
        return replyOneBusArrive[random.Next(0, max)];
    }
    public static string randomOneBusArriving(string busNo2, string arrivalTiming2)
    {
        List<string> replyOneBusArriving = new List<string>();
        Random random = new Random();
        replyOneBusArriving.Add("RUN NOW! " + busNo2 + $" is arriving!  {Environment.NewLine}" + "Next bus: " + arrivalTiming2 + " mins.");
        replyOneBusArriving.Add("Chop chop lo " + busNo2 + $" is arriving!  {Environment.NewLine}" + "Next bus: " + arrivalTiming2 + " mins.");
        replyOneBusArriving.Add("OMG fly now! " + busNo2 + $" is arriving!  {Environment.NewLine}" + "Next bus: " + arrivalTiming2 + " mins.");
        replyOneBusArriving.Add("GG " + busNo2 + $" is arriving liao!  {Environment.NewLine}" + "Next bus: " + arrivalTiming2 + " mins.");
        int max = replyOneBusArriving.Count;
        return replyOneBusArriving[random.Next(0, max)];
    }
    public static string randomTwoBusArriving(string busNo3)
    {
        List<string> replyTwoBusArriving = new List<string>();
        Random random = new Random();
        replyTwoBusArriving.Add("Lucky you two " + busNo3 + " are arriving.");
        replyTwoBusArriving.Add("C'mon two " + busNo3 + " are coming already. GO!");
        replyTwoBusArriving.Add("Fast fast lar two " + busNo3 + " are arriving soon!");
        replyTwoBusArriving.Add("RUNNN, two " + busNo3 + " are gonna reach soon");
        replyTwoBusArriving.Add("Wa you damn ong, two " + busNo3 + " are gonna reach soon");
        int max = replyTwoBusArriving.Count;
        return replyTwoBusArriving[random.Next(0, max)];
    }
    public static string randomOneShuttleArriving(string busStop1, string busName1)
    {
        List<string> replyOneShuttleArriving = new List<string>();
        Random random = new Random();
        replyOneShuttleArriving.Add("RUN NOW! " + busName1 + " is arriving at " + busStop1 + " soon!");
        replyOneShuttleArriving.Add("Chop chop lo " + busName1 + " is arriving at " + busStop1 + " soon!");
        replyOneShuttleArriving.Add("OMG fly now! " + busName1 + " is reaching " + busStop1 + " real soon!");
        replyOneShuttleArriving.Add("GG " + busName1 + " is arriving at " + busStop1 + " liao!");
        int max = replyOneShuttleArriving.Count;
        return replyOneShuttleArriving[random.Next(0, max)];
    }
    public static string randomOneShuttle(string busStop2, string busName2, string busTime2)
    {
        List<string> replyOneShuttle = new List<string>();
        Random random = new Random();
        replyOneShuttle.Add("Alright, " + busName2 + " is arriving at " + busStop2 + " in " + busTime2 + " mins.");
        replyOneShuttle.Add("Hey there, " + busName2 + " is reaching " + busStop2 + " in " + busTime2 + " mins.");
        replyOneShuttle.Add("Take your time, " + busName2 + " is arriving at " + busStop2 + " in " + busTime2 + " mins.");
        replyOneShuttle.Add("Check your watch, " + busName2 + " is arriving at " + busStop2 + " in " + busTime2 + " mins.");
        replyOneShuttle.Add("Wear pretty pretty ady? " + busName2 + " is arriving at " + busStop2 + " in " + busTime2 + " mins.");
        int max = replyOneShuttle.Count;
        return replyOneShuttle[random.Next(0, max)];
    }
    public static string randomNoShuttle(string busColor1)
    {
        List<string> replyNoShuttle = new List<string>();
        Random random = new Random();
        replyNoShuttle.Add(busColor1 + " driver said he pang kang liao.");
        replyNoShuttle.Add("NTU asked all " + busColor1 + " drivers stop driving already.");
        replyNoShuttle.Add("Today no special event leh, " + busColor1 + " drivers no OT, all went home liao.");
        replyNoShuttle.Add("The last " + busColor1 + " driver went home already hehe");
        int max = replyNoShuttle.Count;
        return replyNoShuttle[random.Next(0, max)];
    }
    public static string randomNoneIntentGood()
    {
        List<string> replyNoneIntentGood = new List<string>();
        Random random = new Random();
        replyNoneIntentGood.Add("Sorry, I'm only 1 month old, I only know how to check bus arrival time :/ Can you just ask me stuff like \"red line from hall 11\" hehe ");
        replyNoneIntentGood.Add("I'm not AI, I'm bot :P So just ask me stuff like \"blue line from koufu\" ok? :P");
        replyNoneIntentGood.Add("You should know I'm just a bot, not human right? I don't understand the complication of human world :/ I will only answer you if you ask me a bus location :D");
        replyNoneIntentGood.Add("Okay the number is 8888. Ow wait I thought you're asking my number plat. Sorry :/ I know nothing other than \"red line from hall 11\"");
        int max = replyNoneIntentGood.Count;
        return replyNoneIntentGood[random.Next(0, max)];
    }
    public static string randomNoneIntentBad()
    {
        List<string> replyNoneIntentBad = new List<string>();
        Random random = new Random();
        replyNoneIntentBad.Add("Come on, please don't scold me :( I'm just a bot :(");
        replyNoneIntentBad.Add("TT I'm just a bot :(");
        replyNoneIntentBad.Add("No, I don't understand what you saying doesn't mean you can scold me :'(");
        replyNoneIntentBad.Add("Why you scold me? :( I got feelings one okay :( ");
        replyNoneIntentBad.Add("Don't scold me leh, I will cry one :'(");
        int max = replyNoneIntentBad.Count;
        return replyNoneIntentBad[random.Next(0, max)];
    }
}

//Check sentiment of incoming message
//Find out more here: https://www.microsoft.com/cognitive-services/en-us/text-analytics-api
public class SentimentAnalysis
{
    private const string baseURL = "https://westus.api.cognitive.microsoft.com/";
    private const string AccountKey = "[Request text analytics API key]";
    public static async Task<double> MakeRequests(string input)
    {
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(baseURL);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", AccountKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            byte[] byteData = Encoding.UTF8.GetBytes("{\"documents\":[" +
            "{\"id\":\"1\",\"text\":\"" + input + "\"},]}");
            var uri = "text/analytics/v2.0/sentiment";
            var response = await CallEndpoint(client, uri, byteData);
            return response.documents[0].score;
        }
    }

    //retrieve JSON for sentiment analysis
    public static async Task<Rootobject4> CallEndpoint(HttpClient client, string uri, byte[] byteData)
    {
        using (var content = new ByteArrayContent(byteData))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var response = await client.PostAsync(uri, content);
            var result = await response.Content.ReadAsStringAsync();
            Rootobject4 sentimentJSON = JsonConvert.DeserializeObject<Rootobject4>(result);
            return sentimentJSON;
        }
    }
}

//Code that handle natural language processing
//Find out more here: https://www.microsoft.com/cognitive-services/en-us/language-understanding-intelligent-service-luis
[Serializable]
public class BasicLuisDialog : LuisDialog<object>
{
    public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(Utils.GetAppSetting("LuisAppId"), Utils.GetAppSetting("LuisAPIKey"))))
    {
    }

    //presisted stored variable
    protected string busLocation = "";
    protected string userBusColor = "";
    //Dictionary for bus stop code
    protected Dictionary<string, Dictionary<string, string>> BusDict = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "innovation",
                        new Dictionary<string,string>
                        {
                            {"red","378229"},
                            {"brown","378229"},
                            {"blue","383006"},
                            {"179","27251"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "library",
                        new Dictionary<string,string>
                        {
                            {"red","378224"},
                            {"brown","378224"},
                            {"blue","378225"},
                            {"199","27219"},
                            {"179","27211"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "pioneer",
                        new Dictionary<string,string>
                        {
                            {"green","377906"},
                            {"brown","377906"},
                            {"179","22459"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "cee",
                        new Dictionary<string,string>
                        {
                            {"blue","378226"},
                            {"brown","383015"},
                            {"red","382995"},
                            {"179","27211"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "adm",
                        new Dictionary<string,string>
                        {
                            {"brown","378207"},
                            {"179","27061"},
                            {"199","27069"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "eee",
                        new Dictionary<string,string>
                        {
                            {"red","378227"},
                            {"brown","378227"},
                            {"blue","383010"},
                            {"179","27231"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "northHill",
                        new Dictionary<string,string>
                        {
                            {"red","378202"},
                            {"blue","378222"},
                            {"199","27199"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "tct",
                        new Dictionary<string,string>
                        {
                            {"green","383013"},
                            {"brown","383013"},
                            {"typo \U0001F613","end"}

                        }
                    },
                    {
                        "ssc",
                        new Dictionary<string,string>
                        {
                            {"green","383011"},
                            {"brown","383011"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "1",
                        new Dictionary<string,string>
                        {
                            {"red","378233"},
                            {"brown","378233"},
                            {"green","378233"},
                            {"179","27281"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "2",
                        new Dictionary<string,string>
                        {
                            {"red","378237"},
                            {"brown","378237"},
                            {"green (can 2)","378237"},
                            {"green (opposite can 2)","383014"},
                            {"179","27311"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "3",
                        new Dictionary<string,string>
                        {
                            {"red","378204"},
                            {"blue","382999"},
                            {"199","27031"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "4",
                        new Dictionary<string,string>
                        {
                            {"red","378230"},
                            {"blue","383004"},
                            {"brown","378230"},
                            {"179","27261"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "5",
                        new Dictionary<string,string>
                        {
                            {"red","378230"},
                            {"blue","383004"},
                            {"brown","383018"},
                            {"179","27261"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "6",
                        new Dictionary<string,string>
                        {
                            {"blue","378234"},
                            {"179","27291"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "7",
                        new Dictionary<string,string>
                        {
                            {"red","378228"},
                            {"brown","378228"},
                            {"179","27241"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "8",
                        new Dictionary<string,string>
                        {
                            {"brown","378207"},
                            {"179","27061"},
                            {"199","27069"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "9",
                        new Dictionary<string,string>
                        {
                            {"red","382998"},
                            {"blue","383003"},
                            {"199","27209"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "10",
                        new Dictionary<string,string>
                        {
                            {"blue","383003"},
                            {"199","27011"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "11",
                        new Dictionary<string,string>
                        {
                            {"red","378202"},
                            {"blue","378222"},
                            {"199 (grad hall)","27011"},
                            {"199 (northhill)","27199"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "12",
                        new Dictionary<string,string>
                        {
                            {"red","378204"},
                            {"blue","382999"},
                            {"199","27031"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "13",
                        new Dictionary<string,string>
                        {
                            {"red","378204"},
                            {"blue","382999"},
                            {"199","27031"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "14",
                        new Dictionary<string,string>
                        {
                            {"blue","378203"},
                            {"199","27021"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "15",
                        new Dictionary<string,string>
                        {
                            {"blue","378203"},
                            {"199","27021"},
                            {"typo \U0001F613","end"}
                        }
                    },
                    {
                        "16",
                        new Dictionary<string,string>
                        {
                            {"red","378204"},
                            {"blue","382999"},
                            {"199","27031"},
                            {"typo \U0001F613","end"}
                        }
                    },
                };


    //Handle unknown intent
    //"test patch version" is for internal log purpose
    //"Unknown intent will go thru sentiment analysis
    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        if (result.Query.ToLower() == "test patch version")
        {
            await context.PostAsync("This is version 1.3.3");
            context.Wait(MessageReceivedAsync);
        }
        else
        {
            Double testResponse = await SentimentAnalysis.MakeRequests(result.Query);
            if (testResponse < 0.5)
            {
                await context.PostAsync(RandomPhrase.randomNoneIntentBad());
            }
            else
            {
                await context.PostAsync(RandomPhrase.randomNoneIntentGood());
            }
            context.Wait(MessageReceived);
        }
    }

    //Check available buses
    [LuisIntent("AnyBus")]
    public async Task AnyBus(IDialogContext context, LuisResult result)
    {
        if (result.Query.ToLower().Contains("red"))
        {
            await SendMapImage(context, "44478", "red");
        }
        else if (result.Query.ToLower().Contains("blue"))
        {
            await SendMapImage(context, "44479", "blue");
        }
        else
        {
            string returnMapURL = await CallBingMap.CombinedBusURL();
            if (returnMapURL == "noBus")
            {
                await context.PostAsync("Sorry, there's no shuttle bus now.");
            }
            else
            {
                var replyMap = context.MakeMessage();
                replyMap.Attachments = new List<Attachment>()
            {
                new Attachment()
                {
                    ContentUrl = returnMapURL,
                    ContentType = "image/png",
                    Name = "Map.png"
                }
            };
                await context.PostAsync(replyMap);
            }
        }
        context.Wait(MessageReceived);
    }

    //Greeting intent
    [LuisIntent("Greeting")]
    public async Task Greeting(IDialogContext context, LuisResult result)
    {
        await context.PostAsync(RandomPhrase.randomGreeting());
        var replyIntroMap = context.MakeMessage();
        replyIntroMap.Attachments = new List<Attachment>()
        {
            new Attachment()
            {
                ContentUrl = "[add your own image URL]",
                ContentType = "image/png",
                Name = "Map.png"
            }
        };
        await context.PostAsync(replyIntroMap);
        await context.PostAsync("Refer to the map above for available bus stop!");
        context.Wait(MessageReceived);
    }

    //Valentines easter egg
    [LuisIntent("Valentines")]
    public async Task Valentines(IDialogContext context, LuisResult result)
    {
        await context.PostAsync("Wassup! Don't fall in love with me leh, I'm just a bot :/");
        context.Wait(MessageReceived);
    }

    //Check bus arrival time
    //noticed that natural language may not fully understand acronyms
    //including not proper english
    //lee wee nam and wee kim wee, it may detect "wee" and categorize as same place
    //so there's some manual adjustment here by testing
    //if you have better ideas, please improve here :P

    //LUIS will produce 2 results, intent and entities.
    //Usually there's only 1 intent, but we can have more entities
    //here we extract the entities and store in a list to check the place
    [LuisIntent("BusArrivalTime")]
    public async Task BusArrivalTime(IDialogContext context, LuisResult result)
    {
        if (result.Entities.Count == 0)
        {// without any bus location
            if (result.Query.ToLower().Contains("spms"))
            {
                await PromptChoiceBus(context, "innovation");
            }
            else if (result.Query.ToLower().Contains("lwn"))
            {
                await PromptChoiceBus(context, "library");
            }
            else if (result.Query.ToLower().Contains("wkw"))
            {
                await PromptChoiceBus(context, "eee");
            }
            else if (result.Query.ToLower().Contains("koufu") || result.Query.ToLower().Contains("kofu"))
            {
                await PromptChoiceBus(context, "innovation");
            }
            else if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan") || result.Query.ToLower().Contains("administration"))
            {
                await PromptChoiceBus(context, "tct");
            }
            else if (result.Query.ToLower().Contains("ssc") || result.Query.ToLower().Contains("medical"))
            {
                await PromptChoiceBus(context, "ssc");
            }
            else
            {
                await context.PostAsync("Sorry, I didn't get the place, can you try alternative name?");
                context.Wait(MessageReceived);
            }
        }
        else
        {//with bus location, then save the result as a list
            List<string> entitiesType = new List<string>();
            List<string> entitiesName = new List<string>();
            for (int i = 0; i < result.Entities.Count; i++)
            {
                entitiesType.Add(result.Entities[i].Type);
                entitiesName.Add(result.Entities[i].Entity);
            }
            //we start from processing the queries with bus option
            //then follow up by without bus option
            if (entitiesType.Contains("busColor"))
            {
                string chosenBus = entitiesName[entitiesType.IndexOf("busColor")];
                chosenBus = chosenBus.ToLower();
                this.userBusColor = chosenBus;
                if (entitiesType.Contains("hallNumber"))
                {
                    //hall number
                    string hallIndex = entitiesName[entitiesType.IndexOf("hallNumber")];
                    this.busLocation = hallIndex;
                    if (this.busLocation == "2")
                    {
                        if (chosenBus.Contains("green"))
                        {
                            await ReplyPreviousBus(context, "green (can 2)");
                            await ReplyPreviousBus(context, "green (opposite can 2)");
                        }
                        else
                        {
                            await ReplyPreviousBus(context, chosenBus);
                        }
                    }
                    else if (this.busLocation == "11")
                    {
                        if (chosenBus.Contains("199"))
                        {
                            await context.PostAsync("I check 199 for both Grad Hall & Hall 11 ok? Grad Hall arrival timing, followed by Hall 11 arrival timing");
                            await ReplyPreviousBus(context, "199 (grad hall)");
                            await ReplyPreviousBus(context, "199 (northHill)");
                        }
                        else
                        {
                            await ReplyPreviousBus(context, chosenBus);
                        }
                    }
                    else
                    {
                        await ReplyPreviousBus(context, chosenBus);
                    }
                }
                else if (entitiesType.Contains("library"))
                { //check entities, and there's condition to fix acronyms for ntu places
                    if (result.Query.ToLower().Contains("wkw") || result.Query.ToLower().Contains("kim"))
                    {
                        this.busLocation = "eee";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan"))
                    {
                        this.busLocation = "tct";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else
                    {
                        this.busLocation = "library";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                }
                else if (entitiesType.Contains("innovation"))
                {
                    if (result.Query.ToLower().Contains("lwn"))
                    {
                        this.busLocation = "library";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("wkw"))
                    {
                        this.busLocation = "eee";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan"))
                    {
                        this.busLocation = "tct";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else
                    {
                        this.busLocation = "innovation";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                }
                else if (entitiesType.Contains("pioneer"))
                {
                    if (result.Query.ToLower().Contains("hall"))
                    {
                        this.busLocation = "1";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else
                    {
                        this.busLocation = "pioneer";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                }
                else if (entitiesType.Contains("adm"))
                {
                    this.busLocation = "adm";
                    await ReplyPreviousBus(context, chosenBus);
                }
                else if (entitiesType.Contains("cee"))
                {
                    this.busLocation = "cee";
                    await ReplyPreviousBus(context, chosenBus);
                }
                else if (entitiesType.Contains("eee"))
                {
                    this.busLocation = "eee";
                    await ReplyPreviousBus(context, chosenBus);
                }
                else if (entitiesType.Contains("northHill"))
                {
                    this.busLocation = "northHill";
                    await ReplyPreviousBus(context, chosenBus);
                }
                // remaining uncaptured error or result
                else
                {
                    if (result.Query.ToLower().Contains("lwn"))
                    {
                        this.busLocation = "library";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("wkw"))
                    {
                        this.busLocation = "eee";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("sbs"))
                    {
                        this.busLocation = "cee";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("spms"))
                    {
                        this.busLocation = "innovation";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("ssc") || result.Query.ToLower().Contains("medical"))
                    {
                        this.busLocation = "ssc";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan") || result.Query.ToLower().Contains("administration"))
                    {
                        this.busLocation = "tct";
                        await ReplyPreviousBus(context, chosenBus);
                    }
                    else
                    {
                        if (this.busLocation == "")
                        {
                            await context.PostAsync("I can't read your mind, can tell me which bus stop?");
                        }
                        else
                        {
                            await ReplyPreviousBus(context, result.Query);
                        }
                    }
                }
                context.Wait(MessageReceived);
            }
            else
            {
                //without specific bus
                if (entitiesType.Contains("hallNumber"))
                {
                    //hall number
                    string hallIndex = entitiesName[entitiesType.IndexOf("hallNumber")];
                    if (this.BusDict.ContainsKey(hallIndex))
                    {
                        await PromptChoiceBus(context, hallIndex);
                    }
                    else
                    {
                        await context.PostAsync("Sorry, I can't find any bus here :(");
                        context.Wait(MessageReceived);
                    }
                }
                else if (entitiesType.Contains("library"))
                {
                    //lwn
                    if (result.Query.ToLower().Contains("kim"))
                    {
                        await PromptChoiceBus(context, "eee");
                    }
                    else if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan"))
                    {
                        await PromptChoiceBus(context, "tct");
                    }
                    else
                    {
                        await PromptChoiceBus(context, "library");
                    }

                }
                else if (entitiesType.Contains("innovation"))
                {
                    //koufu
                    if (result.Query.ToLower().Contains("tct") || result.Query.ToLower().Contains("admin") || result.Query.ToLower().Contains("tuan"))
                    {
                        await PromptChoiceBus(context, "tct");
                    }
                    else
                    {
                        await PromptChoiceBus(context, "innovation");
                    }
                }
                else if (entitiesType.Contains("pioneer"))
                {
                    //pioneer
                    if (result.Query.ToLower().Contains("hall"))
                    {
                        await PromptChoiceBus(context, "1");
                    }
                    else
                    {
                        await PromptChoiceBus(context, "pioneer");
                    }
                }
                else if (entitiesType.Contains("adm"))
                {
                    //adm
                    await PromptChoiceBus(context, "adm");
                }
                else if (entitiesType.Contains("cee"))
                {
                    //cee, sbs
                    await PromptChoiceBus(context, "cee");
                }
                else if (entitiesType.Contains("eee"))
                {
                    //eee, wkw
                    await PromptChoiceBus(context, "eee");
                }
                else if (entitiesType.Contains("northHill"))
                {
                    //northHill
                    await PromptChoiceBus(context, "northHill");
                }
                else
                {
                    await context.PostAsync("Sorry, I can't find this location, are you missing a space?");
                    context.Wait(MessageReceived);
                }
            }
        }
    }

    //reply with attachment (map image)
    private async Task ReplyWithMap(IDialogContext context, string shuttleColorCode, string shuttleColor)
    {
        Rootobject3 shuttleBusMap = await GetShuttleBusData.GetShuttleBusMap(shuttleColorCode);
        if (shuttleBusMap.vehicles.Count == 0)
        {
            await context.PostAsync(RandomPhrase.randomNoShuttle(shuttleColor));
        }
        else
        {
            var replyMap = context.MakeMessage();
            string returnMapURL = await CallBingMap.ReturnShuttleBusURL(shuttleColorCode);
            this.userBusColor = shuttleColor;
            if (replyMap.ChannelId.Equals("facebook"))
            {
                replyMap.Attachments = new List<Attachment>()
           {
               new Attachment()
               {
                   ContentUrl = returnMapURL,
                   ContentType = "image/png",
                   Name = "Map.png"
               }
           };
                await context.PostAsync(replyMap);
            }
            else
            {
                List<CardImage> cardImages = new List<CardImage>();
                cardImages.Add(new CardImage(url: returnMapURL));
                List<CardAction> cardButtons = new List<CardAction>();
                if (shuttleColor == "red")
                {
                    CardAction plButton = new CardAction()
                    {
                        Value = "blue map",
                        Type = "imBack",
                        Title = "Blue Map"
                    };
                    cardButtons.Add(plButton);
                }
                if (shuttleColor == "blue")
                {
                    CardAction plButton2 = new CardAction()
                    {
                        Value = "red map",
                        Type = "imBack",
                        Title = "Red Map"
                    };
                    cardButtons.Add(plButton2);
                }
                CardAction plButton3 = new CardAction()
                {
                    Value = "map",
                    Type = "imBack",
                    Title = "Combined Map"
                };
                cardButtons.Add(plButton3);

                CardAction plButton4 = new CardAction()
                {
                    Value = this.busLocation + " " + this.userBusColor,
                    Type = "imBack",
                    Title = "Again"
                };
                cardButtons.Add(plButton4);

                HeroCard plCard = new HeroCard()
                {
                    Title = "Shuttle Bus Map",
                    Subtitle = "Numbers are speed of buses",
                    Images = cardImages,
                    Buttons = cardButtons
                };
                replyMap.Attachments = new List<Attachment>();
                Attachment plAttachment = plCard.ToAttachment();
                replyMap.Attachments.Add(plAttachment);
                await context.PostAsync(replyMap);
            }
        }

    }

    //functions that create the reply when asking bus arrival time
    private async Task ReplyPreviousBus(IDialogContext context, string newBus)
    {
        string lower = newBus.ToLower();
        newBus = lower;
        if (this.BusDict[this.busLocation].ContainsKey(newBus))
        {
            string query = this.BusDict[this.busLocation][newBus];
            this.userBusColor = newBus;
            if (newBus.Contains("179") || newBus.Contains("199"))
            {
                string responseMsg = await GetNextBusData.BusTiming(query);
                await context.PostAsync(responseMsg);
            }
            else
            {
                if (newBus.Contains("green") && newBus.Length > 5)
                {
                    newBus = newBus.Remove(5);
                }
                string responseMsg = await GetShuttleBusData.ShuttleBusTiming(query, newBus);
                if (responseMsg == "noBus")
                {
                    if (newBus.Contains("green"))
                    {
                        await ReplyWithMap(context, "44480", "green");
                    }
                    else if (newBus.Contains("brown"))
                    {
                        await ReplyWithMap(context, "44481", "brown");
                    }
                    else if (newBus.Contains("red"))
                    {
                        await ReplyWithMap(context, "44478", "red");
                    }
                    else
                    {
                        await ReplyWithMap(context, "44479", "blue");
                    }
                }
                else
                {
                    await context.PostAsync(responseMsg);
                }
            }
        }
        else
        {
            await context.PostAsync("Sorry, I don't think there's " + newBus + " bus here.");
        }
    }

    //process the dialog when user has chosen the bus
    private async Task BusResultAsync(IDialogContext context, IAwaitable<string> arguement)
    {
        try
        {
            string chosenChoice = await arguement;
            string query = this.BusDict[this.busLocation][chosenChoice];
            if (query == "end")
            {
                await context.PostAsync("tsk, type slowly next time \U0001F612");
            }
            else
            {
                if (chosenChoice.Contains("179") || chosenChoice.Contains("199"))
                {
                    string responseMsg = await GetNextBusData.BusTiming(query);
                    await context.PostAsync(responseMsg);
                }
                else
                {
                    if (chosenChoice.Contains("green") && chosenChoice.Length > 5)
                    {
                        chosenChoice = chosenChoice.Remove(5);
                    }
                    this.userBusColor = chosenChoice;
                    string responseMsg = await GetShuttleBusData.ShuttleBusTiming(query, chosenChoice);
                    if (responseMsg == "noBus")
                    {
                        if (chosenChoice.Contains("green"))
                        {
                            await ReplyWithMap(context, "44480", "green");
                        }
                        else if (chosenChoice.Contains("brown"))
                        {
                            await ReplyWithMap(context, "44481", "brown");
                        }
                        else if (chosenChoice.Contains("red"))
                        {
                            await ReplyWithMap(context, "44478", "red");
                        }
                        else
                        {
                            await ReplyWithMap(context, "44479", "blue");
                        }
                    }
                    else
                    {
                        await context.PostAsync(responseMsg);
                    }
                }
            }
            context.Wait(MessageReceived);
        }

        catch (TooManyAttemptsException)
        {
            await context.PostAsync("Walao, choose what I show you ok? \U0001F629");
            context.Wait(MessageReceived);
        }
    }

    //bot framework that prompt the user to choose option
    private async Task PromptChoiceBus(IDialogContext context, string busLocation2)
    {
        List<string> chooseOption = new List<string>(this.BusDict[busLocation2].Keys);
        this.busLocation = busLocation2;
        PromptDialog.Choice(
        context,
        BusResultAsync,
        chooseOption,
        "Can tell me which \U0001F68C?",
        "I gave up on you \U0001F335"
        );
    }

    //similar to ReplyWithMap
    //duplication, can be removed
    private async Task SendMapImage(IDialogContext context, string busCode, string busColor3)
    {
        Rootobject3 shuttleBusMap = await GetShuttleBusData.GetShuttleBusMap(busCode);
        if (shuttleBusMap.vehicles.Count == 0)
        {
            await context.PostAsync(RandomPhrase.randomNoShuttle(busColor3));
        }
        else
        {
            var replyMap = context.MakeMessage();
            string returnMapURL = await CallBingMap.ReturnShuttleBusURL(busCode);
            if (replyMap.ChannelId.Equals("facebook"))
            {
                replyMap.Attachments = new List<Attachment>()
           {
               new Attachment()
               {
                   ContentUrl = returnMapURL,
                   ContentType = "image/png",
                   Name = "Map.png"
               }
           };
                await context.PostAsync(replyMap);
            }
            else
            {
                List<CardImage> cardImages = new List<CardImage>();
                cardImages.Add(new CardImage(url: returnMapURL));
                List<CardAction> cardButtons = new List<CardAction>();

                if (busColor3 == "red")
                {
                    CardAction plButton = new CardAction()
                    {
                        Value = "blue map",
                        Type = "imBack",
                        Title = "Blue Map"
                    };
                    cardButtons.Add(plButton);
                }

                if (busColor3 == "blue")
                {
                    CardAction plButton2 = new CardAction()
                    {
                        Value = "red map",
                        Type = "imBack",
                        Title = "Red Map"
                    };
                    cardButtons.Add(plButton2);
                }

                CardAction plButton3 = new CardAction()
                {
                    Value = "map",
                    Type = "imBack",
                    Title = "Combined Map"
                };
                cardButtons.Add(plButton3);

                CardAction plButton4 = new CardAction()
                {
                    Value = this.busLocation + " " + this.userBusColor,
                    Type = "imBack",
                    Title = "Again"
                };
                cardButtons.Add(plButton4);

                HeroCard plCard = new HeroCard()
                {
                    Title = "Shuttle Bus Map",
                    Subtitle = busColor3 + " bus map",
                    Images = cardImages,
                    Buttons = cardButtons
                };
                replyMap.Attachments = new List<Attachment>();
                Attachment plAttachment = plCard.ToAttachment();
                replyMap.Attachments.Add(plAttachment);
                await context.PostAsync(replyMap);
            }
        }
    }
}