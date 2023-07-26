# std pkgs
import sys
from bd_utils import configure_logging

logger = configure_logging("runsettings")

RUN_SETTINGS = """<?xml version="1.0" encoding="utf-8"?>
<!-- auto generated by {THIS_FILE} -->
<RunSettings>
    <!-- Parameters used by tests at run time -->
    <TestRunParameters>
        <Parameter name="config" value="{CONFIG_PATH}" />
    </TestRunParameters>  
</RunSettings>"""

# generate the XML settings file used by C# unit testing
# to pass in parameters via TestContext C# API. See eg
# src/cs/tests/unit/ConfigTest1.cs Setup method.
def generate_run_settings(target_path, config_path):
    with open(target_path, 'wt') as settings_file:
        settings = RUN_SETTINGS.format(THIS_FILE=__file__, CONFIG_PATH=config_path)
        settings_file.write(settings)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: %VPYTHON% %BDROOT%/src/py/build/generate_run_settings.py <target_path> <config_path>")
    else:
        generate_run_settings(sys.argv[1], sys.argv[2])
