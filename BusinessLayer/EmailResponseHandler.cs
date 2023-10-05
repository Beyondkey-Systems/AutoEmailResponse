using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BusinessLayer
{
    public class EmailResponseHandler
    {
        public async Task<List<string>> FindCaseStudy(string contentRootPath,string apiKey, string Domain, string inputText)
        {
            EmailResponseHandler emailResponseHandler = new EmailResponseHandler();
            string relativeFilePath = "DB/CaseStudy.xml";

            // Combine the content root path with the relative file path
            string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);
            string xmlContent = System.IO.File.ReadAllText(physicalFilePath);
            
            var Domainkeywords = await EmailResponseHandler.ExtractKeywordsfromDomain(apiKey, Domain);
            Domainkeywords = Domainkeywords
            .SelectMany(keyword => keyword.Split(',').Select(trimmedKeyword => trimmedKeyword.Trim()))
            .ToList();

            List<string> UserQueryKeywords = await EmailResponseHandler.ExtractKeywordsfromUserQuery(apiKey, inputText);
            UserQueryKeywords = UserQueryKeywords
           .SelectMany(keyword => keyword.Split(',').Select(trimmedKeyword => trimmedKeyword.Trim()))
           .ToList();

            var FinalKeywords = UserQueryKeywords.Concat(Domainkeywords).ToList();

            /*********FIRST ATTEMPT (PASSING DOMAIN AND USER QUERY KEYWORD TO GET CASE STUDY***********/
            var Url = emailResponseHandler.SearchKeywordsInCaseStudyXML(FinalKeywords, xmlContent);
            if (Url.Count > 0) return Url;

            /*********SECOND ATTEMPT (PASSING USER QUERY KEYWORD TO GET CASE STUDY***********/
            Url = SearchKeywordsInCaseStudyXML(UserQueryKeywords, xmlContent);
            return Url;
        }

        private List<string> SearchKeywordsInCaseStudyXML(List<string> keywords, string xmlContent)
        {
            XDocument xdoc = XDocument.Parse(xmlContent);
            List<string> matchedUrls = new List<string>();

            foreach (var item in xdoc.Descendants("item"))
            {
                IEnumerable<string> tags = item.Descendants("ptags").Descendants("tagname").Select(tag => tag.Value);
                string combinedText = string.Join(" ", tags);

                bool allKeywordsMatched = keywords.All(keyword =>
                {
                    var matchesForKeyword = Regex.Matches(combinedText, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase);
                    return matchesForKeyword.Count > 0;
                });

                if (allKeywordsMatched)
                {
                    matchedUrls.Add(item.Element("url")?.Value);
                }
            }

            return matchedUrls;
        }


        public static async Task<List<string>> ExtractMatchedKeywords(string contentRootPath, string apiKey, string userQuery)
        {
            var UserQueryKeywords =await ExtractKeywordsfromUserQuery(apiKey, userQuery);

            // Build the prompt by combining user keywords
            string userKeywordsPrompt = string.Join(", ", UserQueryKeywords);

            // Build the instruction for OpenAI
            string customInstruction = $"###Available keywords###\r\n: {userKeywordsPrompt}";

            
            string relativeFilePath = "DB/Keyword.xml";

            // Combine the content root path with the relative file path
            string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);
            List<string> keywords = new List<string>();
            XDocument xmlDocument = XDocument.Load(physicalFilePath);

            // Iterate through XML elements and add keywords to the list
            foreach (XElement category in xmlDocument.Root.Elements())
            {
                foreach (XElement keywordElement in category.Elements("tagname"))
                {
                    string keyword = keywordElement.Value;
                    keywords.Add(keyword);
                }
            }
            // Create a prompt for OpenAI
            string prompt = $"{customInstruction}\n\nBased on Available keywords, get relevant exact keywords only in comma separated format, from below Keyword List, Do not share any additional information\r\nKeyword List:\n{string.Join(", ", keywords)} \n###Desired Output Format###\nKeyword1,Keyword2,Keyword3";

            // Initialize an HTTP client
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            try
            {
                var endpoint = "https://api.openai.com/v1/chat/completions";
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var jsonBody = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[]
                        {
                            new { role = "system", content = "You are a helpful assistant. Extract keywords only, Do not share any additional information"},
                            new { role = "user", content = prompt }
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
                            var extractedKeywords = firstChoice["message"]["content"].ToString();
                            // Split the response by lines and clean up each line
                            var keywordLines = extractedKeywords.Split(',')
                                .Select(line => line.Trim())
                                .Where(line => !string.IsNullOrEmpty(line))
                                .ToList();

                            // Filter the keywords from the lines
                            var matchingKeywords = keywordLines.Where(line => keywords.Contains(line, StringComparer.OrdinalIgnoreCase))
                                .ToList();

                            return matchingKeywords;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new List<string>(); // Return an empty list in case of an error
            }
            return new List<string>();
        }


        public static string GetDomainFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            // Split the email address by '@' character
            string[] parts = email.Split('@');

            // Check if the email has at least two parts
            if (parts.Length >= 2)
            {
                // The domain is in the second part
                return parts[1];
            }

            return null; // Invalid email format
        }
        public static string ReplaceUrlsNotInXml(string input, string contentRootPath)
        {
            string relativeFilePath = "DB/AllUrls.xml";
            string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);

            string xmlContent = File.ReadAllText(physicalFilePath);
            XElement xml = XElement.Parse(xmlContent);

            List<string> validUrls = xml.Descendants("url")
                                        .Select(e => e.Value)
                                        .ToList();

            string urlPattern = @"(https?://[^\s/$.?#].[^\s]*)";
            Regex regex = new Regex(urlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string[] words = input.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (regex.IsMatch(word) && !validUrls.Contains(word))
                {
                    words[i] = "https://www.beyondintranet.com/";
                }
            }

            return string.Join(" ", words);
        }

        public static async Task<List<string>> ExtractKeywordsfromDomain(string apiKey, string Domain)
        {

            // Initialize the OpenAI API client
            var openAiApi = new OpenAI_API.OpenAIAPI(apiKey);

            // Input text from which to extract keywords
            string customInstruction = $"You are a language model, understand the core business of domain: {Domain} and based on core business, extract only 1 or 2 important keywords. do not share any additional information";

            try
            {
                var response = await openAiApi.Completions.CreateCompletionAsync(new CompletionRequest()
                {
                    Model = "text-davinci-003",
                    Temperature = 0.1,
                    MaxTokens = 50,
                    Prompt = $"'{customInstruction}'\nKeywords:"
                }
                );


                // Extracted keywords from the response
                // Access the generated text (keywords) from the response
                var keywords = response.Completions[0].Text;
                var keywordLines = keywords.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));

                // Extract the keywords without the numbering
                var keywordList = keywordLines.Select(line =>
                {
                    // Remove the numbering and leading/trailing whitespace
                    var keyword = line.TrimStart(' ', '1', '2', '3', '4', '5', '.', ':');
                    return keyword;
                }).ToList();

                return keywordList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new List<string>(); // Return an empty list in case of an error
            }
        }
        public static async Task<List<string>> ExtractKeywordsfromUserQuery(string apiKey, string UserQuery)
        {

            // Initialize the OpenAI API client
            var openAiApi = new OpenAI_API.OpenAIAPI(apiKey);

            // Input text from which to extract keywords
            string customInstruction = $"Understand the Task and extract 1 to 3 important keywords related to the task. Do not share any additional information.\n Task:\n'{UserQuery}'";

            try
            {
                var response = await openAiApi.Completions.CreateCompletionAsync(new CompletionRequest()
                {
                    Model = "text-davinci-003",
                    Temperature = 0.1,
                    MaxTokens = 50,
                    Prompt = $"'{customInstruction}'\nKeywords:"
                }
                );


                // Extracted keywords from the response
                // Access the generated text (keywords) from the response
                var keywords = response.Completions[0].Text;
                var keywordLines = keywords.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));

                // Extract the keywords without the numbering
                var keywordList = keywordLines.Select(line =>
                {
                    // Remove the numbering and leading/trailing whitespace
                    var keyword = line.TrimStart(' ', '1', '2', '3', '4', '5', '.', ':');
                    return keyword;
                }).ToList();

                return keywordList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new List<string>(); // Return an empty list in case of an error
            }
        }





    }
}

