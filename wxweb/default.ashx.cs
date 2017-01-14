using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;
using System.Web.Script.Serialization;


namespace wxweb
{
    /// <summary>
    /// Summary description for _default
    /// </summary>
    public class _default : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
           
            //获取微信消息xml
            requestFromWX wx = readMessage(context);

            //分析词汇
            string target = wx.Content.Content;
            string Msg = "请输入城市名+天气查询某地天气，如:上海天气";
            if (target.Contains("天气"))
            {
                string cityName = target.Replace("天气", "");
                Utils.WriteTextLog("", cityName);
                //进行天气API查找，JSON化，返回content,预读缓存
                Msg = GetWeatherInfoByCity(cityName);
            }

           
            //context.Response.Write(Msg);
            sendToWX(context, Msg, wx);
         
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
       

        public string readyHttpContent(HttpContext Content)
        {
            Stream st = Content.Request.InputStream;
            StreamReader sr = new StreamReader(st);
            return sr.ReadToEnd();
        }

        public requestFromWX readMessage(HttpContext Content)
        {
            requestFromWX wx = new requestFromWX();
            HttpRequest req = Content.Request;
            wx.signature = req["signature"];
            wx.timestamp = req["timestamp"];
            wx.nonce = req["nonce"];
            wx.openid = req["openid"];

            Stream st = Content.Request.InputStream;
            StreamReader sr = new StreamReader(st);
            string requestXML =  sr.ReadToEnd();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(requestXML);
            XmlNode root = doc.FirstChild;

            wx.Content = new RequestContent
            {
                ToUserName = root["ToUserName"].InnerText,
                FromUserName = root["FromUserName"].InnerText,
                CreateTime = root["CreateTime"].InnerText,
                MsgType = root["MsgType"].InnerText,
                Content = root["Content"].InnerText,
                MsgId = root["MsgId"].InnerText

            };
            return wx;
        }

        public void sendToWX(HttpContext context,string Msg,requestFromWX wx)
        {
            string sRespData = "<xml><ToUserName><![CDATA[" + wx.Content.FromUserName + "]]></ToUserName><FromUserName><![CDATA[" + wx.Content.ToUserName + "]]></FromUserName><CreateTime>" + Utils.GetTimeStamp() + "</CreateTime><MsgType><![CDATA[text]]></MsgType><Content><![CDATA["+Msg+"]]></Content><MsgId>"+wx.Content.MsgId+"</MsgId></xml>";
            context.Response.Clear();
            context.Response.ContentType = "application/xml";
            context.Response.Write(sRespData);

        }

        public class requestFromWX
        {
            public string signature { get; set; }
            public string timestamp { get; set; }
            public string nonce { get; set; }
            public string openid { get; set; }
            public RequestContent Content {get;set;}
        }

        public class RequestContent
        {
            public string ToUserName { get; set; }
            public string FromUserName { get; set; }
            public string CreateTime { get; set; }
            public string MsgType { get; set; }
            public string Content { get; set; }
            public string MsgId { get; set; }
        }

        public string GetWeatherInfoByCity(string city)
        {
            results today = new results();
            String Msg = "";
            //尝试从缓存中读取
            if (QueryWeatherFromCache(city, out today))
            {
                Msg = string.Format("您好，现在{0}的天气为{1}，温度为{2}℃", today.location.name, today.now.text, today.now.temperature);
            }
            else
            {
                try
                {
                    //加签名
                    HMACSHA1 hmacsha1 = new HMACSHA1();
                    hmacsha1.Key = Encoding.UTF8.GetBytes(Utils.GetConfig("WeatherKey"));
                    string querystring = "ts=" + Utils.GetTimeStamp() + "&ttl=30&uid=" + Utils.GetConfig("WeatherUID");
                    byte[] dataBuffer = Encoding.UTF8.GetBytes(querystring);
                    byte[] hashBytes = hmacsha1.ComputeHash(dataBuffer);
                    string sig = Convert.ToBase64String(hashBytes);

                    //声明一个HttpWebRequest请求  
                    string urlAPI = "https://api.thinkpage.cn/v3/weather/now.json?location=" + city + "&" + querystring + "&sig=" + sig;
                    var requestAPI = (HttpWebRequest)WebRequest.Create(urlAPI);
                    requestAPI.Method = "GET";
                    requestAPI.ContentType = "text/html;charset=UTF-8";
                    requestAPI.Timeout = 90000;
                    requestAPI.Headers.Set("Pragma", "no-cache");

                    //获取weather的response，格式化对象
                    var response = (HttpWebResponse)requestAPI.GetResponse();
                    var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    today = js.Deserialize<weather>(responseString).results[0];


                    ////回复content内容
                    if (today == null)
                    {
                        Msg = "请您使用正确的中文城市名称哦，暂时不支持外国城市~";
                    }
                    else
                    {
                        //存入缓存中
                        CacheHelper.SetCache("Cache_Weather_" + today.location.name, today, new TimeSpan(1, 0, 0));
                        Msg = string.Format("您好，现在{0}的天气为{1}，温度为{2}℃", today.location.name, today.now.text, today.now.temperature);
                    }

                }
                catch (Exception)
                {

                    Msg = "查询次数过多，请稍后再试！";
                }
            }
            return Msg;
        }

        public bool QueryWeatherFromCache(string city, out results today)
        {
            string prefix = "Cache_Weather_";
            if (CacheHelper.GetCache(prefix + city) != null)
            {
                today = (results)CacheHelper.GetCache(prefix + city);
                return true;
            }
            today = null;
            return false;
        }
    }
}