@echo off
setlocal

cd /d "%~dp0"
call "tools\scripts\SET-DB-MODE.cmd" "ZKBD" "REAL"
exit /b %errorlevel%

