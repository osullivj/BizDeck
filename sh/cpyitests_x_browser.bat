:: Run the int test suite repeatedly with diff
:: default browser settings. NB we change
:: the config in BDTREE, the deploy tree, not
:: BDROOT, the dev tree
setlocal
%VPYTHON% %BDROOT%\src\py\build\set_default_browser.py %BDTREE%\cfg\int_test_config.json chrome
call %BDROOT%\sh\cpyitests.bat
%VPYTHON% %BDROOT%\src\py\build\set_default_browser.py %BDTREE%\cfg\int_test_config.json msedge
call %BDROOT%\sh\cpyitests.bat
%VPYTHON% %BDROOT%\src\py\build\set_default_browser.py %BDTREE%\cfg\int_test_config.json chrome_headless
call %BDROOT%\sh\cpyitests.bat
%VPYTHON% %BDROOT%\src\py\build\set_default_browser.py %BDTREE%\cfg\int_test_config.json msedge_headless
call %BDROOT%\sh\cpyitests.bat
endlocal