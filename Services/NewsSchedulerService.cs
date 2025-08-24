using ApiCausality360.Services;

namespace ApiCausality360.Services
{
    public class NewsSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NewsSchedulerService> _logger;

        public NewsSchedulerService(IServiceProvider serviceProvider, ILogger<NewsSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var targetTime = new DateTime(now.Year, now.Month, now.Day, 12, 00, 0); // 12:00 AM procesamiento diario

                    // Si ya pasó la hora de hoy, programa para mañana
                    if (now > targetTime)
                    {
                        targetTime = targetTime.AddDays(1);
                    }

                    var delay = targetTime - now;
                    _logger.LogInformation($"📅 Próximo procesamiento automático de noticias programado para: {targetTime:dd/MM/yyyy HH:mm}");

                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ProcessDailyNews();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error en NewsSchedulerService: {ex.Message}");
                    // Esperar 1 hora antes de reintentar si hay error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task ProcessDailyNews()
        {
            using var scope = _serviceProvider.CreateScope();
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
            var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
            var iaService = scope.ServiceProvider.GetRequiredService<IIAService>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            try
            {
                var today = DateTime.Today;
                var startTime = DateTime.Now;
                _logger.LogInformation($"🚀 [SCHEDULER] Iniciando procesamiento automático de noticias para {today:dd/MM/yyyy} a las {startTime:HH:mm:ss}");

                // Verificar si ya existen eventos del día
                var existingEvents = await eventService.GetEventsByDateAsync(today);
                if (existingEvents.Any())
                {
                    _logger.LogInformation($"✅ [SCHEDULER] Ya existen {existingEvents.Count} eventos para {today:dd/MM/yyyy}. Cancelando procesamiento.");
                    return;
                }

                _logger.LogInformation($"📰 [SCHEDULER] No hay eventos para hoy. Obteniendo noticias...");

                // Procesar noticias
                var news = await newsService.GetTodayNewsAsync("es", 5);
                
                if (!news.Any())
                {
                    _logger.LogWarning($"⚠️ [SCHEDULER] No se encontraron noticias para {today:dd/MM/yyyy}");
                    return;
                }

                _logger.LogInformation($"📊 [SCHEDULER] Procesando {news.Count} noticias automáticamente de {news.Select(n => n.Source).Distinct().Count()} fuentes diferentes");

                var createdEvents = new List<ApiCausality360.Models.Event>();
                var processedCount = 0;

                foreach (var newsItem in news)
                {
                    try
                    {
                        processedCount++;
                        _logger.LogInformation($"🔄 [SCHEDULER] [{processedCount}/{news.Count}] Procesando: {newsItem.Title.Substring(0, Math.Min(60, newsItem.Title.Length))}... de {newsItem.Source}");

                        var currentTime = DateTime.Now;
                        var eventItem = new ApiCausality360.Models.Event
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

                        // Generar contenido con IA con tiempos optimizados para background
                        _logger.LogInformation($"🤖 [SCHEDULER] Generando análisis de origen...");
                        eventItem.Origen = await iaService.GenerateOrigenAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2500); // Menos agresivo en background

                        _logger.LogInformation($"🤖 [SCHEDULER] Generando análisis de impacto...");
                        eventItem.Impacto = await iaService.GenerateImpactoAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2500);

                        _logger.LogInformation($"🤖 [SCHEDULER] Generando predicción IA...");
                        eventItem.PrediccionIA = await iaService.GeneratePrediccionAsync(eventItem.Titulo, eventItem.Descripcion);
                        await Task.Delay(2500);

                        // Generar eventos similares
                        _logger.LogInformation($"🔍 [SCHEDULER] Generando eventos similares...");
                        var similarEvents = await iaService.GenerateSimilarEventsAsync(eventItem.Titulo, eventItem.Descripcion);
                        var addedSimilar = 0;
                        
                        foreach (var similar in similarEvents.Take(3))
                        {
                            if (!string.IsNullOrWhiteSpace(similar) && !similar.StartsWith("Error"))
                            {
                                var detalle = await iaService.GenerateSimilarEventDetailAsync(similar, eventItem.Titulo, eventItem.Descripcion);
                                await Task.Delay(2000);

                                eventItem.SimilarEvents.Add(new ApiCausality360.Models.SimilarEvent
                                {
                                    Evento = similar.Length > 200 ? similar.Substring(0, 197) + "..." : similar,
                                    Detalle = detalle // 🔥 SIN TRUNCAMIENTO: Permitir respuesta completa
                                });
                                addedSimilar++;
                            }
                        }

                        _logger.LogInformation($"📖 [SCHEDULER] Agregados {addedSimilar} eventos similares");

                        var categories = new List<string> { newsItem.Category };
                        var savedEvent = await eventService.CreateEventAsync(eventItem, categories);
                        createdEvents.Add(savedEvent);

                        _logger.LogInformation($"✅ [SCHEDULER] [{processedCount}/{news.Count}] Guardado: {savedEvent.Titulo.Substring(0, Math.Min(50, savedEvent.Titulo.Length))}... (Categoría: {newsItem.Category})");
                        
                        // Pausa entre eventos para no sobrecargar
                        if (processedCount < news.Count)
                        {
                            await Task.Delay(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ [SCHEDULER] Error procesando noticia #{processedCount}: {ex.Message}");
                        
                        // Continuar con la siguiente noticia
                        continue;
                    }
                }

                // Limpiar cachés relacionados
                var cacheKeys = new[]
                {
                    $"recent_events_{today:yyyy-MM-dd}_5",
                    $"daily_events_{today:yyyy-MM-dd}",
                    $"today_events_{today:yyyy-MM-dd}"
                };

                foreach (var key in cacheKeys)
                {
                    cacheService.Remove(key);
                }

                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                _logger.LogInformation($"🎯 [SCHEDULER] COMPLETADO - Procesamiento automático finalizado:");
                _logger.LogInformation($"   📊 Eventos creados: {createdEvents.Count}/{news.Count}");
                _logger.LogInformation($"   ⏱️ Tiempo total: {duration.TotalMinutes:F1} minutos");
                _logger.LogInformation($"   📅 Fecha: {today:dd/MM/yyyy}");
                _logger.LogInformation($"   🕐 Finalizado a las: {endTime:HH:mm:ss}");
                _logger.LogInformation($"   📱 Los usuarios ahora tendrán noticias listas instantáneamente!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [SCHEDULER] Error crítico en procesamiento automático diario: {ex.Message}");
                _logger.LogError($"🔧 [SCHEDULER] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
