import logging


class Logger(object):
    def Info(self, text):
        logging.Info(text)

    def Error(self, text):
        logging.Error(text)

    def Warn(self, text):
        logging.Warn(text)