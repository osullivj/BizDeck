# std pkg
import os
import json
import shutil
from subprocess import Popen
# 3rd pty
from tornado.testing import AsyncTestCase, AsyncHTTPClient
from tornado import gen
from bd_utils import configure_logging, find_bizdeck_process


# Base test case for our int tests
# use droot.bat dev env vars to discover the config in the deploy tree
class BizDeckIntTestCase(AsyncTestCase):

    def setUp(self):
        super().setUp()
        self.logger = configure_logging(self.__class__.__name__)
        self.bdtree = os.getenv("BDTREE")
        self.start_stop = int(os.getenv("BDSTARTSTOP", "1"))
        # check there is no running BizDeck process
        if self.start_stop:
            proc_info = find_bizdeck_process()
            if proc_info:
                error = f"BizDeck already running: ss[{self.start_stop}], {proc_info}"
                self.logger.error(error)
                raise Exception(error)
        # read deploy tree config to discover port. Exceptions for
        # missing env vars are fine here from the test behaviour and
        # result POV
        self.biz_deck_config = dict()
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

    async def start_biz_deck(self):
        if not self.start_stop:
            return None
        popen_args = ' '.join([self.launch_exe_path, '--config', self.launch_cfg_path])
        self.logger.info(f'start_biz_deck: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        self.logger.info(f'start_biz_deck: proc[{biz_deck_proc}]')
        await gen.sleep(5)
        return biz_deck_proc

    async def stop_biz_deck(self, biz_deck_proc):
        if not self.start_stop or not biz_deck_proc:
            return
        try:
            shutdown_response = await self.http_client.fetch(self.shutdown_url)
            # pause again for BizDeckServer.exe to exit...
            retcode = biz_deck_proc.wait(timeout=5)
            self.logger.info(f'stop_biz_deck: popen retcode:{retcode}')
            # retcode=None means the proc is still running
            self.assertNotEqual(retcode, None)
        except ConnectionResetError as ex:
            self.logger.info(f'stop_biz_deck: {ex}')
            self.logger.info(f'stop_biz_deck: BizDeckServer.exe terminated before serving shutdown response')



