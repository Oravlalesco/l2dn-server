@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0telemetry-snapshot.ps1" %*
