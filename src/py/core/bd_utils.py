# std pkg
import logging
import os
# 3rd pty
import psutil


# configure logging to stdout and file
def configure_logging(log_name):
    log_path = f"{log_name}.log"
    bdroot = os.getenv('BDROOT')
    if bdroot:
        log_path = os.path.join(bdroot, "logs", log_path)
    logging.basicConfig(level=logging.INFO,
                    format='%(asctime)s %(message)s',
                    handlers=[logging.FileHandler(log_path),
                              logging.StreamHandler()])
    return logging.getLogger(log_name)


def find_bizdeck_process(exe_name="BizDeckServer.exe"):
    # check there is no running BizDeck process
    for proc in psutil.process_iter(['pid', 'name', 'username']):
        logging.debug(f"ps:{proc.info}")
        if proc.info.get('name') == exe_name:
            return proc.info
    return None


# Abstract BizDeck env handling code so it can be used
# in eg twiddling int_test_config to switch default browsers
# between batches of tests
class ConfigHelper(object):
    def __init__(self):
        self.bdtree = os.getenv("BDTREE")
        self.bdroot = os.getenv("BDROOT")
        self.is_deploy_tree = self.bdtree != self.bdroot
        self.start_stop = int(os.getenv("BDSTARTSTOP", "1"))
        self.log_dir = os.path.join(self.bdroot, "logs")
        self.launch_cfg_path = os.path.join(self.bdtree, 'cfg', 'int_test_config.json')
        self.backup_cfg_path = os.path.join(self.bdtree, 'cfg', 'int_test_config.json_backup')
        self.csv_dir_path = os.path.join(self.bdtree, 'data', 'csv')
