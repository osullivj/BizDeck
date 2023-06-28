# std pkg
import os
import json
import logging
from subprocess import Popen
import unittest
# 3rd pty
from tornado.testing import AsyncTestCase, gen_test, AsyncHTTPClient
from tornado import gen
from tornado.httpclient import HTTPClient
import psutil

# configure logging to stdout and file
logging.basicConfig(level=logging.INFO,
                    handlers=[logging.FileHandler("start_stop_test.log"),
                              logging.StreamHandler()])
logger = logging.getLogger("start_stop_test")


# use droot.bat dev env vars to discover the config
# in the deploy tree
class TestStartStop(AsyncTestCase):

    def setUp(self):
        super().setUp()
        # check there is no running BizDeck process
        for proc in psutil.process_iter(['pid', 'name', 'username']):
            logger.info(f"ps:{proc.info}")
            if proc.info.get('name') == "BizDeck.exe":
                raise Exception(f"BizDeck already running: {proc.info}")
        # read deploy tree config to discover port. Exceptions for
        # missing env vars are fine here from the test behaviour and
        # result POV
        self.biz_deck_config = dict()
        self.bdtree = os.environ["BDTREE"]
        config_path = os.path.join(self.bdtree, 'cfg', 'config.json')
        logger.info(f'Loading config from {config_path}')
        with open(config_path, 'rt') as config_file:
            self.biz_deck_config = json.loads(config_file.read())
        self.biz_deck_http_port = self.biz_deck_config.get('http_server_port')
        logger.info(f'HTTP port {self.biz_deck_http_port}')
        self.launch_exe_path = os.path.join(self.bdtree, 'bin', 'BizDeckServer.exe')
        self.launch_cfg_path = os.path.join(self.bdtree, 'cfg', 'config.json')
        logger.info(f'Launch exe:{self.launch_exe_path}, path:{self.launch_cfg_path}')
        self.shutdown_url = f'http://localhost:{self.biz_deck_http_port}/api/shutdown'
        self.http_client = AsyncHTTPClient()


    @gen_test(timeout=15)
    def test_start_stop(self):
        # start BizDeck as child process; same as deplaunch.bat
        popen_args = ' '.join([self.launch_exe_path, '--config', self.launch_cfg_path])
        logger.info(f'test_start_stop: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        logger.info(f'test_start_stop: proc[{biz_deck_proc}]')
        # Pause while C# bin starts; IronPy init takes a few secs
        yield gen.sleep(5.0)
        try:
            response = yield self.http_client.fetch(self.shutdown_url)
        except ConnectionResetError as ex:
            logger.info(f'test_start_stop: {ex}')
            logger.info(f'test_start_stop: BizDeckServer.exe terminated before serving shutdown response')
        # pause again for BizDeckServer.exe to exit...
        retcode = biz_deck_proc.wait(timeout=5)
        logger.info(f'test_start_stop: popen retcode:{retcode}')
        # retcode=None means the proc is still running
        self.assertNotEqual(retcode, None)


if __name__ == "__main__":
    unittest.main()