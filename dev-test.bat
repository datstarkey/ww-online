@echo off
REM Thin wrapper - all logic lives in dev-test.ps1 (see its header for flags):
REM   dev-test.bat             build C#, verify game build, launch server + 2 Dolphins + 2 clients
REM   dev-test.bat -Patch      also recompile the C code and re-patch the game first
REM   dev-test.bat -Stop       stop everything and collect logs into logs\latest
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-test.ps1" %*
