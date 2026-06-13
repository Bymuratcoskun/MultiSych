# 1. Build State
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyalarını kopyalayıp bağımlılıkları yüklüyoruz
COPY ["MultiSych.Desktop/MultiSych.Desktop.csproj", "MultiSych.Desktop/"]
COPY ["MultiSych.Services/MultiSych.Services.csproj", "MultiSych.Services/"]
RUN dotnet restore "MultiSych.Desktop/MultiSych.Desktop.csproj"

# Tüm kaynak kodları kopyalayıp Release modunda derliyoruz
COPY . .
WORKDIR "/src/MultiSych.Desktop"
RUN dotnet build "MultiSych.Desktop.csproj" -c Release -o /app/build
RUN dotnet publish "MultiSych.Desktop.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime State
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Avalonia (GUI) ve SQLite/FUSE altyapısı için gerekli olan Linux kütüphaneleri yükleniyor
RUN apt-get update && apt-get install -y \
    libx11-6 libxext6 libxrender1 libxrandr2 libxtst6 \
    libxcomposite1 libxdamage1 libxcursor1 libxi6 \
    fontconfig libfontconfig1 libgl1-mesa-glx \
    fuse libfuse2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Uygulamayı çalıştırıyoruz
ENTRYPOINT ["dotnet", "MultiSych.Desktop.dll"]
