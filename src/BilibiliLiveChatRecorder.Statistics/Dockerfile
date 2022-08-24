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
COPY ["src/BilibiliLiveApi/BilibiliLiveApi.csproj", "src/BilibiliLiveApi/"]
COPY ["src/BilibiliLiveChatRecorder.Statistics/BilibiliLiveChatRecorder.Statistics.csproj", "src/BilibiliLiveChatRecorder.Statistics/"]
COPY ["src/BilibiliLiveChatRecorder.DbModel/BilibiliLiveChatRecorder.DbModel.csproj", "src/BilibiliLiveChatRecorder.DbModel/"]
COPY ["src/BilibiliLiveChatRecorder.Core/BilibiliLiveChatRecorder.Core.csproj", "src/BilibiliLiveChatRecorder.Core/"]
RUN dotnet restore "src/BilibiliLiveChatRecorder.Statistics/BilibiliLiveChatRecorder.Statistics.csproj" -r linux-musl-x64
COPY . .
WORKDIR "/src/src/BilibiliLiveChatRecorder.Statistics"
RUN dotnet build "BilibiliLiveChatRecorder.Statistics.csproj" -c Release -r linux-musl-x64 -o /app/build

FROM build AS publish
RUN dotnet publish "BilibiliLiveChatRecorder.Statistics.csproj" -c Release -r linux-musl-x64 /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunComposite=true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Darkflame.BilibiliLiveChatRecorder.Statistics"]