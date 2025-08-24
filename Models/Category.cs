﻿using System.ComponentModel.DataAnnotations;

namespace ApiCausality360.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Navegación
        public ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();
    }
}
