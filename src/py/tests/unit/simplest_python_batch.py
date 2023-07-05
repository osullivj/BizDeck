# This is not a standalone unit test style test module.
# It's designed to be the minimal loadable py module
# for testing BizDeck IronPython execution of arbitrary
# Python modules as actions. See pytest1.json.
import sys
print("pytest1 sys.argv:%s" % sys.argv)
