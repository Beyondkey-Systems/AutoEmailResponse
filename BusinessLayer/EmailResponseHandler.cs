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

namespace BusinessLayer
{
    public class EmailResponseHandler
    {

        public List<string> SearchKeywordsInCaseStudyXML(List<string> keywords, string xmlContent)
        {
            XDocument xdoc = XDocument.Parse(xmlContent);
            int maxMatches = 0;
            List<string> matchedUrl = new List<string>();

            // First, search in <ptags>
            foreach (var item in xdoc.Descendants("item"))
            {
                IEnumerable<string> tags = item.Descendants("ptags").Descendants("tagname").Select(tag => tag.Value);
                string combinedText = string.Join(" ", tags);

                int matches = keywords.Sum(keyword =>
                    Regex.Matches(combinedText, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase).Count);

                if (matches > maxMatches)
                {
                    matchedUrl.Clear();
                    maxMatches = matches;
                    matchedUrl.Add(item.Element("url")?.Value);
                }
            }

            if (matchedUrl.Count > 0) return matchedUrl; // If URL found in ptags, return it

            // Next, search in <name>
            foreach (var item in xdoc.Descendants("item"))
            {
                string name = item.Element("name")?.Value ?? "";

                int matches = keywords.Sum(keyword =>
                    Regex.Matches(name, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase).Count);

                if (matches > maxMatches)
                {
                    matchedUrl.Clear();
                    maxMatches = matches;
                    matchedUrl.Add(item.Element("url")?.Value);
                }
            }

            if (matchedUrl.Count > 0) return matchedUrl; // If URL found in name, return it

            // Finally, search in <tags>
            foreach (var item in xdoc.Descendants("item"))
            {
                IEnumerable<string> tags = item.Descendants("tags").Descendants("tagname").Select(tag => tag.Value);
                string combinedText = string.Join(" ", tags);

                int matches = keywords.Sum(keyword =>
                    Regex.Matches(combinedText, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase).Count);

                if (matches > maxMatches)
                {
                    matchedUrl.Clear();
                    maxMatches = matches;
                    matchedUrl.Add(item.Element("url")?.Value);
                }
            }

            return matchedUrl;
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
        public static async Task<string> ExtractKeywordsFromDomain(string apiKey, string Domain)
        {
            string customInstruction = $"You are a language model, understand the core business of domain: {Domain} and based on core business, extract only 5 keywords. do not share any additional information";

            string endpoint = "https://api.openai.com/v1/chat/completions"; // Use the appropriate endpoint

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                var jsonBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                      {
                            new { role = "system", content = customInstruction }
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
                return responseContent;
            }
        }
        public static async Task<List<string>> ExtractKeywordsfromDomain(string apiKey, string Domain)
        {

            // Initialize the OpenAI API client
            var openAiApi = new OpenAI_API.OpenAIAPI(apiKey);

            // Input text from which to extract keywords
            string customInstruction = $"You are a language model, understand the core business of domain: {Domain} and based on core business, extract only 1 keywords. do not share any additional information";

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
            string customInstruction = $"You are a language model, understand the user's query: '{UserQuery}' and extract only 5 keywords. Do not share any additional information.";

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

