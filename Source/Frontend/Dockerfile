# Build context is Source/Frontend: the app is self-contained and the generated GraphQL types
# are committed, so the exported schema is not needed at image build time.
FROM node:22-alpine AS build
WORKDIR /src

# Corepack reads packageManager from package.json, so the image uses the same Yarn as the repo.
COPY package.json yarn.lock .yarnrc.yml ./
RUN corepack enable && yarn install --immutable

COPY . .
RUN yarn build

FROM nginx:1.29-alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
# map lives in the http block, so it goes in conf.d rather than in the server template.
COPY nginx-upgrade.conf /etc/nginx/conf.d/00-upgrade.conf
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
EXPOSE 80
