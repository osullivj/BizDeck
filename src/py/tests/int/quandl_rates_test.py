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

    @gen_test
    async def test_quandl_rates(self):
        biz_deck_proc = await self.start_biz_deck()
        self.logger.info(f"test_quandl_rates: HTTP GET {self.quandl_rates_url}")
        rates_response = await self.http_client.fetch(self.quandl_rates_url)
        self.assertEqual(rates_response.code, 200)
        for csv_name in ['yield', 'ded3', 'dswp10']:
            csv_path = os.path.join(self.csv_dir_path, f'{csv_name}.csv')
            self.assertEqual(os.path.exists(csv_path), True)
        # wait to allow async actions in svr to complete
        await gen.sleep(2)
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()