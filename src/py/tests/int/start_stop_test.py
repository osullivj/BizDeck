# std pkg
from subprocess import Popen
import unittest
# 3rd pty
from tornado.testing import AsyncTestCase, gen_test, AsyncHTTPClient
from tornado import gen
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestStartStop(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()

    @gen_test(timeout=15)
    def test_start_stop(self):
        # start BizDeck as child process; same as deplaunch.bat
        popen_args = ' '.join([self.launch_exe_path, '--config', self.launch_cfg_path])
        self.logger.info(f'test_start_stop: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        self.logger.info(f'test_start_stop: proc[{biz_deck_proc}]')
        # Pause while C# bin starts; IronPy init takes a few secs
        yield gen.sleep(5.0)
        try:
            response = yield self.http_client.fetch(self.shutdown_url)
        except ConnectionResetError as ex:
            self.logger.info(f'test_start_stop: {ex}')
            self.logger.info(f'test_start_stop: BizDeckServer.exe terminated before serving shutdown response')
        # pause again for BizDeckServer.exe to exit...
        retcode = biz_deck_proc.wait(timeout=5)
        self.logger.info(f'test_start_stop: popen retcode:{retcode}')
        # retcode=None means the proc is still running
        self.assertNotEqual(retcode, None)


if __name__ == "__main__":
    unittest.main()