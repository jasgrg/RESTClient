using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace RESTClient
{
    class FormData
    {
        public String data { get; set; }
    }

    public struct HTTPResponse<TResponse>
    {
        public HttpStatusCode StatusCode;
        public TResponse Response;
        public WebHeaderCollection Headers;

    }

    public class RESTClientBase
    {
        public string ResourceURI { get; set; }


        protected virtual int GetTimeout()
        {
            return 600000;
        }

        private HTTPResponse<TResponse> Invoke<TRequest, TResponse>(string uri, string verb, TRequest data) where TResponse : class
        {
            if (uri.Equals(String.Empty))
            {
                throw new Exception("ResourceURI must be set before invoking a REST call.");
            }

            var response = new HTTPResponse<TResponse>();

            var webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.Method = verb;
            webRequest.ContentLength = 0;
            webRequest.Timeout = GetTimeout();


            SerializeData<TRequest>(data, webRequest);


            try
            {
                using (var webResponse = webRequest.GetResponse() as HttpWebResponse)
                {
                    response.StatusCode = webResponse.StatusCode;
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        using (var responseStream = webResponse.GetResponseStream())
                        {
                            using (var streamReader = new StreamReader(responseStream))
                            {
                                var streamString = streamReader.ReadToEnd();
                                object responseObject = DeserializeResponse<TResponse>(streamString);

                                response.Response = (TResponse)responseObject;
                                response.Headers = webResponse.Headers;
                            }
                        }
                    }
                }
            }
            catch (WebException webException)
            {
                if (webException.Status == WebExceptionStatus.ProtocolError)
                {

                    if (webException.Response is HttpWebResponse weResponse)
                    {
                        throw webException;

                        // HTTP status codes should be handled gracefully by the derived class.
                        //return new HTTPResponse<TResponse>() { StatusCode = response.StatusCode };
                    }
                }
                throw;
            }

            return response;
        }

        private static void SerializeData<TRequest>(TRequest data, HttpWebRequest webRequest)
        {

            if (data == null) { return; }

            var bytes = (typeof(TRequest) == typeof(XmlDocument)) ?
                SerializeXmlData<TRequest>(data, webRequest) :
                    (typeof(TRequest) == typeof(FormData)) ?
                    SerializeFormData<FormData>(data as FormData, webRequest) :
                    SerializeJsonData<TRequest>(data, webRequest);

            webRequest.ContentLength = bytes.Length;
            using (var requestStream = webRequest.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }

        }

        private static object DeserializeResponse<TResponse>(string streamString)
        {
            object responseObject = null;
            var responseType = typeof(TResponse);
            if (responseType == typeof(XmlDocument))
            {
                XmlDocument xmlObject = new XmlDocument();
                xmlObject.LoadXml(streamString);
                responseObject = xmlObject;
            }
            else
            {
                if (responseType == typeof(String))
                {
                    responseObject = streamString;
                }
                else
                {
                    responseObject = JsonConvert.DeserializeObject<TResponse>(streamString);
                }
            }
            return responseObject;
        }

        private static byte[] SerializeJsonData<TRequest>(TRequest data, HttpWebRequest webRequest)
        {
            webRequest.ContentType = @"application/json; charset=utf-8";

            var serializer = new JsonSerializer();
            var bytes = (byte[])null;
            using (var stream = new MemoryStream())
            {
                using (var swriter = new StreamWriter(stream, new UTF8Encoding(false)))
                using (var jwriter = new JsonTextWriter(swriter))
                {
                    serializer.Serialize(jwriter, data);
                    jwriter.Flush();

                    bytes = stream.ToArray();
                }
            }

            return bytes;
        }

        private static byte[] SerializeFormData<TRequest>(FormData data, HttpWebRequest webRequest)
        {
            webRequest.ContentType = "application/x-www-form-urlencoded";
            return Encoding.UTF8.GetBytes(data.data);

        }

        private static byte[] SerializeXmlData<TRequest>(TRequest data, HttpWebRequest webRequest)
        {
            webRequest.ContentType = @"text/xml";

            var serializer = new XmlSerializer(typeof(TRequest));
            var bytes = (byte[])null;
            using (var stream = new MemoryStream())
            {
                using (var swriter = new StreamWriter(stream, new UTF8Encoding(false)))
                using (var xwriter = new XmlTextWriter(swriter))
                {
                    serializer.Serialize(xwriter, data);
                    xwriter.Flush();

                    bytes = stream.ToArray();
                }

            }

            return bytes;
        }

        public HTTPResponse<TResponse> Get<TResponse>() where TResponse : class
        {
            return Invoke<string, TResponse>(ResourceURI, "GET", null);
        }

        public void Put<TRequest>(TRequest data)
        {
            Invoke<TRequest, string>(ResourceURI, "PUT", data);
        }

        public HTTPResponse<TResponse> Put<TRequest, TResponse>(TRequest data) where TResponse : class
        {
            return Invoke<TRequest, TResponse>(ResourceURI, "PUT", data);
        }

        public HTTPResponse<TResponse> Delete<TRequest, TResponse>(TRequest data) where TResponse : class
        {
            return Invoke<TRequest, TResponse>(ResourceURI, "DELETE", data);
        }

        public void Post<TRequest>(TRequest data)
        {
            Invoke<TRequest, string>(ResourceURI, "POST", data);
        }

        public HTTPResponse<TReponse> Post<TRequest, TReponse>(TRequest data) where TReponse : class
        {
            return Invoke<TRequest, TReponse>(ResourceURI, "POST", data);
        }

        public HTTPResponse<TResponse> PostAsForm<TRequest, TResponse>(TRequest data) where TResponse : class
        {
            var json = JsonConvert.SerializeObject(data);
            Dictionary<string, string> attributes =
                            JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            var list = new List<string>();
            foreach (var key in attributes.Keys)
            {
                var val = attributes[key];
                list.Add(key + "=" + System.Uri.EscapeDataString(val));
            }

            var form = new FormData()
            {
                data = String.Join("&", list)
            };
            return Invoke<FormData, TResponse>(ResourceURI, "POST", form);
        }

        public HTTPResponse<object> Head()
        {
            return Invoke<string, object>(ResourceURI, "HEAD", null);
        }
    }
}
