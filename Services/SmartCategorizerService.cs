using System.Text.RegularExpressions;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ApiCausality360.Services
{
    public interface ISmartCategorizerService
    {
        string DetermineCategory(string title, string description);
        bool AreContentCoherent(string title, string description);
        bool IsRelevantForTargetCategories(string title, string description);
    }

    public class SmartCategorizerService : ISmartCategorizerService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SmartCategorizerService> _logger;
        
        // 🔥 RATE LIMITING ESTÁTICO COMPARTIDO
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastApiCall = DateTime.MinValue;
        // Reducir el tiempo entre llamadas para procesar más rápido
        private static readonly TimeSpan _minTimeBetweenCalls = TimeSpan.FromSeconds(1); // 🔥 REDUCIDO A 1 SEGUNDO

        public SmartCategorizerService(IConfiguration configuration, HttpClient httpClient, ILogger<SmartCategorizerService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public string DetermineCategory(string title, string description)
        {
            _logger.LogInformation($"🔍 Categorizing with AI: {title?.Substring(0, Math.Min(50, title.Length))}...");

            try
            {
                // 🔥 SOLO IA - CON RATE LIMITING Y RETRY
                var category = DetermineCategoryWithAI(title, description).Result;
                
                _logger.LogInformation($"     🎯 AI Selected: {category}");
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ AI categorization failed after retries: {ex.Message}");
                
                // 🔥 SI FALLA DEFINITIVAMENTE LA IA, EXCLUIR (NO CATEGORIZAR LOCALMENTE)
                return "Excluido";
            }
        }

        private async Task<string> DetermineCategoryWithAI(string title, string description)
        {
            const int maxRetries = 3;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 🔥 RATE LIMITING: Esperar si es necesario
                    await _rateLimitSemaphore.WaitAsync();
                    
                    try
                    {
                        var timeSinceLastCall = DateTime.Now - _lastApiCall;
                        if (timeSinceLastCall < _minTimeBetweenCalls)
                        {
                            var delayNeeded = _minTimeBetweenCalls - timeSinceLastCall;
                            _logger.LogInformation($"⏳ Rate limiting: waiting {delayNeeded.TotalSeconds:F1}s before API call (attempt {attempt})");
                            await Task.Delay(delayNeeded);
                        }

                        // 🔥 PROMPT MEJORADO PARA DIVERSIDAD DE CATEGORÍAS
                        var prompt = $"Categoriza la siguiente noticia en UNA de estas 5 categorías EXACTAS. IMPORTANTE: Busca diversidad, no todo es política:\n\n" +
                                    $"CATEGORÍAS VÁLIDAS (busca distribución equilibrada):\n" +
                                    $"- Política: Solo gobierno, partidos, elecciones, leyes específicas\n" +
                                    $"- Economía: Empresas, mercados, finanzas, PIB, inflación, trabajo, industria\n" +
                                    $"- Tecnología: Innovación, apps, software, inteligencia artificial, startups tech\n" +
                                    $"- Social: Emergencias, salud, educación, justicia, sociedad, cultura\n" +
                                    $"- Internacional: Países extranjeros, diplomacia, conflictos globales, UE\n\n" +
                                    $"NOTICIA A CATEGORIZAR:\n" +
                                    $"TÍTULO: {title}\n" +
                                    $"DESCRIPCIÓN: {description.Substring(0, Math.Min(300, description.Length))}\n\n" +
                                    $"INSTRUCCIONES CRÍTICAS:\n" +
                                    $"- PRIORIZA categorías menos obvias (Economía, Tecnología, Internacional)\n" +
                                    $"- Si menciona empresas, trabajo, industria → Economía\n" +
                                    $"- Si menciona otros países, UE, diplomacia → Internacional\n" +
                                    $"- Si menciona tecnología, apps, innovación → Tecnología\n" +
                                    $"- Solo usa 'Política' si es claramente sobre gobierno/partidos\n" +
                                    $"- Solo usa 'Social' si no encaja en las otras 4\n" +
                                    $"- Si es DEPORTES, ENTRETENIMIENTO, FAMOSOS → Responde: 'Excluido'\n\n" +
                                    $"RESPUESTA (una sola palabra):";

                        var response = await CallGroqForCategorizationAsync(prompt);
                        _lastApiCall = DateTime.Now;
                        
                        // VALIDAR que la respuesta sea una categoría válida
                        var validCategories = new[] { "Política", "Economía", "Tecnología", "Social", "Internacional", "Excluido" };
                        
                        foreach (var category in validCategories)
                        {
                            if (response.Trim().Equals(category, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation($"✅ AI categorization successful on attempt {attempt}");
                                return category;
                            }
                        }
                        
                        // Si la IA devuelve algo inesperado, excluir por seguridad
                        _logger.LogWarning($"⚠️ AI returned unexpected category: {response}. Excluding by default.");
                        return "Excluido";
                    }
                    finally
                    {
                        _rateLimitSemaphore.Release();
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("TooManyRequests") || ex.Message.Contains("429"))
                {
                    _logger.LogWarning($"⚠️ Rate limit hit on attempt {attempt}/{maxRetries}");
                    
                    if (attempt < maxRetries)
                    {
                        // 🔥 EXPONENTIAL BACKOFF: Esperar más tiempo en cada intento
                        var waitTime = TimeSpan.FromSeconds(10 * attempt); // 10s, 20s, 30s
                        _logger.LogInformation($"⏳ Waiting {waitTime.TotalSeconds}s before retry...");
                        await Task.Delay(waitTime);
                        continue;
                    }
                    else
                    {
                        _logger.LogError($"❌ Rate limit exceeded after {maxRetries} attempts");
                        throw new Exception("Rate limit exceeded after multiple retries");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ API error on attempt {attempt}/{maxRetries}: {ex.Message}");
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5 * attempt)); // 5s, 10s, 15s
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            
            throw new Exception("All retry attempts failed");
        }

        private async Task<string> CallGroqForCategorizationAsync(string prompt)
        {
            var apiUrl = _configuration["Groq:ApiUrl"];
            var apiKey = _configuration["Groq:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Groq API Key not configured");
            }

            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "Eres un categorizador de noticias experto. SOLO respondes con el nombre exacto de la categoría o 'Excluido'. NUNCA des explicaciones adicionales."
                    },
                    new {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 50, // Solo necesitamos una palabra
                temperature = 0.1, // Muy determinista
                top_p = 0.9,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (result.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var messageContent))
                    {
                        return messageContent.GetString()?.Trim() ?? "Excluido";
                    }
                }
            }

            throw new Exception($"API Error: {response.StatusCode}");
        }

        public bool AreContentCoherent(string title, string description)
        {
            // SIMPLIFICADO: Validación básica de calidad
            return !string.IsNullOrWhiteSpace(title) && 
                   title.Length > 10 && 
                   !string.IsNullOrWhiteSpace(description) && 
                   description.Length > 30;
        }

        public bool IsRelevantForTargetCategories(string title, string description)
        {
            // SIMPLIFICADO: Solo verificar que la categoría asignada no sea "Excluido"
            var category = DetermineCategory(title, description);
            var isRelevant = category != "Excluido";
            
            _logger.LogInformation($"     🎯 Category '{category}' → {(isRelevant ? "RELEVANT" : "NOT RELEVANT")}");
            
            return isRelevant;
        }
    }
}