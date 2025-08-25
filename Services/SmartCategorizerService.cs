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
        private static readonly TimeSpan _minTimeBetweenCalls = TimeSpan.FromMilliseconds(1500); // MÁS CONSERVADOR

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
            var baseDelay = TimeSpan.FromSeconds(2); // 🔥 DELAY BASE MÁS CORTO
            
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

                        // PROMPT MÁS CORTO Y DIRECTO
                        var shortPrompt = $"Categoriza EXACTAMENTE en una palabra:\n" +
                                        $"Opciones: Política, Economía, Tecnología, Social, Internacional, Excluido\n\n" +
                                        $"Título: {title}\n" +
                                        $"Texto: {description.Substring(0, Math.Min(200, description.Length))}\n\n" +
                                        $"Categoría:";

                        var response = await CallGroqForCategorizationAsync(shortPrompt);
                        _lastApiCall = DateTime.Now;
                        
                        // Validar respuesta
                        var validCategories = new[] { "Política", "Economía", "Tecnología", "Social", "Internacional", "Excluido" };
                        
                        foreach (var category in validCategories)
                        {
                            if (response.Trim().Equals(category, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation($"✅ AI categorization successful on attempt {attempt}");
                                return category;
                            }
                        }
                        
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
                        var waitTime = TimeSpan.FromSeconds(5 + (attempt * 3)); // 🔥 8s, 11s, 14s
                        _logger.LogInformation($"⏳ Waiting {waitTime.TotalSeconds}s before retry...");
                        await Task.Delay(waitTime);
                        continue;
                    }
                    throw new Exception("Rate limit exceeded after multiple retries");
                }
                catch (Exception ex) when (ex.Message.Contains("InternalServerError") || ex.Message.Contains("Service overloaded"))
                {
                    _logger.LogError($"❌ Groq service error on attempt {attempt}/{maxRetries}: {ex.Message}");
                    
                    if (attempt < maxRetries)
                    {
                        // 🔥 Para errores 500, esperar menos tiempo pero más incremental
                        var waitTime = baseDelay.Add(TimeSpan.FromSeconds(attempt * 2)); // 2s, 4s, 6s
                        _logger.LogInformation($"⏳ Groq service error, waiting {waitTime.TotalSeconds}s before retry...");
                        await Task.Delay(waitTime);
                        continue;
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ API error on attempt {attempt}/{maxRetries}: {ex.Message}");
                    
                    if (attempt < maxRetries)
                    {
                        var waitTime = baseDelay.Add(TimeSpan.FromSeconds(attempt)); // 3s, 4s, 5s
                        await Task.Delay(waitTime);
                        continue;
                    }
                    throw;
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

            // 🔥 SOLUCIÓN: NO reutilizar el HttpClient inyectado
            // Crear un HttpClient nuevo para cada request
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "Categoriza en una palabra exacta: Política, Economía, Tecnología, Social, Internacional, Excluido"
                    },
                    new {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 10,
                temperature = 0.0,
                top_p = 0.8,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 🔥 Headers limpios en HttpClient nuevo
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ApiCausality360/1.0");

            try
            {
                var response = await httpClient.PostAsync(apiUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Groq API Error: {response.StatusCode} - {responseContent}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        throw new Exception("Groq InternalServerError - Service overloaded");
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        throw new Exception("TooManyRequests");
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        throw new Exception("Groq BadRequest - Invalid prompt");
                    }
                    
                    throw new Exception($"API Error: {response.StatusCode}");
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (result.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var messageContent))
                    {
                        var content_result = messageContent.GetString()?.Trim() ?? "Excluido";
                        
                        if (string.IsNullOrWhiteSpace(content_result))
                        {
                            _logger.LogWarning("⚠️ Groq returned empty content");
                            return "Excluido";
                        }
                        
                        return content_result;
                    }
                }

                _logger.LogError($"❌ Groq response format unexpected: {responseContent}");
                return "Excluido";
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("❌ Groq API timeout");
                throw new Exception("Groq API timeout");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Groq HTTP error: {ex.Message}");
                throw new Exception($"Groq HTTP error: {ex.Message}");
            }
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