@echo off
REM ========================================
REM  Land Of The Consumers - Server Launcher
REM ========================================

echo Starting dedicated server...
echo.

REM Configuration
set SERVER_PORT=7777
set MAX_PLAYERS=10
set LOG_FILE=server_log.txt

REM Display configuration
echo Server Configuration:
echo - Port: %SERVER_PORT%
echo - Max Players: %MAX_PLAYERS%
echo - Log File: %LOG_FILE%
echo.
echo Server will start in 3 seconds...
timeout /t 3 /nobreak > nul

REM Start the server
echo Starting server...

REM Use the Server build (headless - no window)
REM If you haven't built a server build yet, see BUILD_SERVER_GUIDE.md
Builds\Server\PerspecitveController.exe -server -port %SERVER_PORT% -maxplayers %MAX_PLAYERS% -logFile "%LOG_FILE%"

REM Alternative: If you don't have a server build, uncomment this line instead:
REM Builds\PerspecitveController.exe -batchmode -nographics -server -port %SERVER_PORT% -maxplayers %MAX_PLAYERS% -logFile "%LOG_FILE%"

REM If server crashes or stops, pause so you can see the error
echo.
echo Server has stopped.
pause
