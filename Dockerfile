# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# csproj 와 중앙 패키지 설정만 먼저 복사해 restore 를 별도 레이어로 만든다.
# 소스만 바뀌었을 때 패키지를 다시 내려받지 않기 위해서다.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/EmployeeDirectory.Domain/EmployeeDirectory.Domain.csproj                 src/EmployeeDirectory.Domain/
COPY src/EmployeeDirectory.Application/EmployeeDirectory.Application.csproj       src/EmployeeDirectory.Application/
COPY src/EmployeeDirectory.Infrastructure/EmployeeDirectory.Infrastructure.csproj src/EmployeeDirectory.Infrastructure/
COPY src/EmployeeDirectory.Api/EmployeeDirectory.Api.csproj                       src/EmployeeDirectory.Api/

RUN dotnet restore src/EmployeeDirectory.Api/EmployeeDirectory.Api.csproj

COPY src/ src/
# 최초 실행 시 자동 적재되는 샘플 데이터. Api 프로젝트가 출력 폴더로 복사한다.
COPY samples/ samples/

RUN dotnet publish src/EmployeeDirectory.Api/EmployeeDirectory.Api.csproj \
      -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
# runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

# SQLite 파일을 앱 디렉터리가 아닌 /data 로 분리한다.
# 비특권 사용자로 실행하기 때문에 쓰기 가능한 경로가 따로 필요하고,
# 컨테이너를 다시 만들어도 데이터를 유지하려면 이 경로에 볼륨을 붙이면 된다.
ENV ConnectionStrings__Default="Data Source=/data/employee-directory.db"
RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
VOLUME ["/data"]

EXPOSE 8080

# 루트로 실행하지 않는다(.NET 8 이미지가 제공하는 비특권 사용자).
USER $APP_UID

# PaaS(Render, Cloud Run 등)는 PORT 환경변수로 바인딩할 포트를 알려준다.
# 이 처리를 빠뜨리면 배포는 성공했는데 포트를 못 찾아 죽는다.
# 로컬에서는 PORT 가 없으므로 8080 을 쓴다.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet EmployeeDirectory.Api.dll"]
