@echo off
if -%BDROOT%-==-- echo BDROOT env var must be set & exit /b
killall /IM msedge.exe
"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe" --remote-debugging-port=9222 --user-data-dir c:\osullivj\src\BizDeck\logs
