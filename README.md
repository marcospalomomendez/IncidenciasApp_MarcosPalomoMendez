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

## Endpoints principales de la API

| Método | Ruta | Descripción | Roles |
|--------|------|-------------|-------|
| POST | `/api/Auth/registro` | Registrar usuario | Público |
| POST | `/api/Auth/login` | Iniciar sesión | Público |
| GET | `/api/Incidencias` | Listar incidencias | Autenticado |
| POST | `/api/Incidencias` | Crear incidencia | Autenticado |
| PUT | `/api/Incidencias/{id}` | Actualizar estado | Técnico, Admin |
| PUT | `/api/Incidencias/{id}/asignar` | Asignarse incidencia | Técnico, Admin |
| GET | `/api/Usuarios` | Listar usuarios | Admin |
| PUT | `/api/Usuarios/{id}/rol` | Cambiar rol | Admin |

---

## Estructura del proyecto