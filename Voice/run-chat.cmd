@echo off
REM Find layers by talking: the conversation is kept, so "no, search for X instead" works.
REM Press Enter, speak, press Enter. Type new at the prompt to start over. Ctrl+C to quit.
REM Add --type to type instead of speaking, or --mic "name" to pick a microphone.
cd /d "%~dp0"
python layer_search.py --chat %*
pause
