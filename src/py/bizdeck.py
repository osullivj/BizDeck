# std py pkgs provided by IronPython
import csv
import json
import os
import os.path
import sys
# std .Net packages
# We're running under IronPython: import .Net generic
# collections so we can pass back native C# types.
# https://stackoverflow.com/questions/5209675/passing-lists-from-ironpython-to-c-sharp
# https://ironpython.net/documentation/dotnet/dotnet.html#accessing-generic-types
from System.Collections.Generic import List, Dictionary
from System import DateTime

# BizDeck utilities

# Global variables set by BizDeckPython.cs
BDRoot = None
Logger = None

# Insert a CSV into the BizDeck cache as a dict
# cache_location: the root key in the cache eg "quandl"
# csv_file_name: name of file in %BDROOT% to load
# key: which field is used as the key field
# headers: if the CSV doesn't have headers in row0 you must
#   specify them with this param
def add_csv_to_cache_as_dict(args):
    if not BDRoot or not Logger:
        error = "add_csv_to_cache_as_dict: BDRoot or Logger not set"
        Logger.Error(error)
        return error
    if args.Count < 3:
        error = "add_csv_to_cache_as_dict: not enough params %s" % args
        Logger.Error(error)
        return error
    # unload the dynamic args: can't use Python slicing as args is IList
    Logger.Info("add_csv_to_cache_as_dict: args(%s)" % args)
    data_cache = args[0]
    cache_location = args[1]
    csv_file_name = args[2]
    key = None
    if args.Count > 3:
        key = args[3]
    headers = None
    if args.Count > 4:
        headers = args[4]
    csv_path = os.path.join(BDRoot, 'BizDeck', 'dat', csv_file_name)
    cs_cache_entry = None  
    with open(csv_path, "rt") as csv_file:
        reader = csv.DictReader(csv_file, headers)
        if key:
            cs_cache_entry = read_unqiue_key_rows(reader, key)
        else:
            cs_cache_entry = read_non_unqiue_key_rows(reader)
    data_cache.Insert(cache_location, csv_file_name, cs_cache_entry)
    return ""


def read_unqiue_key_rows(reader, key):
    cs_cache_entry = Dictionary[str,Dictionary[str,str]]()
    for py_row_dict in reader:
        key_value = py_row_dict[key]
        cs_row_dict = Dictionary[str,str]()
        for k,v in py_row_dict.items():
            cs_row_dict.Add(k, v)
        cs_cache_entry.Add(key_value, cs_row_dict)
    return cs_cache_entry


def read_non_unique_key_rows(reader):
    cs_cache_entry = List[Dictionary[str,str]]()
    for py_row_dict in reader:
        # Create the C# dict that will hold the row contents
        cs_row_dict = Dictionary[str,str]()
        for k,v in py_row_dict.items():
            cs_row_dict.Add(k, v)
        cs_row_list.Add(key_value, cs_row_dict)
    return cs_cache_entry
