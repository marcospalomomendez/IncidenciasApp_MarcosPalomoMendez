# IncidenciasApp — Marcos Palomo Méndez

Sistema de gestión de incidencias desarrollado como TFG. Permite a usuarios reportar incidencias, a técnicos gestionarlas y a administradores supervisar el sistema desde tres clientes distintos sobre una misma API REST.

---

## Tecnologías

- **Backend:** ASP.NET Core 8, Entity Framework Core, SQLite, JWT
- **Web:** Razor Pages + Bootstrap 5
- **Desktop:** WPF (.NET 8)
- **Shared:** Librería de constantes compartida entre proyectos

---

## Roles

| Rol | Acceso |
|-----|--------|
| `Usuario` | Web — crea y consulta sus propias incidencias |
| `Tecnico` | Web + Desktop — gestiona incidencias asignadas y sin asignar |
| `Admin` | Web + Desktop — panel completo: incidencias, usuarios y estadísticas |

---

## Instalación y ejecución

### Requisitos
- .NET 8 SDK
- Visual Studio 2022

### Pasos

1. Clona el repositorio:
```bash
git clone https://github.com/marcospalomomendez/IncidenciasApp_MarcosPalomoMendez.git
```

2. Crea el archivo `Api/appsettings.Development.json` con tu clave JWT:
```json
{
  "Jwt": {
    "Key": "TuClaveSecretaAqui"
  }
}
```

3. Crea la base de datos:
```bash
cd Api
dotnet ef database update
```

4. Arranca la API:
```bash
cd Api
dotnet run
```

5. Arranca el cliente web:
```bash
cd Web
dotnet run
```

6. Arranca el cliente desktop desde Visual Studio (proyecto Desktop).

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
| PUT | `/api/Incidencias/{id}` | Actualizar estado y/o técnico asignado | Técnico, Admin |
| DELETE | `/api/Incidencias/{id}` | Eliminar incidencia | Admin |
| GET | `/api/Incidencias/mis` | Incidencias creadas por el usuario autenticado | Autenticado |
| GET | `/api/Incidencias/panel-tecnico` | Asignadas al técnico + sin asignar | Técnico, Admin |
| PUT | `/api/Incidencias/{id}/asignar` | Autoasignarse una incidencia | Técnico, Admin |
| GET | `/api/Incidencias/stats` | Estadísticas del dashboard | Admin |

### Comentarios

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| GET | `/api/Comentarios/{incidenciaId}` | Listar comentarios de una incidencia | Autenticado |
| POST | `/api/Comentarios` | Añadir comentario | Autenticado |
| DELETE | `/api/Comentarios/{id}` | Eliminar comentario | Admin |

### Usuarios

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| GET | `/api/Usuarios` | Listar todos los usuarios | Admin |
| PUT | `/api/Usuarios/{id}/rol` | Cambiar rol de un usuario | Admin |

---

## Estructura del proyecto

```
IncidenciasApp/
├── Api/                        # REST API
│   ├── Controllers/            # AuthController, IncidenciasController, ComentariosController, UsuariosController
│   ├── DTOs/                   # Objetos de transferencia con validaciones
│   ├── Models/                 # Usuario, Incidencia, Comentario, HistorialEstado
│   ├── Data/                   # AppDbContext (EF Core)
│   └── Migrations/
├── Web/                        # Cliente Razor Pages
│   ├── Pages/
│   │   ├── Admin/              # Dashboard, Incidencias, Usuarios, Detalle
│   │   ├── Tecnico/            # Panel técnico con filtros
│   │   └── Usuario/            # Panel usuario, Crear, Detalle
│   └── Models/
├── Desktop/                    # Cliente WPF
│   └── Views/                  # LoginPage, IncidenciasPage, DetallePage
└── Shared/                     # Constantes: Roles, Estados, Prioridades
```

---

## Estados de una incidencia

`Abierta` → `EnProceso` → `Resuelta` → `Cerrada`

Cada cambio de estado queda registrado en el historial (`HistorialEstado`).

## Prioridades

`Baja` | `Media` | `Alta` | `Critica`
