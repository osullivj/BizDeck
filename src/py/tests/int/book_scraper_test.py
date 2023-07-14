# std pkg
import json
import os
import unittest
# 3rd pty
from tornado.testing import gen_test
from tornado import gen
# bizdeck
from async_test_utils import BizDeckIntTestCase


class TestBookScraper(BizDeckIntTestCase):

    def setUp(self):
        super().setUp()
        self.scraper_url = f'http://localhost:{self.biz_deck_http_port}/api/run/steps/book_scraper'
        self.api_get_scraped_books_url = f'http://localhost:{self.biz_deck_http_port}/api/cache/scraped/books'
        self.xl_get_scraped_books_url = f'http://localhost:{self.biz_deck_http_port}/excel/scraped/books'

    @gen_test
    async def test_book_scraper(self):
        biz_deck_proc = await self.start_biz_deck()
        # websock client connection so we can see the GUI updates
        await self.connect_websock()
        self.logger.info(f"test_book_scraper: HTTP GET {self.scraper_url}")
        # run the scraper script
        scraper_response = await self.http_client.fetch(self.scraper_url)
        self.logger.info(f"test_book_scraper: retcode[{scraper_response.code}], body[{scraper_response.body}], for {self.scraper_url}")
        self.assertEqual(scraper_response.code, 200)
        # get cache contents
        self.logger.info(f"test_book_scraper: HTTP REST {self.api_get_scraped_books_url}")
        api_get_response = await self.http_client.fetch(self.api_get_scraped_books_url)
        self.logger.info(f"test_book_scraper: retcode[{api_get_response.code} for {self.api_get_scraped_books_url}")
        self.assertEqual(api_get_response.code, 200)
        # turn the response into a py obj
        scrape_csv = json.loads(api_get_response.body)
        self.logger.info("test_book_scraper: {type}, {count} rows, {row_key} key".format(**scrape_csv))
        self.assertEqual(scrape_csv['type'], 'RegularCSV')
        # now check the Excel features: the /excel/scraped/books_csv URL for an Excel friendly
        # HTML table, and the creation of scripts/excel/scraped_books_csv.iqy
        self.logger.info(f"test_book_scraper: Excel HTML table {self.xl_get_scraped_books_url}")
        xl_get_response = await self.http_client.fetch(self.xl_get_scraped_books_url)
        self.logger.info(f"test_book_scraper: retcode[{xl_get_response.code} for {self.xl_get_scraped_books_url}")
        self.assertEqual(xl_get_response.code, 200)
        self.assertFalse(b'No cached data' in xl_get_response.body)
        # self.assertTrue(b'Key' in xl_get_response.body)
        # self.assertTrue(b'1MO' in xl_get_response.body)
        iqy_path = os.path.join(self.bdtree, 'scripts', 'excel', 'scraped_books.iqy')
        self.assertTrue(os.path.exists(iqy_path))
        # wait to allow incoming websock msgs
        await gen.sleep(2)
        await self.stop_biz_deck(biz_deck_proc)


if __name__ == "__main__":
    unittest.main()