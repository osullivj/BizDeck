# This is an IronPython module that uses .Net types
# It will not work on the CPython runtime
# std py pkgs provided by IronPython
from csv import DictReader
import functools
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

# BizDeck utilities

# Global variables set by BizDeckPython.cs
BDRoot = None
Logger = None


# ActionFunctions: base class for an action with type:python_action
# An to unpack and check parameters from an args IDictionary
class ActionFunction(object):
    # pspec should be a list of 3 tuples: param name, type, optional
    def __init__(self, pspec):
        self.param_specs = pspec
        
    def __call__(self, csdict):
        param_dict, error_list = self.unpack_params(csdict)
        if error_list:
            error = "%s: parameter errors: %s" % (self.__class__.__name__, error_list)
            return error
        return self.implementation(**param_dict)
        
    def implementation(self, *args, **kwargs):
        return "not implemented"
        
    def unpack_params(self, csharp_dict):
        # usually in Python we see *args for a list on unnmamed params
        # and **kwargs for a dict of named params. But here we're passing
        # a single arg which is a C# Dictionary[str,object]
        # We also check that the global static vars in this module
        # have been set from startup C#
        if not BDRoot or not Logger:
            error = "%s: BDRoot(%s) or Logger(%s) not set" % (__name__, BDRoot, Logger)
            return error
        if not csharp_dict:
            error = "%s: csharp_dict is None" % __name__
            Logger.Error(error)
            return error
        param_errors = []
        param_dict = dict()
        for pname, ptype, popt in self.param_specs:
            pval = None
            pval_present = csharp_dict.ContainsKey(pname)
            # if a value is present, we use it, optional or not
            # but if it's not present, and not optional, error
            if pval_present:
                pval = csharp_dict[pname]
            elif not popt:  # not optional, so error on absence
                param_errors.append("%s: missing %s param" % (self.__name__, pname))
                continue    # skip on to next param
            # now check the parameter type. NB  isinstance allows subclasses
            # we only pass through pval if it has come from csharp_dict
            # default values are supplied in the declaration of the func
            # we're calling. If we supplied a None value here it would
            # override the method decl default param vals
            if pval_present:
                # a null ptype means skip the type check
                # print("%s:%s:%s" % (pname, ptype, popt))
                if not ptype or type(pval) == ptype:
                    param_dict[pname] = pval
                else:
                    param_errors.append("%s: %s param is %s not %s" % (self.func.__name__, pname, type(pval), ptype))
        return param_dict, param_errors


# A pair of csv reader helper functions

# Python's CSVReader returned ordered dicts, so the KV pair are in the same
# order as in the file. However, C# IDictionary is not ordered, so columns
# appear disordered in the /excel table views, because the code below 
# marshals into C# dicts. 

def read_unique_key_rows(reader, key):
    cs_cache_entry = Dictionary[str, Dictionary[str, str]]()
    for py_row_dict in reader:
        key_value = py_row_dict[key]
        cs_row_dict = Dictionary[str, str]()
        for k,v in py_row_dict.items():
            cs_row_dict.Add(k.replace(' ', ''), v)
        cs_cache_entry.Add(key_value, cs_row_dict)
    return cs_cache_entry


def read_non_unique_key_rows(reader):
    cs_cache_entry = List[Dictionary[str, str]]()
    for py_row_dict in reader:
        # Create the C# dict that will hold the row contents
        cs_row_dict = Dictionary[str, str]()
        for k,v in py_row_dict.items():
            # remove whitespace from keys before caching
            cs_row_dict.Add(k.replace(' ', ''), v)
        cs_cache_entry.Add(cs_row_dict)
    return cs_cache_entry


# Insert a CSV into the BizDeck cache as a dict
# cache: supplied by ActionsDriver.RunPythonAction
# group: supplied by json action object in action script eg "quandl"
# csv: name of csv file in %BDROOT% to load. Used to derive the cache_key
# row_key: which field is used as the key field
# headers: specify column order as IronPython DictReader doesn't maintain order from csv
# max_lines: cap the number of lines to read, handy if latest at top
class AddCsvToCacheAsDict(ActionFunction):
    def implementation(self, cache, group, csv, row_key=None, headers=None):
        print("enter impl")
        csv_path = os.path.join(BDRoot, 'data', 'csv', csv)
        cs_cache_entry = None
        field_names = None
        with open(csv_path, "rt") as csv_file:
            reader = DictReader(csv_file, headers)
            field_names = reader.fieldnames
            if row_key:
                cs_cache_entry = read_unique_key_rows(reader, row_key)
            else:
                cs_cache_entry = read_non_unique_key_rows(reader)
        # csv_file_name will be eg yield.csv; no good as a JSON
        # property name as it looks like a member reference. So
        # change the . to _
        cache_key = csv.replace('.', '_')
        # Return correctly ordered column names as last parameter
        column_names = List[str]()
        map(column_names.Add, field_names)
        cache.Insert(group, cache_key, cs_cache_entry, row_key, column_names)
        return ""


param_specs1 = [
    ('cache', None, False),
    ('group', str, False),
    ('csv', str, False),
    ('row_key', str, True),
    ('headers', List, True)
]
add_csv_to_cache_as_dict = AddCsvToCacheAsDict(param_specs1)
