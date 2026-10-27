# Plant Encyclopedia website (design 2026-10-27): the Angular production build served by nginx over HTTPS, with
# /api, /health, /media and the sign-in callbacks passed to the API. Build context: frontend/.

FROM node:24-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build

FROM nginx:1.27-alpine
RUN apk add --no-cache openssl
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Executable whatever the checkout (nginx skips entrypoint scripts that are not).
COPY --chmod=0755 docker-entrypoint.d/ /docker-entrypoint.d/
COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html
EXPOSE 443
