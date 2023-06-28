# std pkgs
import logging
import shutil
import sys
# plumbum
from plumbum import cli, local
from plumbum.path.utils import copy, delete
# dir tree spec
from specify_deploy_tree import DEPLOY_TREE
from bd_utils import configure_logging

logger = configure_logging("zip")


class DeployTreeZipper(cli.Application):
    # Recursively create a BizDeck deployment dir tree from
    # specify_deploy_tree.DEPLOY_TREE
    # @cli decorated methods are called by plumbum depending
    # on cmd line options
    @cli.switch(["-l", "--log-to-file"], argtype=str)
    def log_to_file(self, filename):
        """logs all output to the given file"""
        handler = logging.FileHandler(filename)
        logger.addHandler(handler)

    @cli.switch(["--verbose"], requires=["--log-to-file"])
    def set_debug(self):
        """Sets verbose mode"""
        logger.setLevel(logging.DEBUG)

    # main point of entry
    def main(self):
        bdroot = local.env["BDROOT"]
        bdtree = local.env["BDTREE"]
        logger.info("BDROOT[%s] BDTREE[%s]", bdroot, bdtree)
        # create the dist subdir
        bdroot_lp = local.path(bdroot)
        dist = bdroot_lp / 'dist'
        if dist.exists():
            dist.delete()
        dist.mkdir()
        zip_path = dist / 'bizdeck'
        shutil.make_archive(zip_path, 'zip', root_dir=bdtree)


if __name__ == "__main__":
    DeployTreeZipper.run()
