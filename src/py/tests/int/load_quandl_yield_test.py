# std pkg
import os
from subprocess import Popen
import unittest
# 3rd pty
from tornado.testing import AsyncTestCase, gen_test, AsyncHTTPClient
from tornado import gen
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestLoadQuandlYield(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.quandl_rates_url = f'http://localhost:{self.biz_deck_http_port}/api/run/actions/quandl_rates'
        self.load_quandl_yield_url = f'http://localhost:{self.biz_deck_http_port}/api/run/actions/load_quandl_yield'
        self.get_quandl_yield_url = f'http://localhost:{self.biz_deck_http_port}/api/cache/quandl/yield_csv'

    @gen_test(timeout=20)
    def test_load_quandl_yield(self):
        # start BizDeck as child process; same as deplaunch.bat
        popen_args = ' '.join([self.launch_exe_path, '--config', self.launch_cfg_path])
        self.logger.info(f'test_load_quandl_yield: args[{popen_args}]')
        biz_deck_proc = Popen(popen_args)
        self.logger.info(f'test_load_quandl_yield: proc[{biz_deck_proc}]')
        # Pause while C# bin starts; IronPy init takes a few secs
        yield gen.sleep(5.0)
        try:
            self.logger.info(f"test_load_quandl_yield: HTTP GET {self.quandl_rates_url}")
            # download from quandl to data/csv
            rates_response = yield self.http_client.fetch(self.quandl_rates_url)
            self.assertEqual(rates_response.code, 200)
            # load data/csv/yield.csv into cache
            load_response = yield self.http_client.fetch(self.load_quandl_yield_url)
            self.assertEqual(load_response.code, 200)
            # get cache contents
            get_response = yield self.http_client.fetch(self.get_quandl_yield_url)
            self.assertEqual(get_response.code, 200)
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