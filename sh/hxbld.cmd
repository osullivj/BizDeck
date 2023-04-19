@echo off
if -%BDROOT%-==-- echo BDROOT env var must be set & exit /b
mkdir %BDROOT%\html\icons
copy %BDROOT%\src\hx\index.html %BDROOT%\html
copy %BDROOT%\icons\favicon.ico %BDROOT%\html
copy %BDROOT%\icons\*.png %BDROOT%\html\icons
cd %BDROOT%
haxe --verbose --dce full --class-path %BDROOT%\src\hx html5.hxml
