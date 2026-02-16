FROM node:22-alpine AS build
WORKDIR /app
COPY Frontend/package.json Frontend/package-lock.json* ./
RUN npm ci
COPY Frontend/ .
RUN npm run build -- --configuration production
RUN test -f /app/dist/browser/browser/index.html || (echo "Build output missing at /app/dist/browser/browser" && exit 1)

FROM nginx:alpine AS final
RUN rm -f /etc/nginx/conf.d/default.conf
RUN rm -rf /usr/share/nginx/html/*
COPY Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/browser/browser/ /usr/share/nginx/html/
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
