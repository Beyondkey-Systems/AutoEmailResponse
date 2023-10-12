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
        public bool IsCareerRelated { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class EmailResponseController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private List<string> CaseStudyFiles;
        private bool IsBeyondIntranet;

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
                IsBeyondIntranet = WebsiteURL.Trim().Contains("beyondintranet") ? true : false;
                var apiKey = _configuration["apiKey"];


                inputText = "Name: " + FullName + "|" + Regex.Replace(inputText, @"\s+", " ").Trim();
                string formattedText = $"Text: \"\"\"\n{inputText}\n\"\"\"";

                var endpoint = "https://api.openai.com/v1/chat/completions";
                //functionality for isCareerRelated is common for both Beyondkey and Beyondintranet
                bool isCareerRelated = await EmailResponseHandler.IsCareerRelated(apiKey, inputText);
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                    var customInstruction = string.Empty;
                    if (IsBeyondIntranet)
                    {
                        string contentRootPath = _environment.ContentRootPath;
                        string relativeFilePath = "DB/Keyword.xml";

                        // Combine the content root path with the relative file path
                        string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);
                        XDocument xmlDocument = XDocument.Load(physicalFilePath);
                        var MatchedKeywords = await EmailResponseHandler.ExtractMatchedKeywords(xmlDocument, apiKey, inputText);
                        bool IsCaseStudyToShow = EmailResponseHandler.IsCaseStudyToShow(MatchedKeywords, xmlDocument);
                        bool IsWebSiteUrlToShow = EmailResponseHandler.IsWebSiteUrlToShow(MatchedKeywords, xmlDocument);
                        if (IsCaseStudyToShow == false && IsWebSiteUrlToShow == false)
                        {
                            var defaultResponse = GetDefaultResponse(FullName, isCareerRelated, IsBeyondIntranet);
                           // SendEmail(defaultResponse);
                            return defaultResponse;
                        }

                        customInstruction = _configuration["CustomInstructionBeyondIntranet1"] + " ";
                        customInstruction += "For 'Product' inquiries, i can be asked about specific products like 'HR Directory,' 'Organizational Chart,' etc or similar kind of products., and I'll provide relevant links as specified here. ";
                        var products = _configuration.GetSection("BeyondIntranetProducts");
                        foreach (var product in products.GetChildren())
                        {
                            customInstruction += $"You can learn more about '{product.Key}' here: {product.Value}|";
                        }
                        customInstruction += _configuration["CustomInstructionBeyondIntranet2"];

                        string Domain = EmailResponseHandler.GetDomainFromEmail(Email);
                        EmailResponseHandler emailResponseHandler = new EmailResponseHandler();
                        CaseStudyFiles = await emailResponseHandler.FindCaseStudy(contentRootPath, apiKey, Domain, inputText);

                        if (IsCaseStudyToShow && CaseStudyFiles.Count == 1)
                            customInstruction += $"Should always force to take a moment to review relevant case study.";
                        if (IsCaseStudyToShow && CaseStudyFiles.Count > 1)
                            customInstruction += $"Should always suggest to take a moment to review our case studies. ";
                        customInstruction += _configuration["CustomInstructionBeyondIntranet3"];

                        customInstruction += _configuration["SampleResponse1"];
                        if (IsWebSiteUrlToShow)
                            customInstruction += " In the meantime, please visit our <a href='https://www.beyondintranet.com/'>website</a> to learn more about our HR products.";
                        if (IsCaseStudyToShow && CaseStudyFiles.Count == 1)
                            customInstruction += " Also please take a moment to review our comprehensive case study.";
                        if (IsCaseStudyToShow && CaseStudyFiles.Count > 1)
                            customInstruction += " Also please take a moment to review our case studies.";
                        customInstruction += "<br/><br/>Best Regards,<br/>Beyond Intranet";

                        customInstruction += _configuration["SampleResponse2"];
                        if (IsWebSiteUrlToShow)
                            customInstruction += " In the meantime, please visit our <a href='https://www.beyondintranet.com/'>website</a> to learn more about our services.";
                        if (IsCaseStudyToShow && CaseStudyFiles.Count == 1)
                            customInstruction += " Also please take a moment to review our comprehensive case study.";
                        if (IsCaseStudyToShow && CaseStudyFiles.Count > 1)
                            customInstruction += " Also please take a moment to review our case studies.";
                        customInstruction += "<br/><br/>Best Regards,<br/>Beyond Intranet";
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
                            if (IsBeyondIntranet)
                            {
                                if (CaseStudyFiles.Count == 1)
                                    content = Regex.Replace(content, @"(<case study>|case study|casestudy)", $"<a href='{CaseStudyFiles[0]}'>$1</a>", RegexOptions.IgnoreCase);

                                content = Regex.Replace(content, @"(case studies|casestudies|case-studies)", "<a href='https://www.beyondintranet.com/customer-stories'>$1</a>", RegexOptions.IgnoreCase);
                            }
                            content += _configuration["DisplayPoweredByBKChatbot"] == "True" ? $" <br/><br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">[Powered by Beyond Key Chatbot]</span>" : string.Empty;
                            content += _configuration["DisplayCautionText"] == "True" ? $" <br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">{_configuration["CautionText"]}</span>" : string.Empty;
                            content = RemoveIgnoredKeywords(content);

                            firstChoice["message"]["content"] = content;
                            firstChoice["message"]["content"] = firstChoice["message"]["content"].ToString().Replace("\n", "<br/>");

                        }
                    }

                    // Serialize the updated JSON back to a string
                    responseContent = jsonResponse.ToString();

                    var customResponse = new CustomResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = responseContent,
                        IsCareerRelated = isCareerRelated
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
                    Content = "An error occurred while processing the request.",
                    IsCareerRelated = false
                };
            }
        }
        private CustomResponse GetDefaultResponse(string FullName, bool IsCareerRelated,bool isBeyondIntranet)
        {
            string DefaultResponse = $"Hello {FullName},<br/><br/>Thank you for reaching out to us. We appreciate your interest.";
            DefaultResponse += "<br/><br/>Your query is important to us, and we want to ensure we provide you with the best possible information and assistance. Our dedicated team is currently reviewing your request, and you can expect to hear back from us shortly.";
            DefaultResponse += isBeyondIntranet ? "<br/><br/>Best Regards,<br/>Beyond Intranet": "<br/><br/>Best Regards,<br/>Beyondkey Systems";
            DefaultResponse += _configuration["DisplayPoweredByBKChatbot"] == "True" ? $" <br/><br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">[Powered by Beyond Key Chatbot]</span>" : string.Empty;
            DefaultResponse += _configuration["DisplayCautionText"] == "True" ? $" <br/><span style=\"font-size: 10px; font-family: 'Helvetica Neue';\">{_configuration["CautionText"]}</span>" : string.Empty;

            // Create the JSON response structure
            var jsonResponse = new
            {
                id = "chatcmpl-88Vs3lMmNLG6cEZ3v4ntKFqUOiz3H",
                @object = "chat.completion",
                created = 1697039827, // You can set this timestamp to the desired value
                model = "gpt-3.5-turbo-0613",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new
                        {
                            role = "assistant",
                            content = DefaultResponse
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 100,
                    completion_tokens = 150,
                    total_tokens = 250
                }
            };

            // Serialize the JSON response to a string
            var jsonResponseString = JsonConvert.SerializeObject(jsonResponse);

            // Create a CustomResponse object and assign the JSON response as its content
            var customResponse = new CustomResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = jsonResponseString, // Assign the JSON response string
                IsCareerRelated = IsCareerRelated
            };

            return customResponse;
        }


        private string RemoveIgnoredKeywords(string Content)
        {
            string[] ignoreKeywords = _configuration.GetSection("IgnoreKeywords").Get<string[]>();
            string pattern = "(" + string.Join("|", ignoreKeywords.Select(kw => Regex.Escape(kw))) + ")";

            return Regex.Replace(Content, pattern, "", RegexOptions.IgnoreCase);
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
