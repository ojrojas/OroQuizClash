export const environment = {
  production: false,
  apiUrl: '/api',
  // Usar http para evitar problemas de certificado autofirmado en dev (Aspire genera cert pero el navegador puede no confiar)
  // El AppHost expone identity-api en 5080 (http) y 5086 (https); ambos sirven discovery, pero http evita CORS/cert.
  identityAuthority: 'http://localhost:5080',
  gameHubUrl: '/hubs/game',
};
