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
using System.Net.Mail;
using BusinessLayer;
using Microsoft.Extensions.Hosting;
using System;
using Microsoft.AspNetCore.DataProtection.KeyManagement;


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
        private readonly IWebHostEnvironment _environment;

        public EmailResponseController(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }


        [HttpGet("GenerateResponse")]
        public async Task<CustomResponse> GenerateResponse(string inputText, string WebsiteURL, string FullName, string Email)
        {
            try
            {
                var apiKey = _configuration["apiKey"];

                inputText = "Name: " + FullName + "|" + Regex.Replace(inputText, @"\s+", " ").Trim();
                string formattedText = $"Text: \"\"\"\n{inputText}\n\"\"\"";


                var endpoint = "https://api.openai.com/v1/chat/completions";

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    List<string> CaseStudyFiles = new List<string>();
                    var customInstruction = string.Empty;
                    if (WebsiteURL.Trim().Contains("beyondintranet"))
                    {
                        string Domain = EmailResponseHandler.GetDomainFromEmail(Email);
                        var Domainkeywords = await EmailResponseHandler.ExtractKeywordsfromDomain(apiKey, Domain);
                        var UserQueryKeywords = await EmailResponseHandler.ExtractKeywordsfromUserQuery(apiKey, inputText);
                        Domainkeywords = Domainkeywords.Select(keyword => keyword.Trim()).ToList();
                        UserQueryKeywords = UserQueryKeywords.Select(keyword => keyword.Trim()).ToList();
                        var FinalKeywords = UserQueryKeywords.Concat(Domainkeywords).ToList();

                        CaseStudyFiles = GetCaseStudy(FinalKeywords);
                        customInstruction = _configuration["CustomInstructionBeyondIntranet1"] + " ";
                        customInstruction += "For 'Product' inquiries, i can be asked about specific products like 'HR Directory,' 'Organizational Chart,' etc or similar kind of products., and I'll provide relevant links as specified here. ";
                        var products = _configuration.GetSection("BeyondIntranetProducts");
                        foreach (var product in products.GetChildren())
                        {
                            customInstruction += $"You can learn more about '{product.Key}' here: {product.Value}|";
                        }
                        customInstruction += _configuration["CustomInstructionBeyondIntranet2"];
                        if (CaseStudyFiles.Count == 1)
                            customInstruction += $"Should always force to take a moment to review relevant case study.";
                        if (CaseStudyFiles.Count > 1)
                            customInstruction += $"Should always suggest to take a moment to review our case studies. ";
                        customInstruction += _configuration["CustomInstructionBeyondIntranet3"];
                        customInstruction += _configuration["SampleResponse1"];
                        customInstruction += _configuration["SampleResponse2"];
                    }
                    else
                        customInstruction = _configuration["CustomInstructionBeyondkey"];

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

                    var jsonResponse = JObject.Parse(responseContent);
                    // Update the inner "content" field
                    var choicesArray = jsonResponse["choices"] as JArray;

                    if (choicesArray != null && choicesArray.Count > 0)
                    {
                        // Access the first item in "choices" and update its "content" field
                        var firstChoice = choicesArray[0] as JObject;
                        if (firstChoice != null)
                        {
                            var content = firstChoice["message"]["content"].ToString();
                            if (WebsiteURL.Trim().Contains("beyondintranet"))
                            {
                                if (CaseStudyFiles.Count == 1)
                                    content = Regex.Replace(content, @"(<case study>|case study|casestudy)", $"<a href='{CaseStudyFiles[0]}'>$1</a>", RegexOptions.IgnoreCase);
                                
                                content = Regex.Replace(content, @"(case studies|casestudies|case-studies)", "<a href='https://www.beyondintranet.com/customer-stories'>$1</a>", RegexOptions.IgnoreCase);
                            }
                            content += _configuration["DisplayPoweredByBKChatbot"] == "True"? $" <br/><br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">[Powered by Beyond Key Chatbot]</span>": string.Empty;
                            content += _configuration["DisplayCautionText"] == "True" ? $" <br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">{_configuration["CautionText"]}</span>" : string.Empty;
                            content = RemoveIgnoredKeywords(content);
                           
                            firstChoice["message"]["content"] =content;
                            firstChoice["message"]["content"] = firstChoice["message"]["content"].ToString().Replace("\n", "<br/>");

                        }
                    }

                    // Serialize the updated JSON back to a string
                    responseContent = jsonResponse.ToString();

                    var customResponse = new CustomResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = responseContent
                    };
                    //SendEmail(customResponse);
                    return customResponse;
                }
            }
            catch (Exception ex)
            {
                BusinessLayer.ErrorHandler.SendErrorEmail(_configuration, ex, WebsiteURL, inputText);
                return new CustomResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = "An error occurred while processing the request."
                };
            }
        }
        private string RemoveIgnoredKeywords(string Content)
        {
            string[] ignoreKeywords = _configuration.GetSection("IgnoreKeywords").Get<string[]>();
            string pattern = "(" + string.Join("|", ignoreKeywords.Select(kw => Regex.Escape(kw))) + ")";

            return Regex.Replace(Content, pattern, "", RegexOptions.IgnoreCase);
        }
        private List<string> GetCaseStudy(List<string> Keywords)
        {
            EmailResponseHandler emailResponseHandler = new EmailResponseHandler();
            string relativeFilePath = "DB/CaseStudy.xml";
            // Get the content root path of your application
            string contentRootPath = _environment.ContentRootPath;

            // Combine the content root path with the relative file path
            string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);


            string xmlContent = System.IO.File.ReadAllText(physicalFilePath);
            var Url = emailResponseHandler.SearchKeywordsInCaseStudyXML(Keywords, xmlContent);
            return Url;
        }

        private void SendEmail(CustomResponse customResponse)
        {
            try
            {
                // Deserialize the JSON response into a JObject
                JObject responseObject = JObject.Parse(customResponse.Content);

                // Access the "choices" array from the JObject
                JArray choicesArray = responseObject["choices"] as JArray;

                if (choicesArray != null && choicesArray.Any())
                {
                    // Access the first item in the "choices" array (index 0)
                    JObject firstChoice = choicesArray[0] as JObject;

                    if (firstChoice != null)
                    {
                        // Access the "content" field from the first choice
                        string content = firstChoice["message"]["content"].ToString();

                        // Gmail SMTP settings
                        var smtpClient = new SmtpClient("smtp.gmail.com")
                        {
                            Port = 587,
                            Credentials = new NetworkCredential(_configuration["FromEmail"], _configuration["EmailPassword"]),
                            EnableSsl = true,
                            UseDefaultCredentials = false,
                        };

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(_configuration["FromEmail"]),
                            Subject = "internal testing - BeyondIntranet Contact us Auto Email Response",
                            Body = content,
                            IsBodyHtml = true
                        };

                        mailMessage.To.Add(_configuration["ReceiverEmail"]);

                        smtpClient.Send(mailMessage);
                    }
                }

            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during email sending, e.g., log them
                Console.WriteLine("Error sending email: " + ex.Message);
            }
        }
    }
}
