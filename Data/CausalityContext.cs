using ApiCausality360.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiCausality360.Data
{
    public class CausalityContext: DbContext
    {
        public CausalityContext(DbContextOptions<CausalityContext> options) : base(options) { }
        public DbSet<Event> Events { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<EventCategory> EventCategories { get; set; }
        public DbSet<SimilarEvent> SimilarEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configurar clave compuesta para EventCategory
            modelBuilder.Entity<EventCategory>()
                .HasKey(ec => new { ec.EventId, ec.CategoryId });

            // Configurar relaciones
            modelBuilder.Entity<EventCategory>()
                .HasOne(ec => ec.Event)
                .WithMany(e => e.EventCategories)
                .HasForeignKey(ec => ec.EventId);

            modelBuilder.Entity<EventCategory>()
                .HasOne(ec => ec.Category)
                .WithMany(c => c.EventCategories)
                .HasForeignKey(ec => ec.CategoryId);

            modelBuilder.Entity<SimilarEvent>()
                .HasOne(se => se.Event)
                .WithMany(e => e.SimilarEvents)
                .HasForeignKey(se => se.EventId);

            // Seed data para categorías
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Política", Description = "Eventos políticos y gubernamentales" },
                new Category { Id = 2, Name = "Economía", Description = "Eventos económicos y financieros" },
                new Category { Id = 3, Name = "Tecnología", Description = "Avances y noticias tecnológicas" },
                new Category { Id = 4, Name = "Social", Description = "Eventos sociales y culturales" },
                new Category { Id = 5, Name = "Internacional", Description = "Eventos internacionales y geopolíticos" }
            );
        }
    }
}
