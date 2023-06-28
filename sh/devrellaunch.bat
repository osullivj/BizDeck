@echo off
setlocal
%BDROOT%\src\cs\server\bin\Release\net5.0\BizDeckServer.exe --config %BDROOT%\cfg\config.json
endlocal
