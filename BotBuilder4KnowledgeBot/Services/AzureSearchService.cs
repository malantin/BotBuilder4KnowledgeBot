using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotBuilder4KnowledgeBot.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace BotBuilder4KnowledgeBot.Services
{
    /// <summary>
    /// A service class for making queries to Azure search
    /// </summary>
    public static class AzureSearchService
    {
        /// <summary>
        /// Creates an Azure Search Service Client instance for creating and managing indexes
        /// </summary>
        /// <param name="searchServiceName">The name of your Azure Search Service</param>
        /// <param name="adminApiKey">The admin Api Key for your Azure Search Service</param>
        /// <returns></returns>
        public static SearchServiceClient CreateSearchServiceClient(string searchServiceName, string adminApiKey)
        {
            SearchServiceClient serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));

            return serviceClient;
        }

        /// <summary>
        /// Create an Azure Search Service Index Client instance for making queries
        /// </summary>
        /// <param name="searchServiceName">The name of your Azure Search Service</param>
        /// <param name="indexName">The name of your Index</param>
        /// <param name="queryApiKey">You query API key</param>
        /// <returns></returns>
        public static SearchIndexClient CreateSearchIndexClient(string searchServiceName, string indexName, string queryApiKey)
        {
            SearchIndexClient indexClient = new SearchIndexClient(searchServiceName, indexName, new SearchCredentials(queryApiKey));

            return indexClient;
        }

        /// <summary>
        /// Does a query for a given string, using custom parameters
        /// </summary>
        /// <param name="searchString">The string you are searching for</param>
        /// <param name="indexClient">The index client to use for the query</param>
        /// <param name="parameters">Your custom search parameters</param>
        /// <returns></returns>
        public static DocumentSearchResult<SearchDocument> RunQuery(string searchString, SearchIndexClient indexClient, SearchParameters parameters)
        {
            // Search the entire index for a term and return some of the fields.

            return indexClient.Documents.Search<SearchDocument>(searchString, parameters);

            //From the sample project

            //parameters =
            //    new SearchParameters()
            //    {
            //        Filter = "baseRate lt 150",
            //        Select = new[] { "hotelId", "description" }
            //    };          

            //parameters =
            //    new SearchParameters()
            //    {
            //        OrderBy = new[] { "lastRenovationDate desc" },
            //        Select = new[] { "hotelName", "lastRenovationDate" },
            //        Top = 2
            //    };
        }

        /// <summary>
        /// A helper method to decode a Base64 encoded url from an Azure search result
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static string DecodeUrl(string encodedUrl)
        {
            // Remove the trailing char
            encodedUrl = encodedUrl.Substring(0, encodedUrl.Length - 1);
            // Decode the Base64 encoded string to a byte array and then encode to string.
            var url = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedUrl));
            return url;
        }
    }
}

