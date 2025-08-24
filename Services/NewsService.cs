using ApiCausality360.DTOs;
using System.Text.Json;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiCausality360.Services
{
    public class NewsService : INewsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IImageExtractionService _imageExtractionService;
        private readonly ISmartCategorizerService _smartCategorizer;

        // RSS Feeds de medios españoles seleccionados - 5 FUENTES
        private readonly Dictionary<string, string> _spanishFeeds = new()
        {
            { "La Vanguardia", "https://www.lavanguardia.com/rss/home.xml" },
            { "OK Diario", "https://okdiario.com/rss" },
            { "El Español", "https://www.elespanol.com/rss/" },
            { "El Mundo", "https://e00-elmundo.uecdn.es/elmundo/rss/espana.xml" },
            { "20 Minutos", "https://www.20minutos.es/rss/" }
        };

        // Las 5 categorías exactas del Context
        private readonly List<string> _targetCategories = new()
        {
            "Política",       // Id = 1
            "Economía",       // Id = 2  
            "Tecnología",     // Id = 3
            "Social",         // Id = 4
            "Internacional"   // Id = 5
        };

        public NewsService(HttpClient httpClient, IConfiguration configuration,
            IImageExtractionService imageExtractionService, ISmartCategorizerService smartCategorizer)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _imageExtractionService = imageExtractionService;
            _smartCategorizer = smartCategorizer;

            // Configurar User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "ApiCausality360/1.0 RSS Reader (https://localhost:7204)");
        }

        public async Task<List<NewsItem>> GetTodayNewsAsync(string country = "es", int maxResults = 5)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            Console.WriteLine($"🗞️ Fetching RSS news for today: {today:dd/MM/yyyy}");
            Console.WriteLine($"🎯 Strategy: 5 sources - AUTHENTIC categorization based on content");

            var selectedNews = new List<NewsItem>();

            // 🔥 NUEVA ESTRATEGIA: Una noticia por fuente con categorías AUTÉNTICAS
            foreach (var feedConfig in _spanishFeeds)
            {
                var sourceName = feedConfig.Key;
                var feedUrl = feedConfig.Value;
                
                try
                {
                    Console.WriteLine($"📡 Processing {sourceName} → Looking for the BEST authentic article");
                    
                    var feedNews = await GetRssNewsAsync(feedUrl, sourceName);
                    var recentNews = feedNews.Where(n => n.PublishedAt >= yesterday).ToList();

                    Console.WriteLine($"     📊 Found {recentNews.Count} recent articles from {sourceName}");

                    // 🔥 SELECCIONAR LA MEJOR NOTICIA VÁLIDA (sin forzar categorías)
                    var bestArticle = recentNews
                        .Where(n => n.Category != "Excluido") // Solo válidas
                        .Where(n => _targetCategories.Contains(n.Category)) // Solo nuestras 5 categorías
                        .Where(n => IsValidNewsItem(n))
                        .OrderByDescending(n => GetRelevanceScore(n))
                        .FirstOrDefault();

                    if (bestArticle != null)
                    {
                        selectedNews.Add(bestArticle);
                        Console.WriteLine($"   ✅ SELECTED: {bestArticle.Title.Substring(0, Math.Min(50, bestArticle.Title.Length))}... → {bestArticle.Category}");
                        Console.WriteLine($"       🎯 AUTHENTIC category based on content analysis");
                    }
                    else
                    {
                        Console.WriteLine($"   ❌ NO VALID ARTICLES found in {sourceName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Error processing {sourceName}: {ex.Message}");
                }
            }

            // 🔥 RESULTADO FINAL
            Console.WriteLine($"\n🎯 FINAL RESULT: {selectedNews.Count} articles from {selectedNews.Count} different sources");
            
            if (selectedNews.Any())
            {
                var sourceStats = selectedNews.GroupBy(n => n.Source).ToDictionary(g => g.Key, g => g.Count());
                var categoryStats = selectedNews.GroupBy(n => n.Category).ToDictionary(g => g.Key, g => g.Count());
                
                Console.WriteLine($"   ✅ AUTHENTIC CATEGORIZATION ACHIEVED!");
                Console.WriteLine($"   📰 Sources: {string.Join(", ", sourceStats.Keys)}");
                Console.WriteLine($"   🏷️ Categories (GENUINE): {string.Join(", ", categoryStats.Keys)}");
                
                Console.WriteLine($"\n   📋 DETAILED BREAKDOWN:");
                foreach (var news in selectedNews)
                {
                    Console.WriteLine($"   📄 {news.Source} | {news.Category} | {news.Title.Substring(0, Math.Min(60, news.Title.Length))}...");
                }

                // 🔥 ESTADÍSTICAS DE CATEGORÍAS AUTÉNTICAS
                Console.WriteLine($"\n   📊 CATEGORY DISTRIBUTION (Based on actual content):");
                foreach (var categoryGroup in categoryStats)
                {
                    Console.WriteLine($"       🏷️ {categoryGroup.Key}: {categoryGroup.Value} article{(categoryGroup.Value != 1 ? "s" : "")}");
                }
            }
            else
            {
                Console.WriteLine($"   ⚠️ No valid articles found from any source");
            }

            return selectedNews.Take(maxResults).ToList();
        }

        public async Task<List<NewsItem>> SearchNewsAsync(string query, DateTime? from = null, int maxResults = 5)
        {
            var allNews = await GetTodayNewsAsync("es", 50);
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var filteredNews = allNews
                .Where(n => queryWords.Any(word =>
                    n.Title.ToLower().Contains(word) ||
                    n.Description.ToLower().Contains(word)))
                .Take(maxResults)
                .ToList();

            return filteredNews;
        }

        private bool IsValidNewsItem(NewsItem news)
        {
            if (string.IsNullOrWhiteSpace(news.Title) || news.Title.Length < 30)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(news.Description) || news.Description.Length < 100)
            {
                return false;
            }

            // Usar el categorizador inteligente para verificar coherencia
            return _smartCategorizer.AreContentCoherent(news.Title, news.Description);
        }

        private bool IsValidNewsItemRelaxed(NewsItem news)
        {
            if (string.IsNullOrWhiteSpace(news.Title) || news.Title.Length < 20)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(news.Description) || news.Description.Length < 50)
            {
                return false;
            }

            // Usar el categorizador inteligente para verificar coherencia (más permisivo)
            return _smartCategorizer.AreContentCoherent(news.Title, news.Description);
        }

        private async Task<List<NewsItem>> GetRssNewsAsync(string rssUrl, string sourceName)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(rssUrl);
                var news = new List<NewsItem>();

                using var stringReader = new StringReader(response);
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    ValidationType = ValidationType.None
                };
                using var xmlReader = XmlReader.Create(stringReader, settings);

                var feed = SyndicationFeed.Load(xmlReader);
                Console.WriteLine($"     📡 Processing {feed.Items.Count()} items from {sourceName}");

                // 🔥 PROCESAR MÁS ITEMS PARA TENER MEJORES OPCIONES
                foreach (var item in feed.Items.Take(10))
                {
                    try
                    {
                        var publishDate = item.PublishDate.DateTime;
                        if (publishDate == DateTime.MinValue || publishDate > DateTime.Now)
                        {
                            publishDate = DateTime.Now;
                        }

                        var description = item.Summary?.Text ?? "";
                        if (string.IsNullOrEmpty(description) && item.Content != null)
                        {
                            if (item.Content is TextSyndicationContent textContent)
                                description = textContent.Text;
                        }

                        var cleanTitle = CleanHtmlTags(item.Title?.Text ?? "Sin título");
                        var cleanDescription = CleanHtmlTags(description);

                        // VALIDACIÓN DE COHERENCIA BÁSICA (sin IA para acelerar)
                        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle.Length < 20 ||
                            string.IsNullOrWhiteSpace(cleanDescription) || cleanDescription.Length < 50)
                        {
                            Console.WriteLine($"     ❌ SKIPPED low quality: {cleanTitle.Substring(0, Math.Min(40, cleanTitle.Length))}...");
                            continue;
                        }

                        if (cleanTitle.Length > 400)
                            cleanTitle = cleanTitle.Substring(0, 397) + "...";

                        if (cleanDescription.Length > 800)
                            cleanDescription = cleanDescription.Substring(0, 797) + "...";

                        // 🔥 CATEGORIZACIÓN AUTÉNTICA CON IA - SIN FORZAR NADA
                        var category = _smartCategorizer.DetermineCategory(cleanTitle, cleanDescription);

                        // 🔥 FILTRAR INMEDIATAMENTE SI ES "Excluido"
                        if (category == "Excluido")
                        {
                            Console.WriteLine($"     ❌ EXCLUDED by AI: {cleanTitle.Substring(0, Math.Min(40, cleanTitle.Length))}...");
                            continue;
                        }

                        var newsItem = new NewsItem
                        {
                            Title = cleanTitle,
                            Description = cleanDescription,
                            Source = sourceName,
                            Url = item.Links.FirstOrDefault()?.Uri?.ToString() ?? "",
                            PublishedAt = publishDate,
                            Category = category, // 🔥 CATEGORÍA AUTÉNTICA DE LA IA
                            ImageUrl = _imageExtractionService?.ExtractImageFromRssItem(item) ??
                                      GetFallbackImageByCategory(category)
                        };

                        news.Add(newsItem);
                        Console.WriteLine($"     ✅ ADDED: {cleanTitle.Substring(0, Math.Min(40, cleanTitle.Length))}... → {category} (AUTHENTIC)");

                        // 🔥 RECOPILAR MÁS OPCIONES PARA MEJOR SELECCIÓN
                        if (news.Count >= 8) // Más opciones por fuente
                        {
                            Console.WriteLine($"     ⚡ Got {news.Count} options from {sourceName}");
                            break;
                        }
                    }
                    catch (Exception itemEx)
                    {
                        Console.WriteLine($"     ❌ Error processing item: {itemEx.Message}");
                    }
                }

                Console.WriteLine($"     📊 {sourceName}: {news.Count} valid articles processed");
                return news;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RSS Error for {sourceName}: {ex.Message}");
                return new List<NewsItem>();
            }
        }

        private bool IsRelevantNews(NewsItem news)
        {
            // CORREGIDO: Validaciones básicas de calidad
            if (string.IsNullOrWhiteSpace(news.Title) || news.Title.Length < 30 ||
                string.IsNullOrWhiteSpace(news.Description) || news.Description.Length < 100)
            {
                return false;
            }
            
            // Verificar si la categoría asignada es una de las 5 objetivo
            var targetCategories = new[] { "Política", "Economía", "Tecnología", "Social", "Internacional" };
            return targetCategories.Contains(news.Category);
        }

        private bool IsRelevantNewsRelaxed(NewsItem news)
        {
            // CORREGIDO: Validaciones básicas más permisivas
            if (string.IsNullOrWhiteSpace(news.Title) || news.Title.Length < 20 ||
                string.IsNullOrWhiteSpace(news.Description) || news.Description.Length < 50)
            {
                return false;       
            }
            
            // Verificar si la categoría asignada es una de las 5 objetivo (modo relajado igual)
            var targetCategories = new[] { "Política", "Economía", "Tecnología", "Social", "Internacional" };
            return targetCategories.Contains(news.Category);
        }

        private int GetRelevanceScore(NewsItem news)
        {
            var score = 0;

            // 🔥 PUNTUACIÓN EQUILIBRADA - SIN FAVORECER NINGUNA CATEGORÍA
            // Todas las categorías tienen valor base de 80
            score += 80;

            // Bonus por longitud de descripción (más contenido = mejor)
            if (news.Description.Length > 300) score += 10;
            if (news.Description.Length > 500) score += 15;

            // Bonus por tener imagen
            if (!string.IsNullOrEmpty(news.ImageUrl)) score += 5;

            // 🔥 BONUS POR CALIDAD DEL TÍTULO (títulos más informativos)
            if (news.Title.Length > 60 && news.Title.Length < 120) score += 5;

            // 🔥 BONUS POR COHERENCIA ENTRE TÍTULO Y DESCRIPCIÓN
            if (_smartCategorizer.AreContentCoherent(news.Title, news.Description)) score += 10;

            return Math.Max(score, 0);
        }

        private string GetFallbackImageByCategory(string category)
        {
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

        private string CleanHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            var cleaned = input.Replace("<br>", " ")
                              .Replace("<br/>", " ")
                              .Replace("<br />", " ")
                              .Replace("</p>", " ")
                              .Replace("</div>", " ");

            cleaned = Regex.Replace(cleaned, @"<[^>]*>", " ");

            cleaned = cleaned.Replace("&nbsp;", " ")
                            .Replace("&amp;", "&")
                            .Replace("&quot;", "\"")
                            .Replace("&apos;", "'")
                            .Replace("&lt;", "<")
                            .Replace("&gt;", ">")
                            .Replace("&#39;", "'")
                            .Replace("&#x27;", "'")
                            .Replace("&hellip;", "...")
                            .Replace("&mdash;", "—")
                            .Replace("&ndash;", "–");

            cleaned = Regex.Replace(cleaned, @"&#\d+;", " ");
            cleaned = Regex.Replace(cleaned, @"&#x[0-9a-fA-F]+;", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = Regex.Replace(cleaned, @"[\x00-\x1F\x7F]", "");

            return cleaned.Trim();
        }
    }
}