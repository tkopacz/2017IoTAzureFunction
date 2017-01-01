#r "Newtonsoft.Json"


using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

///https://pltkw3funconsumption.azurewebsites.net/api/TKSendToIoTHub?module=A1&function=F1&val=123
public static DeviceClient deviceClient = null;

public static string GetCookie(this HttpRequestMessage request, string cookieName)
{
    CookieHeaderValue cookie = request.Headers.GetCookies(cookieName).FirstOrDefault();
    if (cookie != null)
        return cookie[cookieName].Value;

    return null;
}
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    HttpResponseMessage resp;
    try {
    //log.Info("C# HTTP trigger function processed a request.");
    if (deviceClient == null)
    {
        var connectionString = Environment.GetEnvironmentVariable("DeviceClient");
        deviceClient = DeviceClient.CreateFromConnectionString(connectionString, Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only);
        log.Info("IoT connection created");
    }
    string strTrack, strCnt;

    strTrack = req.GetCookie("track-id");
    if (strTrack == null)
    {
        strTrack = Guid.NewGuid().ToString();
    }
    strCnt = req.GetCookie("track-cnt");
    if (strCnt == null)
    {
        strCnt = "1";
    }
    else
    {
        int cnt=0;
        if (int.TryParse(strCnt, out cnt))
        {
            strCnt = (cnt+1).ToString();
        }
        else
        {
            strCnt = "1";
        }

    }
    var cookie_track = new CookieHeaderValue("track-id", strTrack);
    cookie_track.Expires = DateTimeOffset.Now.AddDays(365);
    cookie_track.Domain = req.RequestUri.Host.ToString();
    cookie_track.Path = "/";

    var cookie_cnt = new CookieHeaderValue("track-cnt", strCnt);
    cookie_cnt.Expires = DateTimeOffset.Now.AddDays(365);
    cookie_cnt.Domain = req.RequestUri.Host.ToString();
    cookie_cnt.Path = "/";
    //log.Info("BeginSending");
    JObject val = new JObject();
    val.Add("track-id", strTrack);
    val.Add("track-cnt", strCnt);
    val.Add("Method", req.Method.ToString());
    val.Add("Content", await req.Content.ReadAsStringAsync());
    JObject queries = new JObject();
    var query = req.RequestUri.ParseQueryString();
    foreach (var item in query.AllKeys)
    {
        queries.Add("query-" + item,query[item]);
    }
    val.Add("QueryString",queries);


    JObject headers = new JObject();
    foreach (var item in req.Headers)
    {
        JArray tmp = new JArray();
        foreach (var item1 in item.Value)
        {
            tmp.Add(item1);

        }
        headers.Add(item.Key, tmp);
    }
    val.Add("Headers", headers);

    JObject properties = new JObject();
    foreach (var item in req.Properties)
    {
        properties.Add(item.Key, item.Value.ToString());
    }
    val.Add("Properties", properties);



    var messageString = JsonConvert.SerializeObject(val);
    var msg = new Message(Encoding.UTF8.GetBytes(messageString));

    await deviceClient.SendEventAsync(msg);
    resp = req.CreateResponse(HttpStatusCode.OK);
    resp.Headers.AddCookies(new CookieHeaderValue[] { cookie_track, cookie_cnt });
    } catch (Exception ex) {
        resp = req.CreateResponse(HttpStatusCode.InternalServerError,ex.ToString());
            
    }
    return resp;
}