# Bước 1: Build ứng dụng với .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Sao chép toàn bộ mã nguồn
COPY . .

# Bổ sung gói SQLite tự động cho môi trường Cloud (Render / Railway) để không cần cài SQL Server rời
RUN dotnet add "QuanLyKho.csproj" package Microsoft.EntityFrameworkCore.Sqlite --version "10.0.*"

# Publish ứng dụng với cờ USE_SQLITE
RUN dotnet publish "QuanLyKho.csproj" -c Release -p:DefineConstants=USE_SQLITE -o /app/publish

# Bước 2: Chạy ứng dụng trên .NET 10 ASP.NET Runtime nhẹ
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cấu hình cổng mặc định cho Render / Railway
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "QuanLyKho.dll"]
