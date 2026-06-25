import itertools

_ids = itertools.count(1)


class Task:
    def __init__(self, title: str):
        self.id = next(_ids)
        self.title = title
        self.done = False
