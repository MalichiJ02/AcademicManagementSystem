#!/usr/bin/env bash

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://0.0.0.0:5100

dotnet run --project src/AcademicManagementSystem.Web --no-launch-profile
