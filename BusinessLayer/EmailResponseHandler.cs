using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;


namespace BusinessLayer
{
    public class EmailResponseHandler
    {
       
        public string SearchKeywordsInCaseStudyXML(List<string> keywords,string xmlContent)
        {
            try
            {
                // Load the XML content into an XDocument
                XDocument xdoc = XDocument.Parse(xmlContent);

                // Use LINQ to XML to search for keywords in <name> and <tags> elements
                var query = from item in xdoc.Descendants("item")
                            let name = item.Element("name")?.Value ?? ""
                            let tags = item.Descendants("tagname").Select(tag => tag.Value)
                            let combinedText = name + " " + string.Join(" ", tags)
                            from keyword in keywords
                            where combinedText.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            select item.Element("url")?.Value;

                // Get the first matching URL or return null if no match is found
                string matchedUrl = query.FirstOrDefault();

                return matchedUrl;
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during XML parsing or keyword search
                Console.WriteLine("Error searching keywords in XML: " + ex.Message);
                return null;
            }
        }
    }
}
