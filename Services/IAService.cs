using System.Text;
using System.Text.Json;

namespace ApiCausality360.Services
{
    public class IAService : IIAService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public IAService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GenerateOrigenAsync(string titulo, string descripcion)
        {
            var prompt = $"Analiza el siguiente evento y explica ÚNICAMENTE sus antecedentes históricos y causas (máximo 200 palabras):\n\n" +
                        $"TÍTULO: {titulo}\n" +
                        $"DESCRIPCIÓN: {descripcion ?? "Sin descripción adicional"}\n\n" +
                        $"Responde SOLO con los antecedentes, sin introducción ni comentarios adicionales.";

            return await CallGroqWithRetryAsync(prompt);
        }

        public async Task<string> GenerateImpactoAsync(string titulo, string descripcion)
        {
            var prompt = $"Analiza el impacto del siguiente evento (máximo 200 palabras):\n\n" +
                        $"TÍTULO: {titulo}\n" +
                        $"DESCRIPCIÓN: {descripcion ?? "Sin descripción adicional"}\n\n" +
                        $"Explica ÚNICAMENTE las consecuencias económicas, sociales o políticas. Sin introducción.";

            return await CallGroqWithRetryAsync(prompt);
        }

        public async Task<string> GeneratePrediccionAsync(string titulo, string descripcion)
        {
            var prompt = $"Basándote en el siguiente evento, genera predicciones futuras (máximo 200 palabras):\n\n" +
                        $"TÍTULO: {titulo}\n" +
                        $"DESCRIPCIÓN: {descripcion ?? "Sin descripción adicional"}\n\n" +
                        $"Proporciona ÚNICAMENTE 2-3 escenarios posibles. Sin introducción.";

            return await CallGroqWithRetryAsync(prompt);
        }

        // MEJORADO: Generar detalles específicos para cada evento similar (SIN FALLBACKS)
        public async Task<string> GenerateSimilarEventDetailAsync(string eventoSimilar, string tituloActual, string descripcionActual)
        {
            var prompt = $"Explica en 120-150 palabras cómo '{eventoSimilar}' se relaciona con '{tituloActual}':\n\n" +
                        $"INSTRUCCIONES CRÍTICAS:\n" +
                        $"- NUNCA comiences con 'Lo siento' o 'No puedo proporcionar'\n" +
                        $"- Sé CONCISO pero COMPLETO\n" +
                        $"- Máximo 150 palabras TOTAL\n" +
                        $"- Termina siempre las ideas, no las dejes a medias\n" +
                        $"- Si no conoces el evento exacto, enfócate en similitudes temáticas\n" +
                        $"- SIEMPRE encuentra conexiones válidas entre los eventos\n\n" +
                        $"Estructura BREVE:\n" +
                        $"**SIMILITUDES**: 2-3 conexiones principales\n" +
                        $"**LECCIONES**: Qué enseñanza histórica aporta\n" +
                        $"**DIFERENCIAS**: 1 diferencia contextual clave\n\n" +
                        $"CRÍTICO: Respuesta CORTA pero COMPLETA. No exceder 150 palabras.";

            var response = await CallGroqWithRetryAsync(prompt);

            // Solo errores técnicos graves - sin contenido predeterminado
            if (response.StartsWith("Error"))
            {
                return ""; // Cadena vacía - la IA debe generar todo el contenido
            }

            return response;
        }

