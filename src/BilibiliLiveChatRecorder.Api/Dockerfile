FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

ENV TZ=Asia/Shanghai

RUN apk add --no-cache tzdata \
  && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
  && echo "Asia/Shanghai" > /etc/timezone \
  && rm -rf \
  /var/cache/apk/*

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src


COPY ["nuget.config", "."]
COPY ["src/BilibiliLiveChatRecorder.Api/BilibiliLiveChatRecorder.Api.csproj", "src/BilibiliLiveChatRecorder.Api/"]
COPY ["src/BilibiliLiveApi/BilibiliLiveApi.csproj", "src/BilibiliLiveApi/"]
COPY ["src/BilibiliLiveChatRecorder.DbModel/BilibiliLiveChatRecorder.DbModel.csproj", "src/BilibiliLiveChatRecorder.DbModel/"]
COPY ["src/BilibiliLiveChatRecorder.Core/BilibiliLiveChatRecorder.Core.csproj", "src/BilibiliLiveChatRecorder.Core/"]
RUN dotnet restore "src/BilibiliLiveChatRecorder.Api/BilibiliLiveChatRecorder.Api.csproj" -r linux-musl-x64
COPY . .
WORKDIR "/src/src/BilibiliLiveChatRecorder.Api"
RUN dotnet build "BilibiliLiveChatRecorder.Api.csproj" -c Release -r linux-musl-x64 -o /app/build

FROM build AS publish
RUN dotnet publish "BilibiliLiveChatRecorder.Api.csproj" -c Release -r linux-musl-x64 /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunComposite=true -o /app/publish

FROM base AS final
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Darkflame.BilibiliLiveChatRecorder.Api"]