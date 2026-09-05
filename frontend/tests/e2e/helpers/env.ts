/** Host-published ports/URLs for the demo scenarios, matching .env.example's defaults. */
export const env = {
  authUrl: process.env.E2E_AUTH_URL ?? 'http://localhost:8081',
  gatewayUrl: process.env.E2E_GATEWAY_URL ?? 'http://localhost:8084',
  notificationUrl: process.env.E2E_NOTIFICATION_URL ?? 'http://localhost:8085',
  rabbitMqManagementUrl: process.env.E2E_RABBITMQ_MANAGEMENT_URL ?? 'http://localhost:15672',
  rabbitMqAdminUser: process.env.E2E_RABBITMQ_ADMIN_USER ?? 'admin',
  rabbitMqAdminPassword: process.env.E2E_RABBITMQ_ADMIN_PASSWORD ?? 'admin_rmq_password',
  rabbitMqVirtualHost: process.env.E2E_RABBITMQ_VHOST ?? 'weakapphandler',
  viewerEmail: 'viewer@weakapphandler.local',
  viewerPassword: 'Viewer#12345',
  adminEmail: 'admin@weakapphandler.local',
  adminPassword: 'Admin#12345',
}
