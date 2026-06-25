package handlers

import (
	"net/http"

	"github.com/gin-gonic/gin"

	"ginbooks/store"
)

var books = store.New()

func ListBooks(c *gin.Context) {
	c.JSON(http.StatusOK, books.All())
}

func GetBook(c *gin.Context) {
	c.JSON(http.StatusOK, books.Find(c.Param("id")))
}

func CreateBook(c *gin.Context) {
	var book store.Book
	c.BindJSON(&book)
	c.JSON(http.StatusCreated, books.Add(book))
}
