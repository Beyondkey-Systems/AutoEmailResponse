using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;


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
                        ""role"": ""system"",
                        ""content"": ""You are a helpful assistant that generates formal responses of 3 to 5 lines in proper formating and indenting.""
                    },
                    {
                        ""role"": ""user"",
                        ""content"": """ + inputText.Replace("\"", "\\\"") + @"""
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



                    var customResponse = new CustomResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = responseContent
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
