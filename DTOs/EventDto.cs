using System.ComponentModel.DataAnnotations;

namespace ApiCausality360.DTOs
{
    /// <summary>
    /// Evento geopolítico con análisis completo de IA
    /// </summary>
    public class EventDto
    {
        /// <summary>
        /// Identificador único del evento
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Título principal del evento
        /// </summary>
        [Required]
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Descripción detallada del evento
        /// </summary>
        public string? Descripcion { get; set; }

        /// <summary>
        /// Fecha del evento
        /// </summary>
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Análisis histórico y antecedentes generado por IA
        /// </summary>
        public string? Origen { get; set; }

        /// <summary>
        /// Análisis de impacto económico, social y político generado por IA
        /// </summary>
        public string? Impacto { get; set; }

        /// <summary>
        /// Predicciones futuras y escenarios posibles generados por IA
        /// </summary>
        public string? PrediccionIA { get; set; }

        /// <summary>
        /// URL de la fuente original de la noticia
        /// </summary>
        public string? Fuentes { get; set; }

        /// <summary>
        /// URL de la imagen asociada al evento
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Nombre de la fuente de información (ABC, El Mundo, etc.)
        /// </summary>
        public string? SourceName { get; set; }

        /// <summary>
        /// Categorías del evento (Política, Economía, Tecnología, Social, Internacional)
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Eventos históricos similares con análisis comparativo
        /// </summary>
        public List<SimilarEventDto> SimilarEvents { get; set; } = new();

        /// <summary>
        /// Fecha y hora de creación del evento
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Tiempo transcurrido desde la creación
        /// </summary>
        public TimeSpan TimeAgo => DateTime.Now - CreatedAt;
    }
}