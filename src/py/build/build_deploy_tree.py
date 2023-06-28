# std pkgs
import logging
import sys
# plumbum
from plumbum import cli, local
from plumbum.path.utils import copy, delete
from plumbum.path.local import LocalPath
# dir tree spec
from specify_deploy_tree import DEPLOY_TREE

logging.basicConfig(level=logging.INFO,
                    handlers=[logging.FileHandler("build_deploy_tree.log"),
                              logging.StreamHandler()])
logger = logging.getLogger("deploy")


class DeployTreeBuilder(cli.Application):
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
    def main(self, target_dir):
        target_path = local.path(target_dir)
        if target_path.exists():
            logger.info("Deleting %s" % target_path)
            delete(target_path)
        else:
            target_path.mkdir()
        bdroot = local.env["BDROOT"]
        logger.info("BDROOT[%s] target_dir[%s]", bdroot, target_dir)
        self.build(local.path(bdroot), target_path, DEPLOY_TREE)
        logger.info("Deploy tree complete")

    # Do the recursive tree build
    def build(self, source_dir, target_dir, spec_dict):
        for fs_rel_path, fs_spec in spec_dict.items():
            # does this rel path, file spec pair specify
            # a directory? If so we recurse...
            if fs_rel_path[-1] == '/':  # create the dir
                new_target_subdir = target_dir / fs_rel_path[:-1]
                logging.info("Creating subdir %s" % new_target_subdir)
                new_target_subdir.mkdir()
                # fs_spec tells us what should be in new_target_subdir,
                # so we recurse...
                self.build(source_dir, new_target_subdir, fs_spec)
            else:
                # fs_spec is a file, not directory, spec, so do
                # the copy operation: recursive dir copy, wildcard
                # copy, or fully named file copy
                if fs_rel_path[-1] == ":":    # recursive dir cp
                    # a dos box style copy works without giving the full target
                    # path eg "copy /r c:\a\b\c c:\d" will create c:\d\c with
                    # the entire subtree from c:\a\b\c replacated at c:\d\c
                    # But in plumbum we need to name the subdir under c:\d
                    # explicitly as c:\d\c. An actual use in DEPLOY_TREE for
                    # IronPython libs is...
                    # 'lib:': 'src/cs/server/bin/Debug/net5.0/lib'
                    source_path = source_dir / fs_spec
                    target_path = target_dir / fs_rel_path[:-1]
                elif fs_rel_path[0] == '*':   # Are wildcards (*) in play?
                    # we know the RHS will have wildcards
                    # eg "cp <bdroot>/src/cs/server/bin/Release/net5.0/*.dll <target_dir=bin>"
                    # // instead of / to compose a path with globbing (wildcards)
                    source_path = source_dir // fs_spec
                    target_path = target_dir
                else:
                    # simple file copy
                    # eg "cp <bdroot>/cfg/config.json <target_dir=cfg>/config.json
                    source_path = source_dir / fs_spec
                    target_path = target_dir / fs_rel_path
                logging.info("Copying %s to %s" % (source_path, target_path))
                copy(source_path, target_path)


if __name__ == "__main__":
    DeployTreeBuilder.run()
