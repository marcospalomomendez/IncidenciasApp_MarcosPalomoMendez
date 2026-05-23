# IncidenciasApp — Marcos Palomo Méndez

Sistema inteligente de gestión de incidencias IT desarrollado como TFG de DAM. Permite a usuarios reportar incidencias, a técnicos gestionarlas y a administradores supervisar el sistema desde tres clientes distintos sobre una misma API REST, con clasificación y priorización automática por IA, sugerencia de soluciones y asistente de consultas en lenguaje natural.

---

## Tecnologías

| Capa | Stack |
|------|-------|
| **Backend** | ASP.NET Core 8, Entity Framework Core, PostgreSQL (Npgsql), JWT |
| **Tiempo real** | SignalR (WebSockets) |
| **Email** | MailKit + Mailtrap (sandbox) |
| **Web** | Razor Pages + Bootstrap 5 |
| **Desktop** | WPF (.NET 8) |
| **IA** | Groq API (LLaMA 3.1 8B) — clasificación, priorización, sugerencias y asistente |
| **Shared** | Librería de constantes compartida entre proyectos |
| **Tests** | xUnit + WebApplicationFactory |

---

## Roles

| Rol | Acceso |
|-----|--------|
| `Usuario` | Web — crea y consulta sus propias incidencias |
| `Tecnico` | Web + Desktop — gestiona incidencias asignadas, stats personales, suscripciones |
| `Admin` | Web + Desktop — panel completo: incidencias, usuarios, estadísticas, asistente |

---

## Funcionalidades principales

- **Gestión de incidencias** — CRUD completo con estados, prioridades, categorías y técnico asignado
- **Panel técnico** — stats personales: activas asignadas, resueltas esta semana, tiempo medio de resolución y % SLA cumplido
- **SLA automático** — cada prioridad tiene un límite de tiempo (Crítica 2h, Alta 8h, Media 24h, Baja 72h); las incidencias excedidas se marcan visualmente
- **Notificaciones en tiempo real** — SignalR emite eventos `NuevaIncidencia`, `CambioEstado`, `NuevoComentario` y `NuevaNotificacion` a los clientes conectados
- **Notificaciones email** — al asignar técnico y al cambiar estado; los administradores reciben aviso cuando una incidencia pasa a Resuelta
- **Seguimiento de incidencias** — un técnico puede suscribirse a cualquier incidencia (no solo las suyas) para recibir notificaciones de cambios de estado
- **Auditoría** — cada cambio de estado, técnico o creación queda registrado en la tabla `Auditoria` (quién, qué, cuándo)
- **Clasificación y priorización IA** — al crear una incidencia, Groq (LLaMA 3.1) asigna automáticamente categoría y prioridad; el usuario solo actúa como fallback
- **Sugerencia de solución IA** — al abrir el detalle de una incidencia (técnico o admin), la IA propone pasos de resolución basándose en el título, descripción y categoría
- **Asistente de consultas fijas** — panel admin con 8 preguntas predefinidas sobre el sistema (técnico más activo, SLA excedido, tiempo medio, categoría con más incidencias, etc.)
- **Asistente libre en lenguaje natural** — el admin puede formular cualquier pregunta sobre el sistema; la IA responde con contexto enriquecido (desglose por técnico, categorías, tiempos) y rechaza preguntas fuera del ámbito del sistema
- **Solución obligatoria al resolver** — al marcar una incidencia como Resuelta, técnico y admin deben describir la solución aplicada; se guarda automáticamente como comentario
- **Exportación** — informes en Excel (EPPlus) y PDF (QuestPDF) descargables desde el panel admin
- **Filtros avanzados** — por estado, categoría, prioridad, SLA, sin asignar, mis incidencias y búsqueda de texto libre

---

## Instalación y ejecución

### Requisitos

- .NET 8 SDK
- Docker Desktop (para PostgreSQL)
- Visual Studio 2022

### 1. Clonar el repositorio

```bash
git clone https://github.com/marcospalomomendez/IncidenciasApp_MarcosPalomoMendez.git
```

### 2. Levantar PostgreSQL con Docker

```bash
docker run --name postgres_asir -e POSTGRES_USER=alumno -e POSTGRES_PASSWORD=alumno123 \
  -e POSTGRES_DB=incidencias -p 5432:5432 -d postgres:16
```

