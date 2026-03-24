FROM node:20.19-alpine AS build
WORKDIR /app

COPY src/DocFlowCloud.Web/package.json ./
COPY src/DocFlowCloud.Web/package-lock.json ./
RUN npm ci

COPY src/DocFlowCloud.Web ./

ARG VITE_API_BASE_URL=http://localhost:8080
ARG VITE_APP_ENV=development
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_APP_ENV=$VITE_APP_ENV

RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY --from=build /app/dist ./
COPY src/DocFlowCloud.Web/config.js.template ./config.js.template
COPY src/DocFlowCloud.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/DocFlowCloud.Web/docker-entrypoint.d/40-generate-runtime-config.sh /docker-entrypoint.d/40-generate-runtime-config.sh

RUN chmod +x /docker-entrypoint.d/40-generate-runtime-config.sh

EXPOSE 80
