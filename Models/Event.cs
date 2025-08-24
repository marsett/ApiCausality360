using System.ComponentModel.DataAnnotations;

namespace ApiCausality360.Models
{
    public class Event
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Titulo { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        public DateTime Fecha { get; set; }

        // Campos para contenido generado por IA
        public string? Origen { get; set; }
        public string? Impacto { get; set; }
        public string? PrediccionIA { get; set; }

        [MaxLength(2000)]
        public string? Fuentes { get; set; }

        // NUEVO: Para el frontend
        [MaxLength(1000)]
        public string? ImageUrl { get; set; }

        [MaxLength(100)]
        public string? SourceName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navegación
        public ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();
        public ICollection<SimilarEvent> SimilarEvents { get; set; } = new List<SimilarEvent>();
    }
}