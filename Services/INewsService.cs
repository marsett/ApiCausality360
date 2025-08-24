using ApiCausality360.DTOs;

namespace ApiCausality360.Services
{
    public interface INewsService
    {
        Task<List<NewsItem>> GetTodayNewsAsync(string country = "es", int maxResults = 10);
        Task<List<NewsItem>> SearchNewsAsync(string query, DateTime? from = null, int maxResults = 5);
    }
}
