FROM mcr.microsoft.com/dotnet/sdk:5.0

RUN mkdir /var/app
RUN mkdir /var/app/data
RUN mkdir /var/app/logs
RUN mkdir /var/app/config

VOLUME ["/var/app/config"]
VOLUME ["/var/app/data"]
VOLUME ["/var/app/logs"]

COPY ./bin/release/net5.0/publish /var/app

# Set the default command to execute when creating a new container
WORKDIR /var/app/
ENTRYPOINT dotnet BikeDataProject.Integrations.Fitbit.dll