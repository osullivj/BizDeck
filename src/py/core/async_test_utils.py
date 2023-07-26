# std pkg
import os
import json
import shutil
from subprocess import Popen
# 3rd pty
from tornado.testing import AsyncTestCase, AsyncHTTPClient
from tornado.websocket import websocket_connect
from tornado import gen
import tornado.platform
from bd_utils import configure_logging, find_bizdeck_process, ConfigHelper


# Base test case for our int tests
# use droot.bat dev env vars to discover the config in the deploy tree
class BizDeckIntTestCase(AsyncTestCase):

    def setUp(self):
        super().setUp()
        # Derived class name
        self.test_name = self.__class__.__name__
        self.logger = configure_logging(self.test_name)
        self.ch = ConfigHelper()
        # check there is no running BizDeck process
        if self.ch.start_stop:
            proc_info = find_bizdeck_process()
            if proc_info:
                error = f"BizDeck already running: ss[{self.ch.start_stop}], {proc_info}"
                self.logger.error(error)
                raise Exception(error)
        # read deploy tree config to discover port. Exceptions for
        # missing env vars are fine here from the test behaviour and
        # result POV
        self.biz_deck_config = dict()
        self.result_cfg_path = os.path.join(self.ch.bdtree, 'logs', f'{self.test_name}.config.json')
        self.csv_dir_path = os.path.join(self.ch.bdtree, 'data', 'csv')
        if os.path.exists(self.csv_dir_path) and self.ch.is_deploy_tree:
            # clean up any downloads from previous tests
            self.logger.info(f'Deleting csv dir:{self.csv_dir_path}')
            shutil.rmtree(self.csv_dir_path)
        # load the config, and make a backup
        self.logger.info(f'Loading config from {self.ch.launch_cfg_path}')
        shutil.copyfile(self.ch.launch_cfg_path, self.ch.backup_cfg_path)
        with open(self.ch.launch_cfg_path, 'rt') as config_file:
            self.biz_deck_config = json.loads(config_file.read())
        self.biz_deck_http_port = self.biz_deck_config.get('http_server_port')
        self.logger.info(f'HTTP port {self.biz_deck_http_port}')
        self.launch_exe_path = os.path.join(self.ch.bdtree, 'bin', 'BizDeckServer.exe')
        self.logger.info(f'Launch exe:{self.launch_exe_path}, path:{self.ch.launch_cfg_path}')
        self.shutdown_url = f'http://localhost:{self.biz_deck_http_port}/api/shutdown'
        self.websock_url = f'ws://localhost:{self.biz_deck_http_port}/ws'
        self.http_client = AsyncHTTPClient()
        self.files_to_cleanup = []
        self.websock_messages = []

    def tearDown(self):
        # copy end state config to file named for test so it's available
        # after the test for debugging purposes
        shutil.copyfile(self.ch.launch_cfg_path, self.result_cfg_path)
        # now restore original config
        shutil.copyfile(self.ch.backup_cfg_path, self.ch.launch_cfg_path)
        # delete backup
        os.remove(self.ch.backup_cfg_path)
        # any other cleanups before next test?
        for fpath in self.files_to_cleanup:
            if os.path.exists(fpath):
                os.remove(fpath)
        self.logger.info("tearDown: %d websock messages recved" % len(self.websock_messages))
        for inx, msg_dict in enumerate(self.websock_messages):
            mtype = msg_dict.get('type', 'notype')
            mname = f'{self.test_name}_{mtype}_{inx}.json'
            mpath = os.path.join(self.ch.log_dir, mname)
            with open(mpath, 'wt') as mfile:
                mfile.write(json.dumps(msg_dict))

    def add_cleanup_file(self, fpath):
        self.files_to_cleanup.append(fpath)

    def reload_config(self):
        with open(self.ch.launch_cfg_path, 'rt') as config_file:
            return json.loads(config_file.read())

    async def connect_websock(self):
        self.websock = await websocket_connect(self.websock_url,
                            on_message_callback=self.on_websock_message, subprotocols=['json'])

    def on_websock_message(self, msg):
        msg_dict = None
        try:
            msg_dict = json.loads(msg)
        except Exception as ex:
            self.logger.error("on_websock_message: JSON exception:%s" % str(ex))
            self.logger.error("on_websock_message: msg(%s)" % msg)
            # reraise the Exception to fail the test on bad json
            raise ex
        self.websock_messages.append(msg_dict)
        self.logger.info('on_websock_message: type(%s)' % msg_dict.get('type'))

    async def start_biz_deck(self):
        if not self.ch.start_stop:
            return None
        popen_args = ' '.join([self.launch_exe_path, '--config', self.ch.launch_cfg_path])
        self.logger.info(f'start_biz_deck: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        self.logger.info(f'start_biz_deck: proc[{biz_deck_proc}]')
        await gen.sleep(5)
        return biz_deck_proc

    async def stop_biz_deck(self, biz_deck_proc):
        if not self.ch.start_stop or not biz_deck_proc:
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



