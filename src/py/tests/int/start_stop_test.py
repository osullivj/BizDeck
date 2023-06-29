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

    @gen_test
    async def test_start_stop(self):
        biz_deck_proc = await self.start_biz_deck()
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()