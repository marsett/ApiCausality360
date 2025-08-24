using System.Text.RegularExpressions;
using System.Xml;

namespace ApiCausality360.Services
{
    public interface IImageExtractionService
    {
        string? ExtractImageFromRssItem(System.ServiceModel.Syndication.SyndicationItem item);
        string? GetFallbackImageByCategory(string category);
    }

    public class ImageExtractionService : IImageExtractionService
    {
        private readonly ILogger<ImageExtractionService> _logger;

        public ImageExtractionService(ILogger<ImageExtractionService> logger)
        {
            _logger = logger;
        }

        public string? ExtractImageFromRssItem(System.ServiceModel.Syndication.SyndicationItem item)
        {
            try
            {
                // 1. Buscar en enclosures (attachments)
                var enclosures = item.Links?.Where(l => l.RelationshipType == "enclosure");
                if (enclosures?.Any() == true)
                {
                    var imageEnclosure = enclosures.FirstOrDefault(e => 
                        e.MediaType?.StartsWith("image/") == true);
                    if (imageEnclosure != null && IsValidImageUrl(imageEnclosure.Uri?.ToString()))
                    {
                        return imageEnclosure.Uri?.ToString();
                    }
                }

                // 2. Buscar en el contenido HTML
                var content = item.Summary?.Text ?? item.Content?.ToString() ?? "";
                if (!string.IsNullOrEmpty(content))
                {
                    var imageUrl = ExtractImageFromHtml(content);
                    if (!string.IsNullOrEmpty(imageUrl) && IsValidImageUrl(imageUrl))
                    {
                        return imageUrl;
                    }
                }

                // 3. Buscar en extensiones específicas del RSS
                if (item.ElementExtensions?.Any() == true)
                {
                    foreach (var extension in item.ElementExtensions)
                    {
                        if (extension.OuterName == "thumbnail" || 
                            extension.OuterName == "image" ||
                            extension.GetObject<XmlElement>()?.GetAttribute("url") != null)
                        {
                            var imageUrl = extension.GetObject<XmlElement>()?.GetAttribute("url");
                            if (!string.IsNullOrEmpty(imageUrl) && IsValidImageUrl(imageUrl))
                            {
                                return imageUrl;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error extracting image from RSS item: {ex.Message}");
                return null;
            }
        }

        private string? ExtractImageFromHtml(string htmlContent)
        {
            try
            {
                // Buscar tags <img> en el HTML
                var imgPattern = @"<img[^>]+src\s*=\s*['""]([^'""]+)['""][^>]*>";
                var match = Regex.Match(htmlContent, imgPattern, RegexOptions.IgnoreCase);
                
                if (match.Success)
                {
                    var imageUrl = match.Groups[1].Value;
                    
                    // Validar que sea una URL válida de imagen
                    if (IsValidImageUrl(imageUrl))
                    {
                        return imageUrl;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool IsValidImageUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            try
            {
                // Verificar que sea una URL válida HTTP/HTTPS
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return false;
                }

                // MEJORA: Verificar que no sea un XML o contenido inválido
                if (url.Contains("<?xml") || url.Contains("<rss") || 
                    url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    url.EndsWith(".rss", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Verificar extensiones de imagen comunes
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
                var hasImageExtension = imageExtensions.Any(ext => 
                    url.Contains(ext, StringComparison.OrdinalIgnoreCase));

                // Si tiene parámetros de query que sugieren imagen (como unsplash, imgur, etc.)
                var hasImageParams = url.Contains("w=") || url.Contains("h=") || 
                                   url.Contains("fit=crop") || url.Contains("format=");

                // Dominios conocidos de imágenes
                var imageDomains = new[] { "unsplash.com", "images.unsplash.com", "imgur.com", 
                                         "i.imgur.com", "cloudinary.com", "amazonaws.com" };
                var isImageDomain = imageDomains.Any(domain => 
                    uri.Host.Contains(domain, StringComparison.OrdinalIgnoreCase));

                return hasImageExtension || hasImageParams || isImageDomain;
            }
            catch
            {
                return false;
            }
        }

        public string? GetFallbackImageByCategory(string category)
        {
            // URLs de imágenes por defecto según categoría
            return category.ToLower() switch
            {
                "política" => "https://images.unsplash.com/photo-1529107386315-e1a2ed48a620?w=800&h=400&fit=crop",
                "economía" => "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=800&h=400&fit=crop",
                "tecnología" => "https://images.unsplash.com/photo-1518709268805-4e9042af2176?w=800&h=400&fit=crop",
                "internacional" => "https://images.unsplash.com/photo-1484807352052-23338990c6c6?w=800&h=400&fit=crop",
                "social" => "https://images.unsplash.com/photo-1544027993-37dbfe43562a?w=800&h=400&fit=crop",
                _ => "https://images.unsplash.com/photo-1586339949916-3e9457bef6d3?w=800&h=400&fit=crop"
            };
        }
    }
}