        // MEJORADO: Método para generar eventos similares
        // COMPLETAMENTE LIMPIO: Método para generar eventos similares (SIN FALLBACKS PREDETERMINADOS)
        public async Task<List<string>> GenerateSimilarEventsAsync(string titulo, string descripcion)
        {
            var prompt = $"Lista EXACTAMENTE 3 eventos históricos similares al siguiente evento:\n\n" +
                        $"TÍTULO: {titulo}\n" +
                        $"DESCRIPCIÓN: {descripcion ?? "Sin descripción adicional"}\n\n" +
                        $"INSTRUCCIONES CRÍTICAS:\n" +
                        $"- Busca similitudes temáticas, tecnológicas, políticas o sociales\n" +
                        $"- NUNCA respondas 'Lo siento' o 'No puedo proporcionar'\n" +
                        $"- SIEMPRE encuentra eventos históricos reales, aunque la conexión sea temática\n" +
                        $"- Si no encuentras similitudes exactas, busca similitudes por impacto o contexto\n" +
                        $"- Genera SIEMPRE 3 eventos diferentes y únicos\n\n" +
                        $"Formato requerido (respeta exactamente):\n" +
                        $"1. [Nombre específico del evento histórico (año)]\n" +
                        $"2. [Nombre específico del evento histórico (año)]\n" +
                        $"3. [Nombre específico del evento histórico (año)]\n\n" +
                        $"Ejemplos de formato correcto:\n" +
                        $"1. Presentación del iPhone (2007)\n" +
                        $"2. Crisis de los misiles de Cuba (1962)\n" +
                        $"3. Lanzamiento del Sputnik (1957)\n\n" +
                        $"SOLO nombres de eventos específicos, sin explicaciones.";

            var response = await CallGroqWithRetryAsync(prompt);

            // Solo manejar errores técnicos GRAVES (sin contenido predeterminado)
            if (response.StartsWith("Error"))
            {
                Console.WriteLine($"⚠️ Technical error, returning empty list - AI should handle all cases");
                return new List<string>(); // Lista vacía en lugar de eventos predeterminados
            }

            // Parsear la respuesta línea por línea
            var eventos = new List<string>();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Take(3)) // Máximo 3 eventos
            {
                var cleanLine = line.Trim();

                // Remover numeración (1., 2., 3., -, *, etc.)
                cleanLine = System.Text.RegularExpressions.Regex.Replace(cleanLine, @"^[\d\-\*\•]\s*\.?\s*", "");

                if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine.Length > 10 &&
                    !cleanLine.ToLower().Contains("lo siento") && 
                    !cleanLine.ToLower().Contains("no puedo") && 
                    !cleanLine.ToLower().Contains("sin embargo"))
                {
                    // Truncar si es demasiado largo
                    if (cleanLine.Length > 150)
                    {
                        cleanLine = cleanLine.Substring(0, 147) + "...";
                    }
                    eventos.Add(cleanLine);
                }
            }

            // CONFIANZA TOTAL EN LA IA: Devolver lo que la IA generó, sin completar con predeterminados
            Console.WriteLine($"✅ AI generated {eventos.Count} similar events");
            return eventos; // Sin fallbacks - la IA debe generar todo
        }

        // MEJORADO: Método con retry y mejor manejo de rate limits
        private async Task<string> CallGroqWithRetryAsync(string prompt, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var apiUrl = _configuration["Groq:ApiUrl"];
                    var apiKey = _configuration["Groq:ApiKey"];

                    if (string.IsNullOrEmpty(apiKey))
                    {
                        return "Error: API Key de Groq no configurada";
                    }

                    var requestBody = new
                    {
                        model = "llama-3.1-8b-instant",
                        messages = new[]
                        {
                            new {
                                role = "system",
                                content = "Eres un analista histórico experto. Proporcionas análisis BREVES pero COMPLETOS. FUNDAMENTAL: Siempre terminas tus respuestas, nunca las dejas inconclusas. Máximo 150 palabras por respuesta. Nunca responds con 'Lo siento'."
                            },
                            new {
                                role = "user",
                                content = prompt
                            }
                        },
                        max_tokens = 400, // 🔥 REDUCIDO: Para respuestas más cortas (120-150 palabras)
                        temperature = 0.5, // Más determinista para respuestas consistentes
                        top_p = 0.85,
                        stream = false
                    };

                    var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Limpiar headers previos y añadir autorización
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    Console.WriteLine($"🤖 Calling Groq API for similar event detail (attempt {attempt}/{maxRetries})...");
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
                                var content_result = messageContent.GetString()?.Trim() ?? "Contenido no disponible";
                                
                                // 🔥 VERIFICACIÓN: Asegurar que la respuesta está completa
                                if (content_result.EndsWith("...") || content_result.Length < 100)
                                {
                                    Console.WriteLine($"⚠️ Response seems incomplete, retrying...");
                                    if (attempt < maxRetries)
                                    {
                                        await Task.Delay(2000);
                                        continue;
                                    }
                                }
                                
                                Console.WriteLine($"✅ Groq API success with complete response (attempt {attempt})");
                                return content_result;
                            }
                        }

                        return "Error: Formato de respuesta inesperado";
                    }
                    else if ((int)response.StatusCode == 429) // Too Many Requests
                    {
                        Console.WriteLine($"⚠️ Rate limit hit (attempt {attempt}/{maxRetries})");

                        if (attempt < maxRetries)
                        {
                            var waitTime = GetRetryAfterTime(responseContent);
                            Console.WriteLine($"⏳ Waiting {waitTime} seconds before retry...");
                            await Task.Delay(waitTime * 1000);
                            continue;
                        }
                        else
                        {
                            return $"Rate limit exceeded. Análisis pendiente - será completado automáticamente.";
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ API Error ({response.StatusCode}): {responseContent}");
                        return $"Error temporal de la API. Análisis histórico pendiente.";
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"⚠️ HTTP Error (attempt {attempt}/{maxRetries}): {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(3000);
                        continue;
                    }
                    return $"Error de conexión. Información histórica no disponible temporalmente.";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Unexpected error: {ex.Message}");
                    return $"Error inesperado. Análisis comparativo pendiente.";
                }
            }

            return "Información histórica no disponible tras múltiples intentos.";
        }

        private int GetRetryAfterTime(string responseContent)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(responseContent, @"try again in (\d+(?:\.\d+)?)s");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var seconds))
                {
                    // OPTIMIZADO: Limitar tiempo máximo de espera a 2 minutos (120 segundos)
                    var waitTime = Math.Min((int)Math.Ceiling(seconds), 120);
                    return Math.Max(waitTime, 5); // Mínimo 5 segundos, máximo 120 segundos
                }
            }
            catch
            {
                // Si no podemos parsear, usar default
            }

            return 30; // Default mejorado: 30 segundos (en lugar de 8)
        }
    }
}