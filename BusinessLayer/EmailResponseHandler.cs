using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenAI_API;


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
        //public static List<string> ExtractKeywordsFromEmailDomain(string email)
        //{
        //    try
        //    {
        //        // Extract the domain from the email
        //        string[] parts = email.Split('@');
        //        if (parts.Length >= 2)
        //        {
        //            string domain = parts[1];

        //            // Define the prompt to send to OpenAI
        //            string prompt = $"Extract keywords from the email domain '{domain}'.";

        //            // Set up the API request
        //            var request = new  CreateCompletionRequestuest
        //            {
        //                Model = "text-davinci-1",
        //                Prompt = prompt,
        //                MaxTokens = 30 // Adjust the number of tokens as needed
        //            };

        //            // Call the OpenAI API to generate keywords
        //            var response = OpenAI.Completion.Create(request);

        //            // Extract and return the generated keywords
        //            List<string> keywords = response.Choices.Select(choice => choice.Text).ToList();
        //            return keywords;
        //        }
        //        else
        //        {
        //            Console.WriteLine("Invalid email format.");
        //            return null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Error extracting keywords: " + ex.Message);
        //        return null;
        //    }
        //}

    }
}
