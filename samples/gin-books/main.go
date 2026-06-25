package main

import (
	"github.com/gin-gonic/gin"

	"ginbooks/handlers"
)

func main() {
	r := gin.Default()

	r.GET("/books", handlers.ListBooks)
	r.GET("/books/:id", handlers.GetBook)
	r.POST("/books", handlers.CreateBook)

	r.Run()
}
