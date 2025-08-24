using System.ComponentModel.DataAnnotations;

namespace ApiCausality360.DTOs
{
    public class CreateEventDto
    {
        [Required]
        [MaxLength(500)]
        public string Titulo { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Descripcion { get; set; } // NUEVO

        public DateTime Fecha { get; set; } = DateTime.Today;
        public List<string> Categories { get; set; } = new();
    }
}
