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
        # Derived class name
        test_name = self.__class__.__name__
        self.logger = configure_logging(test_name)
        self.bdtree = os.getenv("BDTREE")
        self.bdroot = os.getenv("BDROOT")
        self.is_deploy_tree = self.bdtree != self.bdroot
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
        self.backup_cfg_path = os.path.join(self.bdtree, 'cfg', 'int_test_config.json_backup')
        self.result_cfg_path = os.path.join(self.bdtree, 'logs', f'{test_name}.config.json')
        self.csv_dir_path = os.path.join(self.bdtree, 'data', 'csv')
        if os.path.exists(self.csv_dir_path) and self.is_deploy_tree:
            # clean up any downloads from previous tests
            self.logger.info(f'Deleting csv dir:{self.csv_dir_path}')
            shutil.rmtree(self.csv_dir_path)
        # load the config, and make a backup
        self.logger.info(f'Loading config from {self.launch_cfg_path}')
        shutil.copyfile(self.launch_cfg_path, self.backup_cfg_path)
        with open(self.launch_cfg_path, 'rt') as config_file:
            self.biz_deck_config = json.loads(config_file.read())
        self.biz_deck_http_port = self.biz_deck_config.get('http_server_port')
        self.logger.info(f'HTTP port {self.biz_deck_http_port}')
        self.launch_exe_path = os.path.join(self.bdtree, 'bin', 'BizDeckServer.exe')
        self.logger.info(f'Launch exe:{self.launch_exe_path}, path:{self.launch_cfg_path}')
        self.shutdown_url = f'http://localhost:{self.biz_deck_http_port}/api/shutdown'
        self.http_client = AsyncHTTPClient()
        self.files_to_cleanup = []

    def tearDown(self):
        # copy end state config to file named for test so it's available
        # after the test for debugging purposes
        shutil.copyfile(self.launch_cfg_path, self.result_cfg_path)
        # now restore original config
        shutil.copyfile(self.backup_cfg_path, self.launch_cfg_path)
        # delete backup
        os.remove(self.backup_cfg_path)
        # any other cleanups before next test?
        for fpath in self.files_to_cleanup:
            if os.path.exists(fpath):
                os.remove(fpath)

    def add_cleanup_file(self, fpath):
        self.files_to_cleanup.append(fpath)

    def reload_config(self):
        with open(self.launch_cfg_path, 'rt') as config_file:
            return json.loads(config_file.read())

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



