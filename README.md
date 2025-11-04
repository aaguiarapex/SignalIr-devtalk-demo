# SignalIr-devtalk-demo

## GitHub Secrets Requeridos
Para que el flujo de despliegue en GitHub Actions funcione, define los siguientes secretos en `Settings` → `Secrets and variables` → `Actions` del repositorio:

- `AZURE_CLIENT_ID`: `appId` del Service Principal usado para autenticarse en Azure.
- `AZURE_CLIENT_SECRET`: contraseña (`password`) del Service Principal.
- `AZURE_TENANT_ID`: identificador del tenant de Azure AD.
- `AZURE_SUBSCRIPTION_ID`: suscripción donde se crearán los recursos.
- `AZURE_RG_NAME`: nombre del Resource Group destino para la infraestructura.
- `AZURE_BASE_NAME`: prefijo base reutilizado por las plantillas Bicep (por ejemplo, `signalirdemo`).
- `AZURE_LOCATION`: región de Azure para el despliegue (por ejemplo, `eastus`).

Una vez configurados, el workflow `deploy.yml` podrá autenticarse, aprovisionar recursos con Bicep y publicar la aplicación SignalR.
