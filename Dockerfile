FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

EXPOSE 5678

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
COPY --from=build-env /app/out .

ENV GOOGLE_APPLICATION_CREDENTIALS=/app/cert-files/shoppingnow-1519d-firebase-adminsdk-fqk5x-36c14e1451.json

# No need to copy the cert-files anymore because we directly mount them to the docker image.
# See docker-compose shoppinglistserver/volumes
# RUN mkdir cert-files
# ADD cert-keys/fullchain.pem cert-keys/privkey.pem app/cert-files/

CMD ["dotnet", "ef" ,"database" ,"update"]
CMD ["dotnet", "ShoppingListServer.dll", "--server.urls", "http://0.0.0.0:5678"]
