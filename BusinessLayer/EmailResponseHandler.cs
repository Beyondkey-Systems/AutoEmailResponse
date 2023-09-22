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

namespace BusinessLayer
{
    public class EmailResponseHandler
    {

        public string SearchKeywordsInCaseStudyXML(List<string> keywords, string xmlContent)
        {
            try
            {
                // Load the XML content into an XDocument
                XDocument xdoc = XDocument.Parse(xmlContent);

                // Initialize variables to keep track of the maximum matches and the corresponding URL
                int maxMatches = 0;
                string matchedUrl = null;

                // Iterate through each item in the XML
                foreach (var item in xdoc.Descendants("item"))
                {
                    string name = item.Element("name")?.Value ?? "";
                    IEnumerable<string> tags = item.Descendants("tagname").Select(tag => tag.Value);

                    // Combine the name and tags into a single text for keyword search
                    string combinedText = name + " " + string.Join(" ", tags);

                    // Count the number of matched keywords for this item
                    int matches = keywords.Count(keyword =>
                        combinedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                    // If the current item has more matches than the previous maximum, update the maximum matches and URL
                    if (matches > maxMatches)
                    {
                        maxMatches = matches;
                        matchedUrl = item.Element("url")?.Value;
                    }
                }

                return matchedUrl;
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during XML parsing or keyword search
                Console.WriteLine("Error searching keywords in XML: " + ex.Message);
                return null;
            }
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
            string customInstruction = $"You are a language model, understand the core business of domain: {Domain} and based on core business, extract only 5 keywords. do not share any additional information";

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

