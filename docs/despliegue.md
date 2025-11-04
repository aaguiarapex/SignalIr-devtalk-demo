# Guía de despliegue en Azure

Esta guía describe cómo provisionar la infraestructura en Azure, publicar la aplicación de chat en tiempo real basada en Azure SignalR Service y automatizar el proceso con GitHub Actions.

## Arquitectura resumida
- **App Service (Linux, .NET 7)** para alojar la API + cliente estático (`DemoApp`).
- **Azure SignalR Service (SKU Free)** para manejar la mensajería en tiempo real.
- **Bicep (`infra/main.bicep`)** orquesta toda la infraestructura y expone la cadena de conexión que se inyecta en el Web App.

## Preparación en Azure Portal
Antes de ejecutar cualquier script, realiza esta verificación rápida:

1. Define el **nombre del Resource Group** que vas a usar. El template Bicep lo creará automáticamente si no existe.
2. Confirma que la región elegida tenga disponibilidad para **Azure SignalR Service** en SKU Free.
3. Asegúrate de que los *resource providers* `Microsoft.SignalRService` y `Microsoft.Web` estén registrados en la suscripción (Azure Portal ➜ *Subscriptions* ➜ *Resource providers*).

## Prerrequisitos
1. Cuenta de Azure activa con permisos para crear resource groups, App Service y SignalR.
2. [Azure CLI 2.47+](https://learn.microsoft.com/cli/azure/install-azure-cli) con sesión iniciada: `az login`.
3. [.NET SDK 7.0](https://dotnet.microsoft.com/en-us/download) instalado localmente.
4. (Opcional) Service Principal para flujos CI/CD (ver sección de GitHub Actions).

## Parámetros y secretos
- El archivo `infra/secrets.example.json` muestra cómo debe lucir la configuración local. Copia su contenido en tu `secrets.json` o `dotnet user-secrets` y reemplaza `ConnectionString` por el valor real cuando lo obtengas.
- El parámetro `baseName` del Bicep debe contener solo minúsculas y números (p. ej. `signalirdemo`). Todos los recursos heredarán este prefijo (`signalirdemo-web`, `signalirdemo-signalr`, etc.).

## Provisionar infraestructura con Bicep
Ejecuta desde la raíz del repositorio (no necesitas crear el Resource Group previamente):

```bash
az deployment sub create \
  --name signalir-deploy \
  --location <region> \
  --template-file infra/main.bicep \
  --parameters resourceGroupName=<resource-group> baseName=<prefijo> location=<region>
```

La salida incluye `webAppName`, `webAppDefaultHostName` y `signalRConnectionString`. Para consultar la cadena de conexión más tarde:

```bash
az deployment sub show \
  --name signalir-deploy \
  --location <region> \
  --query "properties.outputs.signalRConnectionString.value" -o tsv
```

### Configurar secretos locales
Guarda la cadena de conexión para ejecutar la app en tu máquina:

```bash
dotnet user-secrets set "Azure:SignalR:ConnectionString" "<valor-de-signalRConnectionString>" \
  --project DemoApp/DemoApp.csproj
```

Para probar en local:

```bash
dotnet run --project DemoApp/DemoApp.csproj
```

## Publicación manual en App Service
### Provisionar y publicar solo con CLI
Ejecuta todos los comandos desde la raíz del repositorio y sustituye los valores entre `< >`.

```bash
# 1. Variables reutilizables
RG=<resource-group>
BASE=<prefijo-base>
LOCATION=<region>
DEPLOY=signalir-cli
WEB=${BASE}-web

# 2. Inicia sesión en Azure CLI
az login

# 3. (Opcional) establece la suscripción
az account set --subscription <subscription-id>

# 4. Despliega infraestructura con Bicep
az deployment sub create \
  --name "$DEPLOY" \
  --location "$LOCATION" \
  --template-file infra/main.bicep \
  --parameters resourceGroupName="$RG" baseName="$BASE" location="$LOCATION"

# 5. Obtén la cadena de conexión de SignalR
SIGNALR_CONN=$(az deployment sub show \
  --name "$DEPLOY" \
  --location "$LOCATION" \
  --query "properties.outputs.signalRConnectionString.value" -o tsv)

# 6. Compila y publica la app
dotnet publish DemoApp/DemoApp.csproj -c Release -o publish

# 7. Empaqueta los artefactos
(cd publish && zip -r ../demoapp.zip .)

# 8. Despliega al Web App
az webapp deploy \
  --resource-group "$RG" \
  --name "$WEB" \
  --src-path demoapp.zip

# 9. Abre el sitio en el navegador
az webapp browse --resource-group "$RG" --name "$WEB"
```

Con estos pasos puedes repetir el despliegue sin depender del pipeline. Ejecuta el workflow cuando quieras automatizar el mismo proceso tras cada commit.

## Automatización con GitHub Actions
El flujo `deploy.yml` (ver `.github/workflows/deploy.yml`) automatiza la provisión y despliegue en cada push a `main` (y permite `workflow_dispatch`). Pasos clave:

1. **Crear un Service Principal** con permisos suficientes:
   ```bash
   az ad sp create-for-rbac \
     --name "github-deploy-signalir" \
     --role contributor \
     --scopes /subscriptions/<subscription-id>
   ```
   Guarda `appId`, `password`, `tenant`.

2. **Configurar secretos en GitHub** (`Settings` ➜ `Secrets and variables` ➜ `Actions`):
   - `AZURE_CLIENT_ID` = `appId` del Service Principal.
   - `AZURE_CLIENT_SECRET` = `password`.
   - `AZURE_TENANT_ID` = `tenant`.
   - `AZURE_SUBSCRIPTION_ID` = ID de la suscripción.
   - `AZURE_RG_NAME` = nombre del Resource Group que creará el template.
   - `AZURE_BASE_NAME` = prefijo usado en Bicep.
   - `AZURE_LOCATION` = región donde se desplegará todo (p. ej. `eastus`).

3. **Estructura general del workflow**:
   - Inicio de sesión en Azure (`azure/login`).
   - `az deployment sub create` para crear (o actualizar) el Resource Group y el resto de recursos.
   - `dotnet publish` para generar el paquete.
   - `azure/webapps-deploy` para subir el ZIP al Web App.
   - Cuando lanzas el workflow manualmente desde *Actions*, puedes elegir la región desde un desplegable y, si quieres, sobrescribir el resource group y el prefijo base sin editar el YAML.

Al completar estos pasos, cada commit en `main` actualizará la infraestructura y el sitio sin intervención manual.

## Verificación y mantenimiento
- Logs de la aplicación: `az webapp log tail --name <baseName>-web --resource-group <rg>`.
- SignalR métricas: Azure Portal ➜ resource `signalR` ➜ pestaña *Usage*.
- La UI incluye tres salas predefinidas (`#.NET`, `#Java`, `#Full Stack HTML`). Usa dos navegadores en la misma sala para comprobar la mensajería en tiempo real y cambia de sala para demostrar el aislamiento.
- Si cambias dependencias o versión de .NET, actualiza `infra/main.bicep` (propiedad `linuxFxVersion`) y el workflow.
- Para eliminar todo, borra el recurso: `az group delete --name <resource-group> --yes --no-wait`.

Con estos pasos tendrás la infraestructura, aplicación UI y backend de SignalR activos en Azure, listos para demos o pruebas.
