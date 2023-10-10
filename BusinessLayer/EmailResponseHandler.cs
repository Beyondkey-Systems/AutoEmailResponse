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
        public async Task<List<string>> FindCaseStudy(string contentRootPath, string apiKey, string Domain, string inputText)
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

            var UserQuery_DomainKeywords = UserQueryKeywords.Concat(Domainkeywords).ToList();

            /*********FIRST ATTEMPT (PASSING USER QUERY KEYWORD TO GET CASE STUDY***********/
            var Url = SearchKeywordsInCaseStudyXML(UserQueryKeywords, xmlContent);
            if (Url.Count > 0) return Url;

            /*********SECOND ATTEMPT (PASSING DOMAIN AND USER QUERY KEYWORD TO GET CASE STUDY***********/
            Url = emailResponseHandler.SearchKeywordsInCaseStudyXML(UserQuery_DomainKeywords, xmlContent);
            return Url;
        }

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
                {
                    var matchesForKeyword = Regex.Matches(combinedText, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase);
                    return matchesForKeyword.Count > 0 ? 1 : 0; // Consider each keyword match as 1
                });

                if (matches > maxMatches)
                {
                    matchedUrl.Clear();
                    maxMatches = matches;
                    matchedUrl.Add(item.Element("url")?.Value);
                }
                if (matches == maxMatches)
                    matchedUrl.Add(item.Element("url")?.Value);
            }

            if (matchedUrl.Count > 0) return matchedUrl; // If URL found in ptags, return it

            return matchedUrl;
        }

        public static async Task<List<string>> ExtractMatchedKeywords(string contentRootPath, string apiKey, string inputText)
        {

            string relativeFilePath = "DB/Keyword.xml";

            // Combine the content root path with the relative file path
            string physicalFilePath = Path.Combine(contentRootPath, relativeFilePath);
            List<string> masterKeywords = new List<string>();
            XDocument xmlDocument = XDocument.Load(physicalFilePath);

            // Iterate through XML elements and add keywords to the list
            foreach (XElement category in xmlDocument.Root.Elements())
            {
                foreach (XElement keywordElement in category.Elements("tagname"))
                {
                    string keyword = keywordElement.Value;
                    masterKeywords.Add(keyword);
                }
            }

            // Initialize the OpenAI API client
            var openAiApi = new OpenAIAPI(apiKey);

            // Analyze the content using OpenAI
            var response = await openAiApi.Completions.CreateCompletionAsync(new CompletionRequest()
            {
                Model = "text-davinci-003",
                Temperature = 0.1,
                MaxTokens = 50, // Adjust this value as needed
                Prompt = $"Analyze the following text and provide key information:\n\nText: \"{inputText}\"\n\nPlease provide key details."
            });

            // Extracted information from the OpenAI analysis
            string extractedInfo = response.Completions[0].Text;


            // Split the extracted information into words
            string[] extractedWords = extractedInfo.Split(new[] { ' ', ',', '.', ';', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Find keywords that match the master list (case-insensitive)
            var matchedKeywords = extractedWords
            .Where(word => masterKeywords.Any(keyword => keyword.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();



            if (matchedKeywords.Any())
                return matchedKeywords;
            else
                return new List<string> { "Other" };
        }


        /*
        public static async Task<List<string>> ExtractMatchedKeywords(string contentRootPath, string apiKey, string userQuery)
        {
            var UserQueryKeywords = await ExtractKeywordsfromUserQuery(apiKey, userQuery);

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
            string prompt = $"{customInstruction}\n\nBased on Available keywords, get relevant exact keywords only in comma separated format, from below Keyword List, Do not share any additional information. If no matching keyword found then share 'Others' as keyword\r\nKeyword List:\n{string.Join(", ", keywords)} \n###Desired Output Format###\nKeyword1,Keyword2,Keyword3";

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
        */

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

        //public static async Task<bool> IsCareerRelated(string apiKey, string inputText)
        //{

        //    // Initialize the OpenAI API client
        //    var openAiApi = new OpenAI_API.OpenAIAPI(apiKey);

        //    // Input text from which to extract keywords
        //    string customInstruction = $"Understand the following text, return response as 'True' if it is career related, job application, resume, asking for vacancy, asking for job or similar kind of content, else return response as False\n\nText: {inputText}\n\ndo not share any additional information";


        //    var response = await openAiApi.Completions.CreateCompletionAsync(new CompletionRequest()
        //    {
        //        Model = "text-davinci-003",
        //        Temperature = 0.1,
        //        MaxTokens = 50,
        //        Prompt = $"'{customInstruction}'"
        //    }
        //    );


        //    // Extracted keywords from the response
        //    // Access the generated text (keywords) from the response
        //    var res = response.Completions[0].Text;
        //    bool flag = false;
        //    bool IsCareerRelated = false;
        //    flag = bool.TryParse(res, out IsCareerRelated);
        //    return flag ? IsCareerRelated : flag;

        //}
        public static async Task<bool> IsCareerRelated(string apiKey, string inputText)
        {
            // Initialize the OpenAI API client
            var openAiApi = new OpenAI_API.OpenAIAPI(apiKey);

            // Prompt for OpenAI to determine if the text is career-related
            string prompt = $"Is the following text career-related?\n\nText: \"{inputText}\"\n\nPlease respond with 'Yes' or 'No'.";

            var response = await openAiApi.Completions.CreateCompletionAsync(new CompletionRequest()
            {
                Model = "text-davinci-003",
                Temperature = 0.1,
                MaxTokens = 1, // Limit response to a single token (Yes or No)
                Prompt = prompt
            });

            // Extract the response (Yes or No) from the completion
            string responseText = response.Completions[0].Text.Trim().ToLower();

            // Check if the response indicates 'Yes' for career-related
            return responseText == "yes";
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

        public static bool IsCaseStudyToShow(List<string> keywords, XDocument xmlDocument)
        {
            bool IsCaseStudyToShow = false;


            bool isFoundInM365 = keywords.Any(k =>
                xmlDocument.Element("root").Element("M365").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInProducts = keywords.Any(k =>
                xmlDocument.Element("root").Element("Products").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInPowerBI = keywords.Any(k =>
                xmlDocument.Element("root").Element("PowerBI").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInInquiry = keywords.Any(k =>
                xmlDocument.Element("root").Element("Inquiry").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInOthers = keywords.Any(k =>
                xmlDocument.Element("root").Element("Others").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));

            //if none of match then false
            if (isFoundInM365 == false && isFoundInProducts == false && isFoundInPowerBI == false && isFoundInInquiry == false)
                return false;

            if (isFoundInM365 == false && isFoundInProducts == false && isFoundInPowerBI == false && isFoundInInquiry == true && isFoundInOthers == false)
                return true;

            if (isFoundInM365 || isFoundInProducts || isFoundInPowerBI)
                IsCaseStudyToShow = true;


            return IsCaseStudyToShow;
        }

        public static bool IsWebSiteUrlToShow(List<string> keywords, XDocument xmlDocument)
        {
            bool IsWebSiteUrlToShow = false;


            bool isFoundInM365 = keywords.Any(k =>
                xmlDocument.Element("root").Element("M365").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInProducts = keywords.Any(k =>
                xmlDocument.Element("root").Element("Products").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInPowerBI = keywords.Any(k =>
                xmlDocument.Element("root").Element("PowerBI").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInInquiry = keywords.Any(k =>
                xmlDocument.Element("root").Element("Inquiry").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));
            bool isFoundInOthers = keywords.Any(k =>
                xmlDocument.Element("root").Element("Others").Elements("tagname").Any(e => e.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));

            //if none of match then false
            if (isFoundInM365 == false && isFoundInProducts == false && isFoundInPowerBI == false && isFoundInInquiry == false)
                return false;

            if (isFoundInM365 || isFoundInProducts || isFoundInPowerBI)
                IsWebSiteUrlToShow = true;


            return IsWebSiteUrlToShow;
        }



    }
}

