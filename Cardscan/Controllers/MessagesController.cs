using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Cardscan
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            var connector = new ConnectorClient(new Uri(activity.ServiceUrl));

            if (activity.Attachments.Count > 0)
            {
                var attachment = activity.Attachments[0];

                var byteData = await DownloadAttachentFromSkype(connector, attachment);
                var computerVisionResult = await ExecuteComputerVision(activity, connector, byteData);

                var reply = activity.CreateReply($"Ok, I think the email is {computerVisionResult.Email}");

                await connector.Conversations.ReplyToActivityAsync(reply);
            }
            
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private static async Task<RecognisedInformation> ExecuteComputerVision(Activity activity, ConnectorClient connector, byte[] byteData)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            var cognitiveServicesVisionKey = System.Configuration.ConfigurationManager.AppSettings["CognitiveServicesVisionKey"]; ;
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", cognitiveServicesVisionKey);
            queryString["language"] = "unk";
            queryString["detectOrientation"] = "true";
            var uri = "https://api.projectoxford.ai/vision/v1.0/ocr?" + queryString;
            var content = new ByteArrayContent(byteData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var visionResponse = await client.PostAsync(uri, content);
            var visionResponseContent = visionResponse.Content.ReadAsStringAsync().Result;
            return ExtractRecognisedText(visionResponseContent);
        }

        private static async Task<byte[]> DownloadAttachentFromSkype(ConnectorClient connector, Attachment attachment)
        {
            using (var httpClient = new HttpClient())
            {
                var token = await (connector.Credentials as MicrosoftAppCredentials).GetTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                return await httpClient.GetByteArrayAsync(attachment.ContentUrl);
            }
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