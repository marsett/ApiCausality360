# ApiCausality360 - API de Análisis Inteligente de Noticias

## 🚀 Descripción

API RESTful desarrollada con .NET 9.0 que proporciona análisis automatizado de noticias españolas mediante inteligencia artificial. Procesa diariamente 5 fuentes RSS, generando análisis histórico, impacto y predicciones fundamentadas con IA.

🔗 **[Documentación Interactiva](https://apicausality360.azurewebsites.net/scalar/v1)**

## 🛠️ Tecnologías Utilizadas

- **Framework:** .NET 9.0 / ASP.NET Core
- **Lenguajes:** C#
- **Base de Datos:** SQL Server + Entity Framework Core
- **IA:** Groq API (Llama 3.1)
- **Cloud:** Azure (Key Vault, App Service)
- **Documentación:** Scalar OpenAPI

## ✨ Características

- ✅ **Procesamiento automático diario** de 5 fuentes RSS españolas
- ✅ **Análisis IA completo** con origen histórico, impacto y predicciones
- ✅ **Eventos similares históricos** identificados automáticamente
- ✅ **Categorización inteligente** (Política, Economía, Tecnología, Social, Internacional)
- ✅ **Sistema de caché avanzado** con TTL inteligente
- ✅ **Rate limiting** y middleware de seguridad
- ✅ **Background service** para procesamiento programado
- ✅ **Deduplicación automática** de eventos similares
- ✅ **Extracción de imágenes** desde contenido RSS
- ✅ **Gestión segura de secretos** con Azure Key Vault
- ✅ **Sistema de monitoreo** con UptimeRobot para mantener la API activa

## 📱 Funcionalidades Principales

### 🤖 Motor de IA
Análisis automático usando Groq API que genera contexto histórico, evaluación de impacto y predicciones futuras, además de identificar eventos similares de la historia.

### 📰 Procesamiento de Noticias
Recopilación diaria automática desde 5 medios españoles con categorización inteligente y extracción de contenido multimedia.

### ⚡ Sistema de Caché
Caché inteligente con TTL adaptativo que optimiza el rendimiento y reduce llamadas a servicios externos.

### 🔒 Seguridad Empresarial
Gestión de secretos con Azure Key Vault, rate limiting configurable y CORS optimizado para producción.

### 📡 Monitoreo Continuo
Sistema de ping con UptimeRobot que mantiene la aplicación Azure siempre activa, evitando el "cold start" y asegurando respuesta inmediata.

## 🏗️ Estructura del Proyecto

```
ApiCausality360/
├── Controllers/
│   └── EventsController.cs
├── Services/
│   ├── EventService.cs
│   ├── IAService.cs
│   ├── NewsService.cs
│   ├── CacheService.cs
│   └── NewsSchedulerService.cs
├── Models/
│   ├── Event.cs
│   ├── Category.cs
│   └── SimilarEvent.cs
├── DTOs/
│   ├── EventDto.cs
│   ├── CreateEventDto.cs
│   └── NewsItem.cs
├── Data/
│   └── CausalityContext.cs
├── Middleware/
│   └── RateLimitingMiddleware.cs
├── Migrations/
└── Program.cs
```

## 🌐 Endpoints Principales

La API proporciona endpoints RESTful para la gestión completa de eventos y análisis de noticias:

- **GET /api/events/recent** - Eventos más recientes del día
- **GET /api/events/{id}** - Detalle completo de evento
- **POST /api/events/process-today-news** - Procesar noticias actuales
- **POST /api/events/generate-with-ai** - Crear evento con análisis IA
- **GET /api/events/by-category/{category}** - Filtrar por categoría
- **GET /api/events/ping** - Endpoint de monitoreo para UptimeRobot

## 🤖 Motor de IA (Groq Integration)

La API utiliza **Groq API** con el modelo **Llama 3.1** para generar:

### Análisis Histórico
- Investigación de antecedentes y causas
- Contexto geopolítico relevante
- Conexiones con eventos pasados

### Evaluación de Impacto
- Consecuencias económicas proyectadas
- Impacto social y político
- Efectos a corto y largo plazo

### Predicciones Inteligentes
- Escenarios futuros posibles
- Análisis de tendencias
- Proyecciones basadas en datos históricos

### Eventos Similares
- Identificación automática de eventos históricos relacionados
- Análisis comparativo detallado
- Lecciones históricas aplicables

## 📊 Fuentes de Datos

**Medios RSS Procesados:**
- La Vanguardia
- OK Diario  
- El Español
- El Mundo
- 20 Minutos

**Procesamiento:** Diario automático a las 12:00 con análisis completo de IA

### Variables de Entorno
```json
{
  "KeyVault": {
    "VaultUri": "https://keyvaultcausality.vault.azure.net/"
  },
  "Groq": {
    "ApiUrl": "https://api.groq.com/openai/v1/chat/completions"
  }
}
```

**Secretos en Azure Key Vault:**
- Cadena de conexión SQL Server
- Groq API Key para servicios IA

## 📈 Rendimiento y Optimización

- ⚡ **Sistema de caché inteligente** con TTL adaptativo
- 🔄 **Background processing** para operaciones pesadas
- 🛡️ **Rate limiting** configurable por endpoint
- 📊 **Procesamiento en lotes** optimizado
- 🎯 **Deduplicación automática** de contenido
- 📡 **Monitoreo UptimeRobot** para disponibilidad 24/7

## 🔄 Actualizaciones Recientes

**v1.0.0** (2025) - Lanzamiento inicial
- Motor de IA completo con Groq integration
- Procesamiento automático de 5 fuentes RSS
- Sistema de caché avanzado implementado
- Background service para análisis programado
- Middleware de rate limiting y seguridad
- Documentación interactiva con Scalar
- Integración completa con Azure Key Vault
- Categorización automática inteligente
- Sistema de eventos similares históricos
- Endpoint de monitoreo con UptimeRobot para disponibilidad continua

---

## 📄 Licencia

Este proyecto está disponible para visualización y evaluación profesional. Ver el archivo [LICENSE](LICENSE) para más detalles sobre términos de uso y restricciones.