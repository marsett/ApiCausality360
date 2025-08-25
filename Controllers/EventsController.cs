using ApiCausality360.DTOs;
using ApiCausality360.Models;
using ApiCausality360.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace ApiCausality360.Controllers
{
    /// <summary>
    /// Controlador para gestión de eventos geopolíticos con análisis de IA
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IIAService _iaService;
        private readonly IMapper _mapper;
        private readonly INewsService _newsService;
        private readonly ICacheService _cacheService; // NUEVO
        private readonly ILogger<EventsController> _logger; // NUEVO

        public EventsController(IEventService eventService, IIAService iaService, IMapper mapper,
            INewsService newsService, ICacheService cacheService, ILogger<EventsController> logger)
        {
            _eventService = eventService;
            _iaService = iaService;
            _mapper = mapper;
            _newsService = newsService;
            _cacheService = cacheService;
            _logger = logger;
        }

        //[HttpGet("today")]
        //public async Task<ActionResult<List<EventDto>>> GetTodayEvents()
        //{
        //    var today = DateTime.Today;
        //    var cacheKey = $"today_events_{today:yyyy-MM-dd}";

        //    // Verificar caché primero
        //    if (_cacheService.TryGet<List<EventDto>>(cacheKey, out var cachedEvents))
        //    {
        //        _logger.LogInformation($"📦 Returning {cachedEvents.Count} cached events for {today:dd/MM/yyyy}");
        //        return Ok(cachedEvents);
        //    }

        //    // Si no hay caché, obtener de BD
        //    var events = await _eventService.GetEventsByDateAsync(today);
        //    var eventDtos = _mapper.Map<List<EventDto>>(events);

        //    // Cachear por 12 horas
        //    _cacheService.Set(cacheKey, eventDtos, TimeSpan.FromHours(12));

        //    _logger.LogInformation($"🗃️ Loaded {eventDtos.Count} events from DB for {today:dd/MM/yyyy}");
        //    return Ok(eventDtos);
        //}

        /// <summary>
        /// Obtiene los eventos más recientes del día actual
        /// </summary>
        /// <param name="count">Número máximo de eventos a retornar (default: 5)</param>
        /// <returns>Lista de eventos con análisis de IA completo</returns>
        /// <response code="200">Eventos recuperados exitosamente</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<EventDto>>> GetRecentEvents([FromQuery] int count = 5)
        {
            var today = DateTime.Today;
            var cacheKey = $"recent_events_{today:yyyy-MM-dd}_{count}";

            // Verificar caché primero
            if (_cacheService.TryGet<List<EventDto>>(cacheKey, out var cachedEvents))
            {
                _logger.LogInformation($"📦 [RECENT] Returning {cachedEvents.Count} cached recent events");
                return Ok(cachedEvents);
            }

            // Obtener eventos del día actual
            var todayEvents = await _eventService.GetEventsByDateAsync(today);
            
            if (todayEvents.Any())
            {
                // Si hay eventos del día actual, devolverlos (Background Service funcionó)
                _logger.LogInformation($"✅ [RECENT] Found {todayEvents.Count} events for today {today:dd/MM/yyyy} (Background Service working!)");
                var eventDtos = _mapper.Map<List<EventDto>>(todayEvents.Take(count));
                
                // Cachear por 6 horas
                _cacheService.Set(cacheKey, eventDtos, TimeSpan.FromHours(6));
                return Ok(eventDtos);
            }

            // Fallback: Si el Background Service no ha ejecutado aún o falló
            _logger.LogWarning($"⚠️ [RECENT] No events found for today {today:dd/MM/yyyy}. Background Service may not have run yet.");
            
            // 🔥 CORREGIR: Usar zona horaria Madrid consistentemente
            var madridTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            var nowMadrid = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, madridTimeZone);
            var currentHour = nowMadrid.Hour;
            var currentMinute = nowMadrid.Minute;
            var currentTime = currentHour * 60 + currentMinute;
            var scheduledTime = 12 * 60 + 0; // 12:00 en minutos desde medianoche MADRID
            
            // Tiempo de espera antes/después del scheduler (±10 minutos)
            var bufferMinutes = 10;
            
            if (Math.Abs(currentTime - scheduledTime) <= bufferMinutes)
            {
                // Estamos cerca de la hora del scheduler (±10 min)
                _logger.LogInformation($"⏳ [RECENT] Scheduler window detected (±10min from 12:00). Returning previous events to avoid conflicts.");
                
                var recentEvents = await _eventService.GetRecentEventsAsync(count);
                var recentDtos = _mapper.Map<List<EventDto>>(recentEvents);
                
                // Agregar mensaje informativo
                foreach (var dto in recentDtos)
                {
                    if (string.IsNullOrEmpty(dto.Descripcion))
                    {
                        dto.Descripcion = "📰 Las noticias del día se están procesando automáticamente. Actualiza en unos minutos.";
                    }
                }
                
                return Ok(recentDtos);
            }
            else if (currentTime < scheduledTime)
            {
                // Es antes del scheduler
                _logger.LogInformation($"🌅 [RECENT] Before scheduler time ({nowMadrid:HH:mm} < 12:00). Returning recent events from previous days.");
                
                var recentEvents = await _eventService.GetRecentEventsAsync(count);
                var recentDtos = _mapper.Map<List<EventDto>>(recentEvents);
                return Ok(recentDtos);
            }
            else
            {
                // Ya pasó suficiente tiempo después del scheduler, algo falló
                _logger.LogWarning($"🚨 [RECENT] Scheduler should have completed by now ({nowMadrid:HH:mm}). Triggering emergency fallback...");
                
                try
                {
                    // Fallback de emergencia: procesar automáticamente
                    var processedEvents = await ProcessTodayNewsInternal();
                    var resultDtos = _mapper.Map<List<EventDto>>(processedEvents.Take(count));
                    
                    // Cachear los resultados
                    _cacheService.Set(cacheKey, resultDtos, TimeSpan.FromHours(6));
                    
                    _logger.LogInformation($"✅ [RECENT] Emergency fallback completed: {resultDtos.Count} events");
                    return Ok(resultDtos);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ [RECENT] Emergency fallback failed: {ex.Message}");
                    
                    // Último fallback: devolver eventos recientes de días anteriores
                    var fallbackEvents = await _eventService.GetRecentEventsAsync(count);
                    var fallbackDtos = _mapper.Map<List<EventDto>>(fallbackEvents);
                    
                    return Ok(fallbackDtos);
                }
            }
        }

        /// <summary>
        /// Obtiene un evento específico por su ID
        /// </summary>
        /// <param name="id">ID único del evento</param>
        /// <returns>Evento con análisis completo de IA</returns>
        /// <response code="200">Evento encontrado</response>
        /// <response code="404">Evento no encontrado</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventDto>> GetEventById(Guid id)
        {
            var eventItem = await _eventService.GetEventByIdAsync(id);
            if (eventItem == null)
                return NotFound($"Event with ID {id} not found.");
            return Ok(_mapper.Map<EventDto>(eventItem));
        }

        /// <summary>
        /// Procesa las noticias del día y genera eventos con análisis de IA
        /// </summary>
        /// <returns>Lista de eventos creados a partir de noticias actuales</returns>
        /// <response code="200">Noticias procesadas exitosamente</response>
        /// <response code="500">Error en el procesamiento</response>
        /// <remarks>
        /// Este endpoint:
        /// - Obtiene noticias de múltiples fuentes RSS españolas
        /// - Genera análisis histórico, impacto y predicciones usando IA
        /// - Identifica eventos similares de la historia
        /// - Categoriza automáticamente el contenido
        /// - Implementa sistema de caché inteligente
        /// </remarks>
        [HttpPost("process-today-news")]
        [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<EventDto>>> ProcessTodayNews()
        {
            var today = DateTime.Today;
            var cacheKey = $"daily_events_{today:yyyy-MM-dd}";

            // VERIFICAR CACHÉ PRIMERO
            if (_cacheService.TryGet<List<EventDto>>(cacheKey, out var cachedEvents))
            {
                _logger.LogInformation($"📦 Returning {cachedEvents.Count} cached events for {today:dd/MM/yyyy}");
                return Ok(cachedEvents);
            }

            // VERIFICAR BASE DE DATOS
            var existingEvents = await _eventService.GetEventsByDateAsync(today);
            if (existingEvents.Any())
            {
                _logger.LogInformation($"🗃️ Found {existingEvents.Count} existing events in DB for {today:dd/MM/yyyy}");
                var existingEventDtos = _mapper.Map<List<EventDto>>(existingEvents);

                // Cachear eventos existentes
                _cacheService.Set(cacheKey, existingEventDtos);
                return Ok(existingEventDtos);
            }

            // PROCESAR NUEVAS NOTICIAS
            _logger.LogInformation($"🚀 No events found for {today:dd/MM/yyyy}. Processing new news...");

            var news = await _newsService.GetTodayNewsAsync("es", 5);
            var createdEvents = new List<Event>();

            _logger.LogInformation($"📊 Processing {news.Count} diverse news from {news.Select(n => n.Source).Distinct().Count()} sources...");

            if (!news.Any())
            {
                _logger.LogWarning("❌ No relevant news found for today");
                return Ok(new List<EventDto>());
            }

            // Procesar en lotes para evitar rate limits
            var batchSize = 2;
            var batches = news.Select((item, index) => new { item, index })
                             .GroupBy(x => x.index / batchSize)
                             .Select(g => g.Select(x => x.item).ToList())
                             .ToList();

            _logger.LogInformation($"📦 Processing in {batches.Count} batches of {batchSize} articles each");

            foreach (var (batch, batchIndex) in batches.Select((b, i) => (b, i)))
            {
                _logger.LogInformation($"📦 Processing batch {batchIndex + 1}/{batches.Count}...");

                foreach (var newsItem in batch)
                {
                    try
                    {
                        // Verificar duplicados
                        var isDuplicate = existingEvents.Any(e =>
                            AreTitlesSimilar(e.Titulo, newsItem.Title));

                        if (isDuplicate)
                        {
                            _logger.LogWarning($"⚠️ Skipping duplicate: {newsItem.Title.Substring(0, Math.Min(50, newsItem.Title.Length))}...");
                            continue;
                        }

                        var currentTime = DateTime.Now;
                        var eventItem = new Event
                        {
                            Id = Guid.NewGuid(),
                            Titulo = newsItem.Title,
                            Descripcion = newsItem.Description,
                            Fecha = currentTime.Date,
                            Fuentes = newsItem.Url,
                            CreatedAt = currentTime,
                            UpdatedAt = currentTime,
                            // NUEVO: Incluir imagen y fuente
                            ImageUrl = newsItem.ImageUrl,
                            SourceName = newsItem.Source
                        };

                        _logger.LogInformation($"🔄 Processing: {newsItem.Title.Substring(0, Math.Min(60, newsItem.Title.Length))}... [Source: {newsItem.Source}, Category: {newsItem.Category}]");

                        try
                        {
                            // Delays optimizados para 5 noticias
                            _logger.LogInformation($"🤖 Generating Origen...");
                            eventItem.Origen = await _iaService.GenerateOrigenAsync(eventItem.Titulo, eventItem.Descripcion);
                            await Task.Delay(4000);

                            _logger.LogInformation($"🤖 Generating Impacto...");
                            eventItem.Impacto = await _iaService.GenerateImpactoAsync(eventItem.Titulo, eventItem.Descripcion);
                            await Task.Delay(4000);

                            _logger.LogInformation($"🤖 Generating Prediccion...");
                            eventItem.PrediccionIA = await _iaService.GeneratePrediccionAsync(eventItem.Titulo, eventItem.Descripcion);
                            await Task.Delay(4000);

                            // Generar eventos similares
                            _logger.LogInformation($"🔍 Generating similar events...");
                            var similarEvents = await _iaService.GenerateSimilarEventsAsync(eventItem.Titulo, eventItem.Descripcion);

                            foreach (var similar in similarEvents)
                            {
                                if (!string.IsNullOrWhiteSpace(similar) && !similar.StartsWith("Error"))
                                {
                                    _logger.LogInformation($"📖 Generating detail for: {similar.Substring(0, Math.Min(50, similar.Length))}...");

                                    var detalle = await _iaService.GenerateSimilarEventDetailAsync(similar, eventItem.Titulo, eventItem.Descripcion);
                                    await Task.Delay(3000);

                                    eventItem.SimilarEvents.Add(new SimilarEvent
                                    {
                                        Evento = similar.Length > 200 ? similar.Substring(0, 197) + "..." : similar,
                                        Detalle = detalle.Length > 1000 ? detalle.Substring(0, 997) + "..." : detalle // 🔥 SIN TRUNCAMIENTO: Permitir respuesta completa
                                    });
                                }
                            }

                            _logger.LogInformation($"📊 Added {eventItem.SimilarEvents.Count} similar events");
                            await Task.Delay(2000);

                            // Guardar evento
                            var categories = new List<string> { newsItem.Category };
                            var savedEvent = await _eventService.CreateEventAsync(eventItem, categories);
                            createdEvents.Add(savedEvent);

                            _logger.LogInformation($"✅ Saved: {savedEvent.Titulo.Substring(0, Math.Min(50, savedEvent.Titulo.Length))}... from {newsItem.Source}");

                            await Task.Delay(3000);
                        }
                        catch (Exception aiEx)
                        {
                            _logger.LogError($"❌ AI Error for {newsItem.Title.Substring(0, Math.Min(30, newsItem.Title.Length))}...: {aiEx.Message}");

                            if (aiEx.Message.Contains("Rate limit") || aiEx.Message.Contains("TooManyRequests"))
                            {
                                _logger.LogWarning($"⏳ Rate limit detected, using fallback content...");

                                eventItem.Origen = "Información de contexto no disponible temporalmente debido a limitaciones del servicio de IA.";
                                eventItem.Impacto = "Análisis de impacto pendiente. Se actualizará cuando el servicio esté disponible.";
                                eventItem.PrediccionIA = "Predicciones futuras no disponibles temporalmente.";
                            }

                            var categories = new List<string> { newsItem.Category };
                            var savedEvent = await _eventService.CreateEventAsync(eventItem, categories);
                            createdEvents.Add(savedEvent);

                            await Task.Delay(8000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error processing {newsItem.Title.Substring(0, Math.Min(30, newsItem.Title.Length))}...: {ex.Message}");
                    }
                }

                // Delay entre lotes
                if (batchIndex < batches.Count - 1)
                {
                    _logger.LogInformation($"⏳ Waiting 10 seconds before next batch...");
                    await Task.Delay(10000);
                }
            }

            var finalEventDtos = _mapper.Map<List<EventDto>>(createdEvents);

            // CACHEAR RESULTADOS
            _cacheService.Set(cacheKey, finalEventDtos);

            _logger.LogInformation($"🎯 Final result: {createdEvents.Count} events created from {news.Select(n => n.Source).Distinct().Count()} sources with {news.Select(n => n.Category).Distinct().Count()} different categories");

            return Ok(finalEventDtos);
        }

        /// <summary>
        /// Crea un nuevo evento con análisis de IA completo
        /// </summary>
        /// <param name="createEventDto">Datos del evento a crear</param>
        /// <returns>Evento creado con análisis de IA</returns>
        /// <response code="201">Evento creado exitosamente</response>
        /// <response code="400">Datos de entrada inválidos</response>
        [HttpPost("generate-with-ai")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EventDto>> CreateEventWithAI([FromBody] CreateEventDto createEventDto)
        {
            var eventItem = _mapper.Map<Event>(createEventDto);

            // Generar contenido con IA
            eventItem.Origen = await _iaService.GenerateOrigenAsync(eventItem.Titulo, eventItem.Descripcion);
            eventItem.Impacto = await _iaService.GenerateImpactoAsync(eventItem.Titulo, eventItem.Descripcion);
            eventItem.PrediccionIA = await _iaService.GeneratePrediccionAsync(eventItem.Titulo, eventItem.Descripcion);

            // MEJORADO: Generar eventos similares con detalles específicos
            var similarEvents = await _iaService.GenerateSimilarEventsAsync(eventItem.Titulo, eventItem.Descripcion);

            foreach (var similar in similarEvents)
            {
                if (!string.IsNullOrWhiteSpace(similar))
                {
                    var detalle = await _iaService.GenerateSimilarEventDetailAsync(similar, eventItem.Titulo, eventItem.Descripcion);

                    eventItem.SimilarEvents.Add(new SimilarEvent
                    {
                        Evento = similar.Length > 200 ? similar.Substring(0, 197) + "..." : similar,
                        Detalle = detalle.Length > 1000 ? detalle.Substring(0, 997) + "..." : detalle
                    });

                    await Task.Delay(2000);
                }
            }

            var savedEvent = await _eventService.CreateEventAsync(eventItem, createEventDto.Categories);
            return CreatedAtAction(nameof(GetEventById),
                new { id = savedEvent.Id },
                _mapper.Map<EventDto>(savedEvent));
        }

        /// <summary>
        /// Disparador manual del procesamiento automático (solo desarrollo)
        /// </summary>
        /// <returns>Resultado del procesamiento</returns>
        /// <response code="200">Procesamiento completado</response>
        /// <response code="403">No permitido en producción</response>
        [HttpPost("trigger-scheduler")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> TriggerScheduler()
        {
            if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid("Este endpoint solo está disponible en desarrollo");
            }

            try
            {
                _logger.LogInformation("🧪 [MANUAL] Disparando procesamiento manual desde endpoint...");
                
                var result = await ProcessTodayNewsInternal();
                
                return Ok(new
                {
                    success = true,
                    message = $"Procesamiento manual completado. {result.Count} eventos creados.",
                    eventsCreated = result.Count,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [MANUAL] Error en procesamiento manual: {ex.Message}");
                return BadRequest(new
                {
                    success = false,
                    message = $"Error en procesamiento manual: {ex.Message}",
                    timestamp = DateTime.Now
                });
            }
        }

        // NUEVO: Método privado para procesamiento automático de noticias
        private async Task<List<Event>> ProcessTodayNewsInternal()
        {
            var today = DateTime.Today;
            
            // Verificar una vez más si ya existen eventos (race condition)
            var existingEvents = await _eventService.GetEventsByDateAsync(today);
            if (existingEvents.Any())
            {
                return existingEvents;
            }

            _logger.LogInformation($"🚀 Auto-processing news for {today:dd/MM/yyyy}...");

            var news = await _newsService.GetTodayNewsAsync("es", 5);
            var createdEvents = new List<Event>();

            if (!news.Any())
            {
                _logger.LogWarning("❌ No relevant news found for auto-processing");
                return createdEvents;
            }

            _logger.LogInformation($"📊 Auto-processing {news.Count} news items...");

            // Procesar de forma más rápida (sin tanto delay entre llamadas)
            foreach (var newsItem in news)
            {
                try
                {
                    var currentTime = DateTime.Now;
                    var eventItem = new Event
                    {
                        Id = Guid.NewGuid(),
                        Titulo = newsItem.Title,
                        Descripcion = newsItem.Description,
                        Fecha = currentTime.Date,
                        Fuentes = newsItem.Url,
                        CreatedAt = currentTime,
                        UpdatedAt = currentTime,
                        ImageUrl = newsItem.ImageUrl,
                        SourceName = newsItem.Source
                    };

                    try
                    {
                        // Generar contenido IA con delays mínimos
                        eventItem.Origen = await _iaService.GenerateOrigenAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2000);

                        eventItem.Impacto = await _iaService.GenerateImpactoAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2000);

                        eventItem.PrediccionIA = await _iaService.GeneratePrediccionAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2000);

                        // Solo generar 2 eventos similares para ser más rápido
                        var similarEvents = await _iaService.GenerateSimilarEventsAsync(eventItem.Titulo, eventItem.Descripcion);
                        var limitedSimilar = similarEvents.Take(2);

                        foreach (var similar in limitedSimilar)
                        {
                            if (!string.IsNullOrWhiteSpace(similar) && !similar.StartsWith("Error"))
                            {
                                var detalle = await _iaService.GenerateSimilarEventDetailAsync(similar, eventItem.Titulo, eventItem.Descripcion);
                                await Task.Delay(1500);

                                eventItem.SimilarEvents.Add(new SimilarEvent
                                {
                                    Evento = similar.Length > 200 ? similar.Substring(0, 197) + "..." : similar,
                                    Detalle = detalle // 🔥 SIN TRUNCAMIENTO: Permitir respuesta completa
                                });
                            }
                        }

                        var categories = new List<string> { newsItem.Category };
                        var savedEvent = await _eventService.CreateEventAsync(eventItem, categories);
                        createdEvents.Add(savedEvent);

                        _logger.LogInformation($"✅ Auto-saved: {savedEvent.Titulo.Substring(0, Math.Min(50, savedEvent.Titulo.Length))}...");
                        await Task.Delay(2000);
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogWarning($"⚠️ AI processing failed for auto-processing, using fallback: {aiEx.Message}");

                        // Usar contenido de fallback
                        eventItem.Origen = "Análisis automático pendiente.";
                        eventItem.Impacto = "Evaluación de impacto en proceso.";
                        eventItem.PrediccionIA = "Predicciones disponibles próximamente.";

                        var categories = new List<string> { newsItem.Category };
                        var savedEvent = await _eventService.CreateEventAsync(eventItem, categories);
                        createdEvents.Add(savedEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error auto-processing news item: {ex.Message}");
                    continue;
                }
            }

            _logger.LogInformation($"🎯 Auto-processing completed: {createdEvents.Count} events created");
            return createdEvents;
        }

        private bool AreTitlesSimilar(string title1, string title2)
        {
            if (string.IsNullOrEmpty(title1) || string.IsNullOrEmpty(title2))
                return false;

            var normalized1 = NormalizeForComparison(title1);
            var normalized2 = NormalizeForComparison(title2);

            return normalized1.Contains(normalized2) ||
                   normalized2.Contains(normalized1) ||
                   CalculateSimilarity(normalized1, normalized2) > 0.7;
        }

        private string NormalizeForComparison(string text)
        {
            return text.ToLower()
                       .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                       .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                       .Replace(",", "").Replace(".", "").Replace(":", "")
                       .Replace("\"", "").Replace("'", "")
                       .Trim();
        }

        private double CalculateSimilarity(string text1, string text2)
        {
            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return union == 0 ? 0 : (double)intersection / union;
        }

        /// <summary>
        /// Obtiene eventos filtrados por categoría
        /// </summary>
        /// <param name="categoryName">Nombre de la categoría (Política, Economía, Tecnología, Social, Internacional)</param>
        /// <param name="count">Número máximo de eventos a retornar</param>
        /// <returns>Lista de eventos de la categoría especificada</returns>
        [HttpGet("by-category/{categoryName}")]
        [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<EventDto>>> GetEventsByCategory(string categoryName, [FromQuery] int count = 10)
        {
            var cacheKey = $"events_category_{categoryName.ToLower()}_{DateTime.Today:yyyy-MM-dd}";

            if (_cacheService.TryGet<List<EventDto>>(cacheKey, out var cachedEvents))
            {
                return Ok(cachedEvents);
            }

            var events = await _eventService.GetEventsByCategoryAsync(categoryName, count);
            var eventDtos = _mapper.Map<List<EventDto>>(events);

            _cacheService.Set(cacheKey, eventDtos, TimeSpan.FromHours(6));

            return Ok(eventDtos);
        }

        /// <summary>
        /// Endpoint para mantener la aplicación activa (UptimeRobot)
        /// </summary>
        [HttpGet("ping")]
        [HttpHead("ping")]  // ← AGREGAR esta línea
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult Ping()
        {
            var madridTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            var nowMadrid = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, madridTimeZone);
            
            return Ok(new
            {
                status = "alive",
                timestamp = nowMadrid.ToString("dd/MM/yyyy HH:mm:ss"),
                timezone = "Madrid",
                server = "West Europe",
                nextScheduledRun = "12:00 PM Madrid daily",
                message = "App is awake - Background Service active"
            });
        }
    }
}