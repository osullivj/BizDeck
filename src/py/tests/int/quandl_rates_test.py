# std pkg
import os
from subprocess import Popen
import unittest
# 3rd pty
from tornado.testing import AsyncTestCase, gen_test, AsyncHTTPClient
from tornado import gen
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestQuandlRates(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.quandl_rates_url = f'http://localhost:{self.biz_deck_http_port}/api/run/actions/quandl_rates'

    @gen_test(timeout=15)
    def test_quandl_rates(self):
        # start BizDeck as child process; same as deplaunch.bat
        popen_args = ' '.join([self.launch_exe_path, '--config', self.launch_cfg_path])
        self.logger.info(f'test_quandl_rates: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        self.logger.info(f'test_quandl_rates: proc[{biz_deck_proc}]')
        # Pause while C# bin starts; IronPy init takes a few secs
        yield gen.sleep(5.0)
        try:
            self.logger.info(f"test_quandl_rates: HTTP GET {self.quandl_rates_url}")
            rates_response = yield self.http_client.fetch(self.quandl_rates_url)
            self.assertEqual(rates_response.code, 200)
            for csv_name in ['yield', 'ded3', 'dswp10']:
                csv_path = os.path.join(self.csv_dir_path, f'{csv_name}.csv')
                self.assertEqual(os.path.exists(csv_path), True)
            shutdown_response = yield self.http_client.fetch(self.shutdown_url)
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