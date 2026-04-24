FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ARG API_BASE_URL=http://localhost:5225

COPY HotelChain.Domain/HotelChain.Domain.csproj HotelChain.Domain/
COPY HotelChain.Web/HotelChain.Web.csproj HotelChain.Web/

RUN dotnet restore HotelChain.Web/HotelChain.Web.csproj

COPY . .

RUN printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$API_BASE_URL" > /src/HotelChain.Web/wwwroot/appsettings.json

WORKDIR /src/HotelChain.Web
RUN dotnet publish HotelChain.Web.csproj -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

COPY --from=build /app/publish/wwwroot ./

RUN printf 'server {\n\
    listen 80;\n\
    server_name localhost;\n\
    root /usr/share/nginx/html;\n\
    index index.html;\n\
\n\
    location / {\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80