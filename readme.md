# FBreezy

A work-in-progress project to extract data from the [breezy.hr API](https://developer.breezy.hr/docs/overview), to allow analysing [Ghyston's](https://ghyston.com) recruitment stats.

Written in F#

To run:
- Run `docker-compose up -d` to create the postgres DB
- In `src/Migrations`, run `dotnet run -- --clean` to clean and initialise the DB schema
- In `src/BreezyConsole`, run `dotnet run -- <email> <password>`, passing in your Breezy login details