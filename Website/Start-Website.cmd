@echo off
setlocal
cd /d "%~dp0"
set "BACKPACK_NODE=node"
where node >nul 2>nul
if not errorlevel 1 goto check_dependencies
set "BACKPACK_NODE=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe"
if exist "%BACKPACK_NODE%" goto check_dependencies
echo Node.js was not found. Install Node.js 22.12+ and try again.
pause
exit /b 1

:check_dependencies
if exist "node_modules\vite\bin\vite.js" goto start_site
echo Dependencies are missing. Run pnpm install --frozen-lockfile in this folder.
pause
exit /b 1

:start_site
echo Starting Backpack. Keep this window open while viewing the website.
echo Press Ctrl+C to stop the local server.
"%BACKPACK_NODE%" "node_modules\vite\bin\vite.js" --host 127.0.0.1 --open
if errorlevel 1 pause