### 3. Configurar la API

Crea `Api/appsettings.Development.json`:

```json
{
  "Jwt": { "Key": "TuClaveSecretaMuyLarga32Caracteres!" },
  "Groq": { "ApiKey": "tu_clave_groq_opcional" },
  "Email": {
    "Smtp": "sandbox.smtp.mailtrap.io",
    "Port": "587",
    "Usuario": "tu_usuario_mailtrap",
    "Password": "tu_password_mailtrap",
    "Remitente": "noreply@incidencias.local"
  }
}
```

### 4. Aplicar migraciones

```bash
cd Api
dotnet ef database update
```

### 5. Arrancar la API

```bash
cd Api
dotnet run
# Escucha en https://localhost:7085
```

### 6. Arrancar el cliente web

```bash
cd Web
dotnet run
# Escucha en https://localhost:7084
```

### 7. Arrancar el cliente desktop

Abrir `Desktop/Desktop.csproj` en Visual Studio y ejecutar con F5.

---

## Endpoints de la API

### Autenticación

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/Auth/registro` | Registrar nuevo usuario | Público |
| POST | `/api/Auth/login` | Iniciar sesión, devuelve JWT | Público |

### Incidencias

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| GET | `/api/Incidencias` | Listar todas (paginado, filtros) | Autenticado |
| GET | `/api/Incidencias/{id}` | Detalle con comentarios e historial | Autenticado |
| POST | `/api/Incidencias` | Crear incidencia | Autenticado |
| PUT | `/api/Incidencias/{id}` | Actualizar estado y/o técnico | Técnico, Admin |
| DELETE | `/api/Incidencias/{id}` | Eliminar incidencia | Admin |
| GET | `/api/Incidencias/mis` | Incidencias del usuario autenticado | Autenticado |
| GET | `/api/Incidencias/panel-tecnico` | Asignadas al técnico + sin asignar | Técnico, Admin |
| PUT | `/api/Incidencias/{id}/asignar` | Autoasignarse una incidencia | Técnico, Admin |
| GET | `/api/Incidencias/stats` | Estadísticas del dashboard admin | Admin |
| GET | `/api/Incidencias/mis-stats` | Stats personales del técnico | Técnico, Admin |
| GET | `/api/Incidencias/{id}/auditoria` | Historial de auditoría | Admin |
| GET | `/api/Incidencias/{id}/suscrito` | Comprobar si el usuario está suscrito | Técnico, Admin |
| POST | `/api/Incidencias/{id}/suscribir` | Suscribirse a una incidencia | Técnico, Admin |
| DELETE | `/api/Incidencias/{id}/suscribir` | Desuscribirse de una incidencia | Técnico, Admin |
| GET | `/api/Incidencias/consulta` | Consulta del asistente fijo| Admin |
| GET | `/api/Incidencias/{id}/sugerencia` | Sugerencia de solución IA para una incidencia | Técnico, Admin |
| POST | `/api/Incidencias/consulta-libre` | Asistente IA en lenguaje natural| Admin |

### Comentarios

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| GET | `/api/Comentarios/{incidenciaId}` | Listar comentarios | Autenticado |
| POST | `/api/Comentarios` | Añadir comentario | Autenticado |
| DELETE | `/api/Comentarios/{id}` | Eliminar comentario | Admin |

### Usuarios

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| GET | `/api/Usuarios` | Listar todos | Admin |
| PUT | `/api/Usuarios/{id}/rol` | Cambiar rol | Admin |

### SignalR Hub

`/hubs/incidencias` — el token JWT se pasa como query string `?access_token=`.

| Evento | Destinatario | Payload |
|--------|-------------|---------|
| `NuevaIncidencia` | Grupo `admins` | id, titulo, categoria |
| `CambioEstado` | Grupo `admins` + `tecnico-{id}` + `user-{id}` | id, estado |
| `NuevoComentario` | Grupos relevantes | id, contenido, autor |
| `NuevaNotificacion` | Grupo `user-{id}` | mensaje |

---

## Estructura del proyecto

```
IncidenciasApp/
├── Api/
│   ├── Controllers/        # AuthController, IncidenciasController, ComentariosController, UsuariosController
│   ├── DTOs/               # Objetos de transferencia con validaciones
│   ├── Models/             # Usuario, Incidencia, Comentario, HistorialEstado, Auditoria,
│   │                       # Notificacion, SuscripcionIncidencia
│   ├── Data/               # AppDbContext (EF Core)
│   ├── Hubs/               # IncidenciasHub (SignalR)
│   ├── Services/           # ClasificadorService (Groq IA), EmailService (MailKit)
│   └── Migrations/
├── Api.Tests/              # Tests de integración (xUnit + WebApplicationFactory)
├── Web/
│   ├── Pages/
│   │   ├── Admin/          # Index, Incidencias, Usuarios, Detalle, Asistente, ExportarExcel, ExportarPdf
│   │   ├── Tecnico/        # Index (con stats), Detalle
│   │   └── Usuario/        # Index, Crear, Detalle
│   └── Models/
├── Desktop/
│   └── Views/              # LoginPage, IncidenciasPage, DetallePage, UsuariosPage, AsistentePage
└── Shared/                 # Constantes: Roles, Estados, Prioridades, Categorias
```

---

## Tests

El proyecto incluye tests de integración en `Api.Tests/` que prueban los endpoints reales con base de datos en memoria.

```bash
dotnet test Api.Tests/Api.Tests.csproj
```

| Suite | Descripción |
|-------|-------------|
| `AuthTests` | Registro, login, validaciones |
| `IncidenciasTests` | CRUD, roles, stats, asignación |
| `UsuariosTests` | Gestión de roles, protección último admin |
| `ComentariosTests` | Crear, listar, eliminar |

---

## Modelo entidad-relación

```mermaid
erDiagram
    USUARIO {
        int Id PK
        string Nombre
        string Email
        string PasswordHash
        string Rol
    }
    INCIDENCIA {
        int Id PK
        string Titulo
        string Descripcion
        string Estado
        string Prioridad
        string Categoria
        string JustificacionIA
        datetime FechaCreacion
        datetime FechaActualizacion
        int UsuarioCreadorId FK
        int TecnicoAsignadoId FK
    }
    COMENTARIO {
        int Id PK
        string Contenido
        datetime FechaCreacion
        int UsuarioId FK
        int IncidenciaId FK
    }
    HISTORIALESTADO {
        int Id PK
        string EstadoAnterior
        string EstadoNuevo
        datetime FechaCambio
        int UsuarioId FK
        int IncidenciaId FK
    }
    AUDITORIA {
        int Id PK
        string TipoCambio
        string ValorAnterior
        string ValorNuevo
        datetime FechaCambio
        int UsuarioId FK
        int IncidenciaId FK
    }
    NOTIFICACION {
        int Id PK
        string Mensaje
        bool Leida
        datetime FechaCreacion
        int UsuarioId FK
        int IncidenciaId FK
    }
    SUSCRIPCIONINCIDENCIA {
        int Id PK
        datetime FechaSuscripcion
        int UsuarioId FK
        int IncidenciaId FK
    }

    USUARIO ||--o{ INCIDENCIA : "crea"
    USUARIO ||--o{ INCIDENCIA : "tiene asignada"
    USUARIO ||--o{ COMENTARIO : "escribe"
    USUARIO ||--o{ HISTORIALESTADO : "genera"
    USUARIO ||--o{ AUDITORIA : "genera"
    USUARIO ||--o{ NOTIFICACION : "recibe"
    USUARIO ||--o{ SUSCRIPCIONINCIDENCIA : "se suscribe"
    INCIDENCIA ||--o{ COMENTARIO : "tiene"
    INCIDENCIA ||--o{ HISTORIALESTADO : "registra"
    INCIDENCIA ||--o{ AUDITORIA : "registra"
    INCIDENCIA ||--o{ NOTIFICACION : "genera"
    INCIDENCIA ||--o{ SUSCRIPCIONINCIDENCIA : "tiene"
```

---

## Ciclo de vida de una incidencia

```
Abierta → EnProceso → Resuelta → Cerrada
```

Cada cambio de estado queda registrado en `HistorialEstado` y en `Auditoria`.

## SLA por prioridad

| Prioridad | Tiempo máximo |
|-----------|--------------|
| Crítica | 2 horas |
| Alta | 8 horas |
| Media | 24 horas |
| Baja | 72 horas |
