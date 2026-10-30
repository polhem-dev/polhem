# The public Northwind demo site: Polhem.Northwind.Blazor in its Demo environment, which also serves the JSON-RPC
# endpoint at /api and the Avalonia WASM head at /web/ (see Polhem.Northwind.Blazor/Demo/NorthwindDemo.cs).
#
# Build from the repository root, which the projects reference through ProjectReference:
#
#   docker build -f apps/Polhem.Northwind/Dockerfile -t polhem-northwind-demo .
#   docker run --rm -p 8080:8080 polhem-northwind-demo
#
# The database lives inside the container and is seeded at startup, so every restart starts from the seed again.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# SkiaSharp and HarfBuzz ship static native libraries, so publishing the WASM head relinks dotnet.native.wasm even
# with trimming and AOT off, and that needs the workload. Its Emscripten compiler runs on Python, which the SDK
# image does not carry on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet workload install wasm-tools
WORKDIR /src
COPY . .
RUN dotnet publish apps/Polhem.Northwind/Polhem.Northwind.Browser -c Release -o /out/browser
# Publishing for the image's architecture keeps only its native libraries (SQLite's among them); a portable publish
# carries one copy per platform.
ARG TARGETARCH
RUN rid="linux-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
    && dotnet publish apps/Polhem.Northwind/Polhem.Northwind.Blazor -c Release -r "$rid" --self-contained false \
       -o /out/app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/app/ ./
COPY --from=build /out/browser/wwwroot/ ./browser/
# NorthwindBackend finds Define/ by walking up from the application folder and opens northwind.db next to it, so
# both sit in /app. The app user writes the database there, and the backend may write a framework TableSchema the
# demo does not define yet into Define/, so the user owns both.
COPY --chown=$APP_UID:$APP_UID apps/Polhem.Northwind/Define/ ./Define/
COPY --chown=$APP_UID:$APP_UID apps/Polhem.Northwind/Customize/ ./Customize/
RUN chown $APP_UID:$APP_UID /app
USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Demo
EXPOSE 8080
# Hosting platforms such as Render name the port in PORT; without it the image's default 8080 applies.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet Polhem.Northwind.Blazor.dll"]
