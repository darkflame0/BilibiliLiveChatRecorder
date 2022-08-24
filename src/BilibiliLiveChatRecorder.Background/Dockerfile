FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine AS base
WORKDIR /app
EXPOSE 80

ENV TZ=Asia/Shanghai


RUN apk add --no-cache tzdata \
  && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
  && echo "Asia/Shanghai" > /etc/timezone \
  && rm -rf \
  /var/cache/apk/*

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src

COPY ["nuget.config", "."]
COPY ["src/BilibiliLiveChatRecorder.Background/BilibiliLiveChatRecorder.Background.csproj", "src/BilibiliLiveChatRecorder.Background/"]
COPY ["src/BilibiliLiveApi/BilibiliLiveApi.csproj", "src/BilibiliLiveApi/"]
COPY ["src/BilibiliLiveChatRecorder.Transport/BilibiliLiveChatRecorder.Transport.csproj", "src/BilibiliLiveChatRecorder.Transport/"]
COPY ["src/BilibiliLiveChatRecorder.Core/BilibiliLiveChatRecorder.Core.csproj", "src/BilibiliLiveChatRecorder.Core/"]
COPY ["src/BilibiliLiveChatRecorder.DbModel/BilibiliLiveChatRecorder.DbModel.csproj", "src/BilibiliLiveChatRecorder.DbModel/"]
RUN dotnet restore "src/BilibiliLiveChatRecorder.Background/BilibiliLiveChatRecorder.Background.csproj" -r linux-musl-x64
COPY . .
WORKDIR "/src/src/BilibiliLiveChatRecorder.Background"
RUN dotnet build "BilibiliLiveChatRecorder.Background.csproj" -c Release -r linux-musl-x64 -o /app/build

FROM build AS publish
RUN dotnet publish "BilibiliLiveChatRecorder.Background.csproj" -c Release -r linux-musl-x64 /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunComposite=true -o /app/publish

FROM base AS final
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Darkflame.BilibiliLiveChatRecorder.Background"]