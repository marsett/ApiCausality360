using ApiCausality360.Models;

namespace ApiCausality360.Services
{
    public interface IEventService
    {
        Task<List<Event>> GetEventsByDateAsync(DateTime date);
        Task<Event?> GetEventByIdAsync(Guid id);
        Task<Event> CreateEventAsync(Event eventItem);
        Task<Event> CreateEventAsync(Event eventItem, List<string> categoryNames);
        Task<Event> UpdateEventAsync(Event eventItem);
        Task<bool> DeleteEventAsync(Guid id);
        Task<List<Event>> GetRecentEventsAsync(int count = 10);
        Task<List<Event>> GetTodayEventsOrProcessAsync(int count = 5); // NUEVO

        // NUEVO: Para filtrar por categoría
        Task<List<Event>> GetEventsByCategoryAsync(string categoryName, int count = 10);
    }
}