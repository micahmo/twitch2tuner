#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["twitch2dvr/twitch2dvr.csproj", "twitch2dvr/"]
RUN dotnet restore "twitch2dvr/twitch2dvr.csproj"
COPY . .
WORKDIR "/src/twitch2dvr"
RUN dotnet build "twitch2dvr.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "twitch2dvr.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN apt-get update && apt-get install -y \
  # Install pip so that we can use pip to install youtube-dl and streamlink at runtime
  python3-pip \
  # youtube-dl needs ffmpeg, but it can't be installed with pip
  ffmpeg \
  && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "twitch2dvr.dll"]