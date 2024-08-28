setlocal
:: BDSTARTSTOP should be 0 or 1
set BDSTARTSTOP=0
:: Tornado AsyncTestCase timeout
set ASYNC_TEST_TIMEOUT=20
%VPYTHON% %BDROOT%\src\py\tests\int\cargo_scraper_test.py
endlocal