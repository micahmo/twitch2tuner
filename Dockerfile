#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["twitch2tuner/twitch2tuner.csproj", "twitch2tuner/"]
RUN dotnet restore "twitch2tuner/twitch2tuner.csproj"
COPY . .
WORKDIR "/src/twitch2tuner"
#RUN dotnet build "twitch2tuner.csproj" -c Release -o /app/build
RUN dotnet publish "twitch2tuner.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
RUN apt-get update && apt-get install -y \
  # Install pip so that we can use pip to install yt-dlp and streamlink at runtime
  python3-full \
  python3-pip \
  python3-venv \
  # yt-dlp needs ffmpeg, but it can't be installed with pip
  ffmpeg \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app
RUN python3 -m venv /opt/venv
# Enable venv
ENV PATH="/opt/venv/bin:$PATH"
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "twitch2tuner.dll"]