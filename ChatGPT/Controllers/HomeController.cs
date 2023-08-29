using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChatGPT.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient httpClient;

        public HomeController()
        {

        }

        //public async Task<string> GenerateResponse(string inputtext)
        public async Task<ActionResult> GenerateResponse(string inputtext)
        {
            try
            {
                var apiKey = "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";
                var endpoint = "https://api.openai.com/v1/chat/completions";

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    
                    var jsonBody = @"{
                        ""model"": ""gpt-3.5-turbo"",
                        ""messages"": [
                            {
                                ""role"": ""user"",
                                ""content"": """ + inputtext.Replace("\"", "\\\"") + @"""
                            }
                        ]
                    }";

                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error calling OpenAI API: {response.StatusCode} - {responseContent}");
                    }
                    return Json(responseContent);
                }
                
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        public async Task<ActionResult> Index()
        {
            try
            {
                //var jsonBody = @"{
                //        ""model"": ""gpt-3.5-turbo"",
                //        ""messages"": [
                //            {
                //                ""role"": ""user"",
                //                ""content"": ""Suggest email response for following  \n Today’s work report ""
                //            }
                //        ]
                //    }";

                //var response = await GetCompletions(jsonBody);

                //ViewBag.Result = response;
                return View();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during the API call
                ViewBag.Error = "An error occurred: " + ex.Message;
                return View();
            }

        }

        //public static async Task<string> GetCompletions(string jsonBody)
        //{
        //    var apiKey = "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";
        //    var endpoint = "https://api.openai.com/v1/chat/completions";

        //    using (var client = new HttpClient())
        //    {
        //        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        //        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        //        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        //        var response = await client.SendAsync(request);
        //        var responseContent = await response.Content.ReadAsStringAsync();

        //        if (!response.IsSuccessStatusCode)
        //        {
        //            throw new Exception($"Error calling OpenAI API: {response.StatusCode} - {responseContent}");
        //        }

        //        return responseContent;
        //    }
        //}

    }

}
