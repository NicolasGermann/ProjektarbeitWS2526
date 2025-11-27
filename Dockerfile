FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

COPY . /wordir

RUN mkdir /out

WORKDIR /wordir

RUN dotnet publish --sc true -o '/out'

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS run

COPY --from=build /out/. /run 
# COPY ./xmldata/testdata.xml /xml

RUN mkdir /xml

WORKDIR /run

CMD /run/Projektarbeit
