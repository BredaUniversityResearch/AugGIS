@echo off
REM Runs the demo with the sample layer names. The mic is chosen automatically:
REM voice.py skips virtual devices and takes the first real one. Check the line
REM starting `mic` is the device you are speaking into, and see START-FROM-SCRATCH.txt
REM if it picked wrong.
cd /d "%~dp0"
python voice.py --layers "Population,Bathymetry,Shipping lanes,Wind farms"
pause
