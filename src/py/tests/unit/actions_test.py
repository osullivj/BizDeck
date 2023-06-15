"""
unit test the BizDeck core actions module
NB this module is loaded by BizDeckPython.cs in the BizDeck server
"""
import unittest
import actions
from mock_logger import Logger
from mock_cache import DataCache
from System.Collections.Generic import Dictionary
from System import Object

class TestActions(unittest.TestCase):

    def setUp(self):
        # BizDeckPython.cs sets the BDRoot and Logger variable in actions.py
        actions.BDRoot = "c:\\osullivj\\src"
        actions.Logger = Logger()
        self.cache = DataCache()

    def test_python_csv_action1(self):
        # here we're simulating the behaviour of BizDeckPython.RunActionFunction
        param_dict = Dictionary[str, Object]()
        param_dict.Add("cache", self.cache)
        param_dict.Add("group", "quandl")
        param_dict.Add("csv", "yield.csv")
        param_dict.Add("row_key", "Date")
        param_dict.Add("max_lines", 5)
        func_name = "add_csv_to_cache_as_dict"
        func = getattr(actions, func_name)
        rv = func(param_dict)
        self.assertEqual(rv, "")
        self.assertTrue("quandl" in self.cache.cache)


if __name__ == '__main__':
    unittest.main()
