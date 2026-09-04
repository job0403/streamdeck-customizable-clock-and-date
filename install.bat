@echo off
echo =======================================================
echo  Deploying Stream Deck Clock ^& Calendar Plugin (Low RAM)
echo =======================================================
echo.

set TARGET_DIR="%APPDATA%\Elgato\StreamDeck\Plugins\com.job0403.clock.sdPlugin"

echo Destination: %TARGET_DIR%
echo.

:: Delete old directory if it exists to clean up obsolete UI files
if exist %TARGET_DIR% (
    echo Cleaning up old plugin deployment files...
    rmdir /s /q %TARGET_DIR%
)

echo Creating target directory...
mkdir %TARGET_DIR%

echo Copying plugin files...
:: /E copies subdirectories (including empty ones)
:: /Y suppresses prompts to overwrite existing files
:: /I creates target directories if they don't exist
xcopy "%~dp0*" %TARGET_DIR% /E /Y /I

echo.
echo Restarting Stream Deck application...

:: Stop the Stream Deck software
taskkill /f /im StreamDeck.exe 2>nul

:: Wait 1.5 seconds for it to fully terminate
timeout /t 2 /nobreak >nul

:: Start the Stream Deck software again in the background
start "" "C:\Program Files\Elgato\StreamDeck\StreamDeck.exe"

echo.
echo =======================================================
echo  SUCCESS! Clean low-RAM clock plugin deployed.
echo =======================================================
echo.
pause
