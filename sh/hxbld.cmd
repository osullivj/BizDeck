@echo off
if -%BDROOT%-==-- echo BDROOT env var must be set & exit /b
cd %BDROOT%
haxe --verbose --dce full --class-path %BDROOT%\src\hx html5.hxml