import json
import sys

DATASETS = dict(
	FRED=["DED1", "DED3", "DED6", "DSWP3", "DSWP4", "DSWP5", "DSWP7", "DSWP10"],
	USTREASURY=["YIELD"]
)
QUANDL_URL_FMT = "https://www.quandl.com/api/v1/datasets/%s/%s.csv?auth_token=%s"
AUTH_TOKEN="75XT74ibdtekRjCNVdqR"

# No need for the "if __name__ == "__main__" idiom here, this code will
# always be a one shot op in an actions script.

actions = []
for dataset, datafiles in DATASETS.items():
	for datafile in datafiles:
		actions.append(
			dict(
				type="http_get", 
				name=datafile, 
				target="%s.csv" % datafile.lower(),
				url=QUANDL_URL_FMT % (dataset, datafile, AUTH_TOKEN),
				fail_ok=True
			)
		)

# one cmd line option: the file path for dumping results
output_file_name = sys.argv[0]
output_json = json.dumps(dict(actions=actions), indent=4)
with open(output_file_name, "wt") as output_file:
	output_file.write(output_json)
