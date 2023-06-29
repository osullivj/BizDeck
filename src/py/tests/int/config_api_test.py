# std pkg
import json
import unittest
# 3rd pty
from tornado.testing import gen_test
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestConfigAPI(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.config_url = f'http://localhost:{self.biz_deck_http_port}/api/config'

    @gen_test
    async def test_config_api(self):
        # start BizDeck as child process; same as deplaunch.bat
        # But only if we're configged to do so by BDSTARTSTOP env var
        biz_deck_proc = await self.start_biz_deck()
        self.logger.info(f"test_config_api: HTTP GET {self.config_url}")
        config_response = await self.http_client.fetch(self.config_url)
        self.logger.info(f"test_config_api: retcode[{config_response.code} for {self.config_url}")
        self.assertEqual(config_response.code, 200)
        config_dict = json.loads(config_response.body)
        self.assertIsNotNone(config_dict)
        self.assertEqual(self.biz_deck_config['ConfigPath'], config_dict['ConfigPath'])
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()