FROM node:20.19-alpine AS build
WORKDIR /app

COPY src/DocFlowCloud.Web/package.json ./
COPY src/DocFlowCloud.Web/package-lock.json ./
RUN npm ci

COPY src/DocFlowCloud.Web ./

ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY --from=build /app/dist ./

EXPOSE 80
