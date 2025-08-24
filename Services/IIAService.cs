namespace ApiCausality360.Services
{
    public interface IIAService
    {
        Task<string> GenerateOrigenAsync(string titulo, string descripcion);
        Task<string> GenerateImpactoAsync(string titulo, string descripcion);
        Task<string> GeneratePrediccionAsync(string titulo, string descripcion);
        Task<List<string>> GenerateSimilarEventsAsync(string titulo, string descripcion);
        
        // NUEVO: Para generar detalles específicos de cada evento similar
        Task<string> GenerateSimilarEventDetailAsync(string eventoSimilar, string tituloActual, string descripcionActual);
    }
}
