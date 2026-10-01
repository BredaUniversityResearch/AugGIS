@echo off
REM Lists every layer on the team's GeoServer and on EMODnet, then finds layers by voice.
REM Press Enter, speak, press Enter. Add --type to type requests instead of speaking.
cd /d "%~dp0"
python layer_search.py %*
pause
