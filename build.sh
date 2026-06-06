#!/bin/bash
find "/Volumes/SSD Manoel/4_Trabalhos/SmartLine" -name "._*" -delete
cd backend
dotnet build
