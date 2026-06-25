from models.task import Task


class TaskService:
    def __init__(self):
        self._tasks: list[Task] = []

    def list(self):
        return self._tasks

    def get(self, task_id: int):
        return next(task for task in self._tasks if task.id == task_id)

    def create(self, task: Task):
        self._tasks.append(task)
        return task
