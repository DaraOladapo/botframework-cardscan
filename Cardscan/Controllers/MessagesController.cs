using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Text;
using System.Web;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cardscan
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            //Activity reply = activity.CreateReply($"i habe been called");
            //await connector.Conversations.ReplyToActivityAsync(reply);

            if (activity.Attachments.Count > 0)
            {
                var attachment = activity.Attachments[0];
                var x = activity.CreateReply($"{attachment.ContentUrl}");
                await connector.Conversations.ReplyToActivityAsync(x);

                // query cognitive services vision
                var client = new HttpClient();
                var queryString = HttpUtility.ParseQueryString(string.Empty);

                // Request headers
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "");

                // Request parameters
                queryString["language"] = "unk";
                queryString["detectOrientation"] = "true";
                var uri = "https://api.projectoxford.ai/vision/v1.0/ocr?" + queryString;

                // Request body
                //https://scontent.xx.fbcdn.net/v/t35.0-12/16129708_10154245705797083_1623964492_o.jpg?_nc_ad=z-m&oh=924386164bd9edb18622e9b0f80e5ac8&oe=587EED1D
                var byteData = Encoding.UTF8.GetBytes($"{{\"url\":\"{attachment.ContentUrl}\"}}");
                //var byteData = Encoding.UTF8.GetBytes($"{{\"url\":\"https://scontent.xx.fbcdn.net/v/t35.0-12/16129708_10154245705797083_1623964492_o.jpg?_nc_ad=z-m&oh=924386164bd9edb18622e9b0f80e5ac8&oe=587EED1D\"}}");

                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    var visionResponse = await client.PostAsync(uri, content);
                    var visionResponseContent = visionResponse.Content.ReadAsStringAsync().Result;

                    // jpath stuff
                    var lines = ExtractRecognisedText(visionResponseContent);


                    //

                    var reply = activity.CreateReply($"Ok, I think the email is {lines.Email}");
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }
                
                //reply = activity.CreateReply($"{attachment.ContentUrl}");
                //await connector.Conversations.ReplyToActivityAsync(reply);
            }

            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private static RecognisedInformation ExtractRecognisedText(string visionResponseContent)
        {
            var result = new RecognisedInformation() { Lines = new List<string>() };
            
            var visionResponseContentParsed = JObject.Parse(visionResponseContent);
            var regions = visionResponseContentParsed.SelectToken("$..regions");
            foreach (var region in regions)
            {
                var currentLine = new StringBuilder();
                var lines = region.SelectToken("$..lines");
                foreach (var line in lines)
                {
                    var words = line.SelectToken("$..words");
                    foreach (var word in words)
                    {
                        var text = word.SelectToken("$..text").Value<string>();

                        // pattern matching is rudimentary for POC
                        // for a production app this would need to be more robust
                        if (text.Contains("@"))
                        {
                            result.Email = text;
                        }

                        currentLine.Append(word + " ");
                    }
                }
                result.Lines.Add(currentLine.ToString());
            }

            return result;
        }
    }

    public class RecognisedInformation
    {
        public List<string> Lines { get; set; }
        public string Email { get; set; }
    }
}