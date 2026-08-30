const target = process.env.API_URL || process.env.api__url || process.env.services__oroclash_api__http__0 || 'http://localhost:5199';
console.log(`[proxy] API target: ${target}`);
module.exports = {
  "/api": {
    target: target,
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
  },
  "/hubs": {
    target: target,
    secure: false,
    changeOrigin: true,
    ws: true,
    logLevel: "debug",
  }
};
