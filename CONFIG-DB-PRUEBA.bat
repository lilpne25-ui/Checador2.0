@echo off
setlocal

cd /d "%~dp0"
call "tools\scripts\SET-DB-MODE.cmd" "ZKBD_PRUEBA" "PRUEBA"
exit /b %errorlevel%

