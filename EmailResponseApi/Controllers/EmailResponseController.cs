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
using Microsoft.Extensions.Configuration;

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
        private readonly IConfiguration _configuration;
        public EmailResponseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet("GenerateResponse")]
        public async Task<CustomResponse> GenerateResponse(string inputText)
        {
            try
            {
                // Remove additional white spaces from the input text
                inputText = Regex.Replace(inputText, @"\s+", " ").Trim();
                string formattedText = $"Text: \"\"\"\n{inputText}\n\"\"\"";

                var apiKey = _configuration["apiKey"];
                var endpoint = "https://api.openai.com/v1/chat/completions";

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                    //var customInstruction = "You are an assistant that should understand user query and should provides information as responder from Beyond key Systems (https://www.beyondkey.com) in 3 to 5 lines and should use information like their services, technologies, solutions, career opportunities, about company, insights, locations etc. for better result should visit Beyond Key website at https://www.beyondkey.com.";

                    var customInstruction = _configuration["CustomInstruction"];

                    var jsonBody = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[]
                        {
                            new { role = "system", content = customInstruction },
                            new { role = "user", content = formattedText }
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
                    string pattern = @"[^,]*\b(AI|chatbot)\b[^,]*,";

                    string finalResponse = Regex.Replace(responseContent, pattern, "", RegexOptions.IgnoreCase);

                    var customResponse = new CustomResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = finalResponse
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
