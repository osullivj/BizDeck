class CacheEntry(object):
    def __init__(self, val_dict, row_key, headers):
        self.type = "PrimaryKeyCSV"
        self.value = val_dict
        self.row_key = row_key
        self.headers = headers


class DataCache(object):
    def __init__(self):
        self.cache = dict()

    def Insert(self, group, cache_key, cs_cache_entry, row_key, column_names):
        group_dict = self.cache.setdefault(group, dict())
        group_dict[cache_key] = CacheEntry(cs_cache_entry, row_key, column_names)
