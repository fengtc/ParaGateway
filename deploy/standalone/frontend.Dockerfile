FROM nginx:1.27-alpine
# Blazor WASM is published on a build machine that has the licensed DevExpress
# packages. This image is only an optional runtime image for a prepared
# deploy/standalone/data/frontend directory. The static application is under
# its wwwroot child, matching the bind mount used by docker-compose.
COPY deploy/standalone/data/frontend/wwwroot/ /usr/share/nginx/html/
COPY deploy/standalone/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
