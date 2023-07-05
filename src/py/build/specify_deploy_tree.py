# Declarative map of a BizDeck deploy tree (a prod install)
# "git clone" gives us a source tree we can use to build
# C# bins and Haxe JS. Our end product is a zip, designed
# to unzip under %LOCALAPPDATA%. So we declare the deploy
# tree and provide sources for each tree element.
# Note that all paths are relative. Deploy tree paths
# are relative to the build_deploy_tree.py cmd line parm.
# Source tree paths are relative to BDROOT.

DEPLOY_TREE = {
    'cfg/': {
        'config.json': 'cfg/config.json',
        'int_test_config.json': 'cfg/int_test_config.json',
        'http_formats.json': 'cfg/http_formats.json',
        'secrets.json': 'cfg/secrets.json'
    },
    'bin/': {
        '*.dll': 'src/cs/server/bin/Release/net5.0/*.dll',
        '*.exe': 'src/cs/server/bin/Release/net5.0/*.exe',
        'BizDeckServer.runtimeconfig.json': 'src/cs/server/bin/Release/net5.0/BizDeckServer.runtimeconfig.json',
        'BizDeckServer.deps.json': 'src/cs/server/bin/Release/net5.0/BizDeckServer.deps.json',
        'lib:': 'src/cs/server/bin/Debug/net5.0/lib',
        'runtimes/': {
            'win:': 'src/cs/server/bin/Release/net5.0/runtimes/win'
        },
    },
    'data/': {},
    'doc/': {},
    'icons/': {
        '*': 'icons/*.png'
    },
    'html/': {
        'favicon.ico': 'icons/favicon.ico',
        'index.html': 'html/index.html',
        'Main.js': 'html/main.js'
    },
    'logs/': {},
    'scripts/': {
        'actions/': {
            'quandl_rates.json': 'scripts/actions/quandl_rates.json',
            'load_quandl_yield.json': 'scripts/actions/load_quandl_yield.json',
        },
        'apps/': {
            'excel.json': 'scripts/apps/excel.json',
            'word.json': 'scripts/apps/word.json'
        },
        'excel/': {},
        'py/': {
            'generate_quandl_ycb_download_script.py': 'scripts/py/generate_quandl_ycb_download_script.py'
        },
        'steps/': {
            'google.json': 'scripts/steps/google.json',
            'tiingo_gui_login.json': 'scripts/steps/tiingo_gui_login.json',
        },
        'rest/': {
            'test_add_button1.json': 'scripts/rest/test_add_button1.json',
            'test_add_button2.json': 'scripts/rest/test_add_button2.json',
            'test_add_button3.json': 'scripts/rest/test_add_button3.json'
        }
    },
    'src/': {
        'py/': {
            'core/': {
                'actions.py': 'src/py/core/actions.py'
            }
        }
    }
}
