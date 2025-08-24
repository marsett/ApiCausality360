using ApiCausality360.Data;
using ApiCausality360.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiCausality360.Services
{
    public class EventService : IEventService
    {
        private readonly CausalityContext _context;

        public EventService(CausalityContext context)
        {
            _context = context;
        }

        public async Task<List<Event>> GetEventsByDateAsync(DateTime date)
        {
            return await _context.Events
                .Where(e => e.Fecha.Date == date.Date)
                .Include(e => e.EventCategories)
                    .ThenInclude(ec => ec.Category)
                .Include(e => e.SimilarEvents)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<Event?> GetEventByIdAsync(Guid id)
        {
            return await _context.Events
                .Include(e => e.EventCategories)
                    .ThenInclude(ec => ec.Category)
                .Include(e => e.SimilarEvents)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Event> CreateEventAsync(Event eventItem)
        {
            _context.Events.Add(eventItem);
            await _context.SaveChangesAsync();
            return eventItem;
        }

        public async Task<Event> CreateEventAsync(Event eventItem, List<string> categoryNames)
        {
            _context.Events.Add(eventItem);
            await _context.SaveChangesAsync();

            // Asociar categorías
            foreach (var categoryName in categoryNames)
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name == categoryName);

                if (category != null)
                {
                    _context.EventCategories.Add(new EventCategory
                    {
                        EventId = eventItem.Id,
                        CategoryId = category.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            return eventItem;
        }

        public async Task<Event> UpdateEventAsync(Event eventItem)
        {
            _context.Events.Update(eventItem);
            await _context.SaveChangesAsync();
            return eventItem;
        }

        public async Task<bool> DeleteEventAsync(Guid id)
        {
            var eventItem = await _context.Events.FindAsync(id);
            if (eventItem == null)
                return false;

            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Event>> GetRecentEventsAsync(int count = 10)
        {
            return await _context.Events
                .Include(e => e.EventCategories)
                    .ThenInclude(ec => ec.Category)
                .Include(e => e.SimilarEvents)
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        // NUEVO: Método inteligente que verifica eventos del día antes de devolver recientes
        public async Task<List<Event>> GetTodayEventsOrProcessAsync(int count = 5)
        {
            var today = DateTime.Today;
            
            // Primero verificar si hay eventos del día actual
            var todayEvents = await _context.Events
                .Where(e => e.Fecha.Date == today.Date)
                .Include(e => e.EventCategories)
                    .ThenInclude(ec => ec.Category)
                .Include(e => e.SimilarEvents)
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToListAsync();

            // Si hay eventos del día actual, devolverlos
            if (todayEvents.Any())
            {
                return todayEvents;
            }

            // Si no hay eventos del día actual, devolver los más recientes
            // (El procesamiento de noticias se manejará en el controlador)
            return await GetRecentEventsAsync(count);
        }

        // NUEVO: Método para obtener eventos por categoría
        public async Task<List<Event>> GetEventsByCategoryAsync(string categoryName, int count = 10)
        {
            return await _context.Events
                .Where(e => e.EventCategories.Any(ec =>
                    ec.Category.Name.ToLower() == categoryName.ToLower()))
                .Include(e => e.EventCategories)
                    .ThenInclude(ec => ec.Category)
                .Include(e => e.SimilarEvents)
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}