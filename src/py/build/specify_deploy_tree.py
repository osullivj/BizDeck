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
        'http_formats.json': 'cfg/http_formats.json',
        'secrets.json': 'cfg/secrets.json'
    },
    'bin/': {
        '*': 'src/cs/server/bin/Release/net5.0/*.dll',
        '*': 'src/cs/server/bin/Release/net5.0/*.dll',
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
            'quandl_rates.json': 'scripts/actions/quandl_rates.json'
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
