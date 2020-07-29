#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM harbor.nngeo.net/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM harbor.nngeo.net/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/SmartHomeGateway/SmartHomeGateway.csproj", "src/SmartHomeGateway/"]
COPY ["src/SmartHome.Application/SmartHome.Application.csproj", "src/SmartHome.Application/"]
COPY ["src/SmartHome.Domain/SmartHome.Domain.csproj", "src/SmartHome.Domain/"]
RUN dotnet restore "src/SmartHomeGateway/SmartHomeGateway.csproj"
COPY . .
WORKDIR "/src/src/SmartHomeGateway"
RUN dotnet build "SmartHomeGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartHomeGateway.csproj" -c Release -o /app/publish

RUN /bin/cp /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
&& echo 'Asia/Shanghai' >/etc/timezone


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartHomeGateway.dll"]