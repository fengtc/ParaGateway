FROM nginx:1.27-alpine
# Blazor WASM is published on a build machine that has the licensed DevExpress
# packages. This image is only an optional runtime image for a prepared
# deploy/standalone/data/frontend directory; docker-compose mounts that
# directory directly so the server never needs the private NuGet source.
COPY deploy/standalone/data/frontend/ /usr/share/nginx/html/
COPY deploy/standalone/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
