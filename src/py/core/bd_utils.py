# std pkg
import logging
# 3rd pty
import psutil


# configure logging to stdout and file
def configure_logging(log_name):
    logging.basicConfig(level=logging.INFO,
                    format='%(asctime)s %(message)s',
                    handlers=[logging.FileHandler(f"{log_name}.log"),
                              logging.StreamHandler()])
    return logging.getLogger(log_name)


def find_bizdeck_process(exe_name="BizDeckServer.exe"):
    # check there is no running BizDeck process
    for proc in psutil.process_iter(['pid', 'name', 'username']):
        logging.debug(f"ps:{proc.info}")
        if proc.info.get('name') == exe_name:
            return proc.info
    return None
