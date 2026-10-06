# Build app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0-alpine AS build
ARG TARGETARCH

RUN apk add --update --no-cache bash

SHELL ["/bin/bash", "-c"]

RUN echo linux-${TARGETARCH/amd/x} > ~/.RuntimeIdentifier

WORKDIR /app

COPY . ./

RUN dotnet publish -r $(cat ~/.RuntimeIdentifier) -c Release --no-self-contained

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:11.0

WORKDIR /app

COPY --from=build /app/out-linux-* .

WORKDIR /data

ENTRYPOINT ["/app/AssettoServer", "--plugins-from-workdir"]
