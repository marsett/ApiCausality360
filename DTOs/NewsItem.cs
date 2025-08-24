namespace ApiCausality360.DTOs
{
    public class NewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string Category { get; set; } = string.Empty;

        // NUEVO: Para imágenes en el frontend
        public string? ImageUrl { get; set; }
    }
}