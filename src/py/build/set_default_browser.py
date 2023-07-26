# std pkgs
import json
import sys
# BizDeck
from bd_utils import configure_logging, ConfigHelper

logger = configure_logging("set_default_browser")


def set_default_browser(config_path, browser_key):
    logger.info(f'Setting default_browser={browser_key} in {config_path}')
    config_dict = dict()
    with open(config_path, 'rt') as config_file:
        config_dict =  json.loads(config_file.read())
        config_file.close()
    config_dict["default_browser"] = browser_key
    with open(config_path, 'wt') as config_file:
        config_json =  json.dumps(config_dict, indent=4)
        config_file.write(config_json)
        config_file.close()


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: %VPYTHON% %BDROOT%/src/py/build/set_default_browser.py <config_path> <browser_key>")
    else:
        set_default_browser(sys.argv[1], sys.argv[2])
