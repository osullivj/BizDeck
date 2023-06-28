# std pkg
import os
import json
import shutil
# 3rd pty
from tornado.testing import AsyncTestCase, AsyncHTTPClient
from bd_utils import configure_logging, find_bizdeck_process


# Base test case for our int tests
# use droot.bat dev env vars to discover the config in the deploy tree
class BizDeckIntTestCase(AsyncTestCase):

    def setUp(self):
        super().setUp()
        self.logger = configure_logging(self.__class__.__name__)
        # check there is no running BizDeck process
        proc_info = find_bizdeck_process()
        if proc_info:
            error = f"BizDeck already running: {proc_info}"
            self.logger.error(error)
            raise Exception(error)
        # read deploy tree config to discover port. Exceptions for
        # missing env vars are fine here from the test behaviour and
        # result POV
        self.biz_deck_config = dict()
        self.bdtree = os.environ["BDTREE"]
        self.launch_cfg_path = os.path.join(self.bdtree, 'cfg', 'int_test_config.json')
        self.csv_dir_path = os.path.join(self.bdtree, 'data', 'csv')
        if os.path.exists(self.csv_dir_path):
            # clean up any downloads from previous tests
            self.logger.info(f'Deleting csv dir:{self.csv_dir_path}')
            shutil.rmtree(self.csv_dir_path)
        self.logger.info(f'Loading config from {self.launch_cfg_path}')
        with open(self.launch_cfg_path, 'rt') as config_file:
            self.biz_deck_config = json.loads(config_file.read())
        self.biz_deck_http_port = self.biz_deck_config.get('http_server_port')
        self.logger.info(f'HTTP port {self.biz_deck_http_port}')
        self.launch_exe_path = os.path.join(self.bdtree, 'bin', 'BizDeckServer.exe')
        self.logger.info(f'Launch exe:{self.launch_exe_path}, path:{self.launch_cfg_path}')
        self.shutdown_url = f'http://localhost:{self.biz_deck_http_port}/api/shutdown'
        self.http_client = AsyncHTTPClient()
