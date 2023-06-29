# std pkg
import json
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

    @gen_test
    async def test_load_quandl_yield(self):
        biz_deck_proc = await self.start_biz_deck()
        self.logger.info(f"test_load_quandl_yield: HTTP GET {self.quandl_rates_url}")
        # download from quandl to data/csv
        rates_response = await self.http_client.fetch(self.quandl_rates_url)
        self.logger.info(f"test_load_quandl_yield: retcode[{rates_response.code} for {self.quandl_rates_url}")
        self.assertEqual(rates_response.code, 200)
        # load data/csv/yield.csv into cache
        self.logger.info(f"test_load_quandl_yield: LOAD {self.load_quandl_yield_url}")
        load_response = await self.http_client.fetch(self.load_quandl_yield_url)
        self.logger.info(f"test_load_quandl_yield: retcode[{load_response.code} for {self.load_quandl_yield_url}")
        self.assertEqual(load_response.code, 200)
        # get cache contents
        self.logger.info(f"test_load_quandl_yield: HTTT REST {self.get_quandl_yield_url}")
        get_response = await self.http_client.fetch(self.get_quandl_yield_url)
        self.logger.info(f"test_load_quandl_yield: retcode[{get_response.code} for {self.get_quandl_yield_url}")
        self.assertEqual(get_response.code, 200)
        # turn the response into a py obj
        quandl_csv = json.loads(get_response.body)
        self.logger.info("test_load_quandl_yield: {type}, {count} rows, {row_key} key".format(**quandl_csv))
        self.assertEqual(quandl_csv['type'], 'PrimaryKeyCSV')
        self.assertEqual(quandl_csv['row_key'], 'Date')
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()