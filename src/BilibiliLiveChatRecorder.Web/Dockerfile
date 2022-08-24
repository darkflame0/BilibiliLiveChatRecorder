FROM node AS build
COPY ./ClientApp /app
WORKDIR /app

ENV NODE_OPTIONS=--openssl-legacy-provider

RUN yarn && yarn run build -- --prod

FROM nginx:stable-alpine
EXPOSE 80
EXPOSE 443

COPY nginx-site.conf /etc/nginx/conf.d/default.conf

WORKDIR /app
COPY --from=build /app/dist .
CMD ["nginx", "-g", "daemon off;"]
