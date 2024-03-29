:: change these to suit your env
set BDROOT=C:\osullivj\src\BizDeck
set BDTREE=C:\osullivj\bld\BizDeck
set HXROOT=C:\osullivj\bld\HaxeToolkit
set IRONPYTHON=c:\osullivj\bin\irnpy340\ipy.exe
set CPYTHON=c:\osullivj\bin\py311x64\python.exe
set NUGET=c:\osullivj\bin\nuget.exe
set VSTEST="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
:: CPython int tests can be configged to start and stop BizDeck
:: Useful for switching between running them against a BDTREE deploy tree
:: and running them against a VS debug session
set BDSTARTSTOP=1
:: We don't set timeouts with @gen_test(timeout=) because that hardwires
:: 20 works running against the dep tree with start stop
:: NB cpyitests_vsdebug.bat sets it higher so the test code
:: will wait while debugging C# in VS
set ASYNC_TEST_TIMEOUT=20
:: you should not need to change the lines below
set VPYTHON=%BDROOT%\venv\scripts\python.exe
set PYTHONPATH=%BDROOT%\src\py\build;%BDROOT%\src\py\core
set IRONPYTHONPATH=%BDROOT%\src\py\core;%BDROOT%\src\py\mock
set PATH=%PATH%;%HXROOT%\haxe;%HXROOT%\neko
cd %BDROOT%/sh