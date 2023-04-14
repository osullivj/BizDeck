@echo off
if -%BDROOT%-==-- echo BDROOT env var must be set & exit /b
cd %BDROOT%
copy %BDROOT%\src\hx\index.html %BDROOT%\html
copy %BDROOT%\icons\favicon.ico %BDROOT%\html
haxe --verbose --dce full --class-path %BDROOT%\src\hx html5.hxml
