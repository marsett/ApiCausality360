using ApiCausality360.Services;

namespace ApiCausality360.DTOs
{
    // DTOs para NewsAPI
    public class NewsApiResponse
    {
        public string Status { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<Article> Articles { get; set; } = new();
    }
}
