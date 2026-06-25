from fastapi import FastAPI

from services.task_service import TaskService
from models.task import Task

app = FastAPI()
service = TaskService()


@app.get("/tasks")
def list_tasks():
    return service.list()


@app.get("/tasks/{task_id}")
def get_task(task_id: int):
    return service.get(task_id)


@app.post("/tasks")
def create_task(title: str):
    return service.create(Task(title))
