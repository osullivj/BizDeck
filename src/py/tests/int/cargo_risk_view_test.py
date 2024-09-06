# std pkg
import json
import os
import unittest
# 3rd pty
from tornado.testing import gen_test
from tornado import gen
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestCargoRiskView(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.iterate_url = f'http://localhost:{self.biz_deck_http_port}/api/run/actions/cargo_risk_iterate'

    @gen_test
    async def test_cargo_risk_view(self):
        biz_deck_proc = await self.start_biz_deck()
        # websock client connection so we can see the GUI updates
        await self.connect_websock()
        self.logger.info(f"test_cargo_risk_view: HTTP GET {self.iterate_url}")
        iter_response = await self.http_client.fetch(self.iterate_url)
        self.logger.info(f"test_cargo_risk_view: retcode[{iter_response.code}], body[{iter_response.body}], for {self.iterate_url}")
        self.assertEqual(iter_response.code, 200)
        # wait to allow incoming websock msgs
        await gen.sleep(2)
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()