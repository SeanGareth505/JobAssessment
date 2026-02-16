const PROXY_TARGET = process.env['API_PROXY_TARGET'] || 'https://localhost:7130';

module.exports = {
  '/api': {
    target: PROXY_TARGET,
    secure: false,
    changeOrigin: true,
    logLevel: 'debug',
    onProxyReq(proxyReq, req) {
      const auth = req.headers?.authorization;
      if (auth) {
        proxyReq.setHeader('Authorization', auth);
      }
    },
  },
};
