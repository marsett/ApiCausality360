using ApiCausality360.Services;

namespace ApiCausality360.DTOs
{
    public class Article
    {
        public Source Source { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime? PublishedAt { get; set; }
    }
}
