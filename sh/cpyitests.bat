setlocal
:: BDSTARTSTOP should be 0 or 1
set BDSTARTSTOP=1
:: Tornado AsyncTestCase timeout
set ASYNC_TEST_TIMEOUT=20
%VPYTHON% %BDROOT%\src\py\tests\int\start_stop_test.py
%VPYTHON% %BDROOT%\src\py\tests\int\config_api_test.py
%VPYTHON% %BDROOT%\src\py\tests\int\quandl_rates_test.py
%VPYTHON% %BDROOT%\src\py\tests\int\load_quandl_yield_test.py
endlocal