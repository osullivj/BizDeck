# std pkgs
import logging
import sys


logging.basicConfig(stream=sys.stdout, level=logging.INFO)
logger = logging.getLogger("runsettings")

RUN_SETTINGS = """<?xml version="1.0" encoding="utf-8"?>
<!-- auto generated by {THIS_FILE} -->
<RunSettings>
    <!-- Parameters used by tests at run time -->
    <TestRunParameters>
        <Parameter name="config" value="{CONFIG_PATH}" />
    </TestRunParameters>  
</RunSettings>"""


def generate_run_settings(target_path, config_path):
    with open(target_path, 'wt') as settings_file:
        settings = RUN_SETTINGS.format(THIS_FILE=__file__, CONFIG_PATH=config_path)
        settings_file.write(settings)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: %VPYTHON% <target_path> <config_path>")
    else:
        generate_run_settings(sys.argv[1], sys.argv[2])