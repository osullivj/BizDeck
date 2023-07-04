# std pkg
import json
import os
import unittest
import urllib
# 3rd pty
from tornado.testing import gen_test
from tornado.httpclient import HTTPRequest
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestAddButtonAPI(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.add_button_url = f'http://localhost:{self.biz_deck_http_port}/api/add_button'
        payload_path = os.path.join(self.bdtree, 'scripts', 'rest', 'test_add_button1.json')
        with open(payload_path, 'rt') as payload_file:
            self.payload = payload_file.read()
        # specify content type so server knows it's a single payload,
        # not multipart for large files
        # headers = {'Content-Type':"application/x-www-form-urlencoded"}
        headers = {'Content-Type': 'text/plain'}
        self.add_button_request = HTTPRequest(url=self.add_button_url, method='POST',
                                              headers=headers, body=self.payload)

    @gen_test
    async def test_add_button_api(self):
        # start BizDeck as child process; same as deplaunch.bat
        # But only if we're configged to do so by BDSTARTSTOP env var
        biz_deck_proc = await self.start_biz_deck()
        self.logger.info(f"test_add_button_api: HTTP POST {self.add_button_url}")
        add_response = await self.http_client.fetch(self.add_button_request)
        self.logger.info(f"test_add_button_api: retcode[{add_response.code}], body[{add_response.body}] for {self.add_button_url}")
        self.assertEqual(add_response.code, 200)
        # TODO: load the updated config.json to validate
        # self.assertEqual(self.biz_deck_config['browser_path'], config_dict['BrowserPath'])
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()