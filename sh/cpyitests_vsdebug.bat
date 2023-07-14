:: Boilerplate for launching a CPython int test against
:: a BizDeckServer that's running in the debugger
:: setlocal to prevent env var changes leaking into
:: your DOS box dev session
setlocal
:: int tests use the deploy tree root (BDTREE) not
:: the dev tree root (BDROOT) to find the config
:: and thereby figure out the port
set BDTREE=%BDROOT%
:: BDSTARTSTOP should be 0 or 1
set BDSTARTSTOP=0
:: Tornado AsyncTestCase timeout
set ASYNC_TEST_TIMEOUT=3600
%VPYTHON% %BDROOT%\src\py\tests\int\book_scraper_test.py
endlocal