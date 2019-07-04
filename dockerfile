FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS builder

ARG tmdbApiKey
ENV envTmdbApiKey=$tmdbApiKey

RUN apt-get update
RUN apt-get install -y unzip libunwind8 gettext
RUN mkdir DeathClock
ADD . /FilmLister
WORKDIR /FilmLister
RUN ./cake.sh -target=PublishWebUIDocker

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
COPY --from=builder /FilmLister/FilmLister.WebUI/bin/Release/netcoreapp2.2/publish/ /FilmLister/
WORKDIR /FilmLister
ENTRYPOINT dotnet FilmLister.WebUI.dll --TmdbApiKey $envTmdbApiKey
