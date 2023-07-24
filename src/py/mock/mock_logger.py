import logging


class Logger(object):
    def Info(self, text):
        logging.info(text)

    def Error(self, text):
        logging.error(text)

    def Warn(self, text):
        logging.warn(text)