import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    // Aumenta el límite de headers para evitar 431 con JWT grande + cookies Aspire
    // Vite default es 8KB (Node 16384), con OIDC + SignalR puede superarse
    maxHeaderSize: 32768,
    headersTimeout: 120000,
  }
});
