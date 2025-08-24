using System.ComponentModel.DataAnnotations;

namespace ApiCausality360.Models
{
    public class SimilarEvent
    {
        public int Id { get; set; }

        public Guid EventId { get; set; }
        public Event Event { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Evento { get; set; } = string.Empty;

        public string? Detalle { get; set; }
    }
}
