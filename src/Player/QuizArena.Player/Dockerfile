# Build stage — context debe ser la raíz del repo:
# podman build -f src/Player/QuizArena.Player/Dockerfile -t localhost/quizarena-player:latest .
FROM node:24 AS build

WORKDIR /repo

# Copiar solo lo necesario para instalar deps (aprovecha cache)
COPY src/Player/QuizArena.Player/package.json src/Player/QuizArena.Player/pnpm-lock.yaml src/Player/QuizArena.Player/pnpm-workspace.yaml src/Player/QuizArena.Player/.npmrc* ./src/Player/QuizArena.Player/
COPY design-system ./design-system

WORKDIR /repo/src/Player/QuizArena.Player

RUN corepack enable && pnpm config set ignore-scripts false && pnpm install --frozen-lockfile

# Copiar fuente angular
COPY src/Player/QuizArena.Player/angular.json ./angular.json
COPY src/Player/QuizArena.Player/tsconfig.json ./tsconfig.json
COPY src/Player/QuizArena.Player/tsconfig.app.json ./tsconfig.app.json
COPY src/Player/QuizArena.Player/tsconfig.spec.json ./tsconfig.spec.json
COPY src/Player/QuizArena.Player/proxy.conf.json ./proxy.conf.json
COPY src/Player/QuizArena.Player/src ./src

# Build producción (genera dist/quizarena-player/browser)
RUN pnpm run build

FROM docker.io/library/nginx:alpine

# Template nginx: Aspire inyecta PORT y API_URL
COPY src/Player/QuizArena.Player/default.conf.template /etc/nginx/templates/default.conf.template

COPY --from=build /repo/src/Player/QuizArena.Player/dist/quizarena-player/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
