using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Configuration;
using System.IO.Compression;
using System.Web;

namespace SharePointCookie
{
    class Program
    {


        /// <summary>
        /// Invoke using the following switches -username "username" -password "password" -endpoint = "endpoint"
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var username = ConfigurationManager.AppSettings.Get("SharePointOnlineUserName");
            var password = ConfigurationManager.AppSettings.Get("SharePointOnlinePassword");
            var endPoint = ConfigurationManager.AppSettings.Get("SharePointOnlineEndpoint");
            var stsEndpoint = ConfigurationManager.AppSettings.Get("SharePointOnlineSTS");
            var signInUrl = ConfigurationManager.AppSettings.Get("SharePointOnlineSignInUri");
            var browserHost = ConfigurationManager.AppSettings.Get("BrowserHost");
            var browserUserAgent = ConfigurationManager.AppSettings.Get("BrowserUserAgent");

            var cookies = new CookieContainer();
            var executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var payLoadSts = System.IO.File.ReadAllText(Path.Combine(executingPath, "extStsTemplate.xml")).Replace("{USERNAME}", username).Replace("{PASSWORD}", password).Replace("{ENDPOINTREFERENCE}", endPoint);

            // webrequest 0
            // webrequest 0: get the RpsContextCookie
            // webrequest 0
            string requiredAuthUrl = string.Format("{0}/_layouts/15/Authenticate.aspx?Source={1}", endPoint, HttpUtility.UrlDecode(endPoint));
            HttpWebRequest request0 = (HttpWebRequest)WebRequest.Create(requiredAuthUrl);
            request0.Accept = "*/*";
            request0.Headers.Set(HttpRequestHeader.AcceptLanguage, "en-US");
            request0.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            request0.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; Trident/5.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; .NET4.0C; .NET4.0E; InfoPath.3)";
            request0.AllowAutoRedirect = false;
            request0.CookieContainer = cookies;

            var response0 = (HttpWebResponse)request0.GetResponse();

            // webrequest 1
            // webrequest 1: login
            // webrequest 1
            var request1 = (HttpWebRequest)WebRequest.Create(stsEndpoint);
            request1.CookieContainer = cookies;
            var data = Encoding.ASCII.GetBytes(payLoadSts);

            request1.Method = "POST";
            request1.ContentType = "application/xml";
            request1.ContentLength = data.Length;

            using (var stream1 = request1.GetRequestStream())
            {
                stream1.Write(data, 0, data.Length);
            }

            // Response 1
            var response1 = (HttpWebResponse)request1.GetResponse();
            var responseString = new StreamReader(response1.GetResponseStream()).ReadToEnd();

            // Get BinarySecurityToken
            var xData = XDocument.Parse(responseString);
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("S", "http://www.w3.org/2003/05/soap-envelope");
            namespaceManager.AddNamespace("wst", "http://schemas.xmlsoap.org/ws/2005/02/trust");
            namespaceManager.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            var BinarySecurityToken = xData.XPathSelectElement("/S:Envelope/S:Body/wst:RequestSecurityTokenResponse/wst:RequestedSecurityToken/wsse:BinarySecurityToken", namespaceManager);

            // webrequest 2
            // webrequest 2 //_forms/default.aspx?wa=wsignin1.0
            // webrequest 2
            var request2 = (HttpWebRequest)WebRequest.Create(signInUrl);
            
            var data2 = Encoding.ASCII.GetBytes(BinarySecurityToken.Value);
            request2.Method = "POST";
            request2.Accept = "*/*";
            request2.Headers.Set(HttpRequestHeader.AcceptLanguage, "en-US");
            request2.ContentType = "application/x-www-form-urlencoded";
            request2.UserAgent = browserUserAgent;
            request2.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            request2.Host = browserHost;
            request2.ContentLength = data2.Length;
            request2.CookieContainer = cookies;

            using (var stream2 = request2.GetRequestStream())
            {
                stream2.Write(data2, 0, data2.Length);
            }

            // Response 2
            var response2 = (HttpWebResponse)request2.GetResponse();
            var responseString2 = new StreamReader(response2.GetResponseStream()).ReadToEnd();


            // webrequest 3
            // webrequest 3: get X-RequestDigest 
            // webrequest 3
            string restUrl3 = string.Format("{0}/_api/contextinfo", endPoint);
            var request3 = (HttpWebRequest)WebRequest.Create(restUrl3);
            request3.CookieContainer = cookies;
            request3.Method = "POST";
            request3.ContentLength = 0;

            // Response 3
            string formDigest = string.Empty;
            using (var response3 = (HttpWebResponse)request3.GetResponse())
            {
                using (var reader = new StreamReader(response3.GetResponseStream()))
                {
                    var result = reader.ReadToEnd();

                    // parse the ContextInfo response
                    var resultXml = XDocument.Parse(result);

                    // get the form digest value
                    var x = from y in resultXml.Descendants()
                            where y.Name == XName.Get("FormDigestValue", "http://schemas.microsoft.com/ado/2007/08/dataservices")
                            select y;
                    formDigest = x.First().Value;
                }
            }


            // webrequest 4
            // webrequest 4: execute rest call
            // webrequest 4
            string restUrl4 = string.Format("{0}/_api/web/webinfos/add", endPoint);
            var request4 = (HttpWebRequest)WebRequest.Create(restUrl4);
            request4.CookieContainer = cookies;
            request4.Method = "POST";
            request4.Accept = "application/json; odata=verbose";
            request4.ContentType = "application/json;odata=verbose";
            request4.Headers.Add("X-RequestDigest", formDigest);

            
            string json4 = " {'parameters': { " +
            "'__metadata':  {'type': 'SP.WebInfoCreationInformation' }, " +
            "'Url': 'RestSubWeb', " +
            "'Title': 'RestSubWeb', " +
            "'Description': 'REST created web', " +
            "'Language':1033, " +
            "'WebTemplate':'sts', " +
            "'UseUniquePermissions':false} } ";

            var data4 = Encoding.ASCII.GetBytes(json4);
            request4.ContentLength = data4.Length;
             using (var stream4 = request4.GetRequestStream())
            {
                stream4.Write(data4, 0, data4.Length);
            }
            

            // Response 4
            var response4 = (HttpWebResponse)request4.GetResponse();
            var responseString4 = new StreamReader(response4.GetResponseStream()).ReadToEnd();

        
            Console.ReadKey();
     

        }

       
    }
}
