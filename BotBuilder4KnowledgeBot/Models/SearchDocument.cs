using Microsoft.Azure.Search.Models;

namespace BotBuilder4KnowledgeBot.Models
{
    [SerializePropertyNamesAsCamelCase]
    public class SearchDocument
    {
        public string metadata_storage_path { get; set; }
        public string content { get; set; }
        public string language { get; set; }
        public string[] keyphrases { get; set; }
    }
}
