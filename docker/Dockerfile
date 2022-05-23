# Build app
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build

RUN apk add --update --no-cache bash

SHELL ["/bin/bash", "-c"]

ARG TARGETARCH
RUN echo linux-${TARGETARCH/amd/x} > ~/.RuntimeIdentifier

WORKDIR /app

COPY . ./

RUN dotnet publish -r $(cat ~/.RuntimeIdentifier) -c Release --no-self-contained

# Build runtime image based on alpine
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

WORKDIR /app

COPY --from=build /app/out-linux-* .

RUN apk add --update --no-cache libc6-compat

ENTRYPOINT ["./AssettoServer"]
