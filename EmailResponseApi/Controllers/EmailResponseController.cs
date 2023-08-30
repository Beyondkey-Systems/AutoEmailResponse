using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace EmailResponseApi.Controllers
{
    public class CustomResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class EmailResponseController : ControllerBase
    {
        [HttpGet("GenerateResponse")]
        public async Task<CustomResponse> GenerateResponse(string inputText)
        {
            try
            {
                // Remove additional white spaces from the input text
                inputText = Regex.Replace(inputText, @"\s+", " ").Trim();
                var apiKey = "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";
                var endpoint = "https://api.openai.com/v1/chat/completions";

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                    var customInstruction = "You are an assistant that should understand user query and should provides information as responder from Beyond key Systems (https://www.beyondkey.com) in 3 to 5 lines and should use information like their services, technologies, solutions, career opportunities, about company, insights, locations etc. for better result should visit Beyond Key website at https://www.beyondkey.com.";


                    var jsonBody = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[]
                        {
                            new { role = "system", content = customInstruction },
                            new { role = "user", content = inputText }
                        }
                    };

                    var jsonBodyString = JsonConvert.SerializeObject(jsonBody);

                    request.Content = new StringContent(jsonBodyString, Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error calling OpenAI API: {response.StatusCode} - {responseContent}");
                    }
                    var finalresponse = Regex.Replace(responseContent, @"(As an AI assistant,|As an AI language model,)", "", RegexOptions.IgnoreCase).Trim();
                    var customResponse = new CustomResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = finalresponse
                    };

                    return customResponse;
                }
            }
            catch (Exception ex)
            {
                // Handle the exception appropriately (e.g., log, return error response, etc.)
                return new CustomResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = "An error occurred while processing the request."
                };
            }
        }



    }
}
