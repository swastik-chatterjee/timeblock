FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TimeBlock.sln ./
COPY src/TimeBlock.Core/TimeBlock.Core.csproj src/TimeBlock.Core/
COPY src/TimeBlock.Api/TimeBlock.Api.csproj src/TimeBlock.Api/
COPY src/TimeBlock.Web/TimeBlock.Web.csproj src/TimeBlock.Web/
COPY src/TimeBlock.Tests/TimeBlock.Tests.csproj src/TimeBlock.Tests/
RUN dotnet restore TimeBlock.sln

COPY src/ src/
RUN dotnet publish src/TimeBlock.Api/TimeBlock.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TimeBlock.Api.dll"]
