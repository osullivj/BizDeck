# std pkg
import json
import unittest
# 3rd pty
from tornado.testing import gen_test
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestStatusAPI(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.status_url = f'http://localhost:{self.biz_deck_http_port}/api/status'

    @gen_test
    async def test_status_api(self):
        # start BizDeck as child process; same as deplaunch.bat
        # But only if we're configged to do so by BDSTARTSTOP env var
        biz_deck_proc = await self.start_biz_deck()
        self.logger.info(f"test_status_api: HTTP GET {self.status_url}")
        status_response = await self.http_client.fetch(self.status_url)
        self.logger.info(f"test_status_api: retcode[{status_response.code} for {self.status_url}")
        self.assertEqual(status_response.code, 200)
        status_dict = json.loads(status_response.body)
        self.logger.info(f"test_status_api: status_dict:{status_dict}")
        self.assertEqual(status_dict['ButtonSize'], 72)
        self.assertIsNotNone(status_dict)
        # TODO: config.json uses lower case keys. C# JsonSerialization
        # uses camel case style of C# properties.
        # self.assertEqual(self.biz_deck_config['browser_path'], status_dict['BrowserPath'])
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()