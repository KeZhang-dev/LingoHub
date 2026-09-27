# ============================================================
# 后端（.NET API）的 Docker 镜像。docker-compose.yml 会用它构建 api 服务。
#
# 两个阶段：
#   build：用 SDK 镜像（有编译器，体积大）把代码编译成 dll
#   最终： 用 aspnet 镜像（只能运行，体积小）运行编译好的 dll
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先只复制 csproj 并还原 NuGet 包。
# 只要依赖没变，Docker 会复用这一层缓存，改代码后重新构建会快很多
COPY LingoHub.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# PostgreSQL 驱动（Npgsql）启动时会尝试加载 Kerberos 登录库，镜像里没有就会在日志里报错。
# 我们不用 Kerberos，但装上它能去掉这条吓人的报错（很小，几 MB）
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# 上传的 PDF 存在这里（docker-compose.yml 会挂载一个卷，重启不丢）。
# 程序用非 root 用户运行（更安全），所以要提前把这个文件夹交给它
RUN mkdir -p /app/uploads && chown $APP_UID /app/uploads
USER $APP_UID

# aspnet 镜像默认监听 8080 端口
EXPOSE 8080
ENTRYPOINT ["dotnet", "LingoHub.Api.dll"]
