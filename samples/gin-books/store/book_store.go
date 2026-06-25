package store

type Book struct {
	ID    string `json:"id"`
	Title string `json:"title"`
}

type Store struct {
	books []Book
}

func New() *Store {
	return &Store{}
}

func (s *Store) All() []Book {
	return s.books
}

func (s *Store) Find(id string) *Book {
	for i := range s.books {
		if s.books[i].ID == id {
			return &s.books[i]
		}
	}
	return nil
}

func (s *Store) Add(book Book) Book {
	s.books = append(s.books, book)
	return book
}
