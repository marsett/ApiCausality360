using AutoMapper;
using ApiCausality360.Models;
using ApiCausality360.DTOs;

namespace ApiCausality360.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Event, EventDto>()
                .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => 
                    src.EventCategories.Select(ec => ec.Category.Name).ToList()))
                .ForMember(dest => dest.TimeAgo, opt => opt.MapFrom(src => 
                    DateTime.Now - src.CreatedAt));

            CreateMap<CreateEventDto, Event>();
            
            CreateMap<SimilarEvent, SimilarEventDto>();
            
            // NUEVO: Mapeo para incluir imágenes
            CreateMap<NewsItem, Event>()
                .ForMember(dest => dest.Titulo, opt => opt.MapFrom(src => src.Title))
                .ForMember(dest => dest.Descripcion, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Fuentes, opt => opt.MapFrom(src => src.Url))
                .ForMember(dest => dest.SourceName, opt => opt.MapFrom(src => src.Source))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl))
                .ForMember(dest => dest.Fecha, opt => opt.MapFrom(src => src.PublishedAt))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
        }
    }
}