using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        /// <summary>
        /// AddBook should create and save a new Book when repository doesn't contain the title.
        /// </summary>
        [Fact]
        public void AddBook_ShouldAddNewBook_WhenNotExists()
        {
            _repoMock.Setup(r => r.FindBook("1984")).Returns((Book)null);

            _service.AddBook("1984", 3);

            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == "1984" && b.Copies == 3)), Times.Once);
        }

        /// <summary>
        /// AddBook should increase copies on the existing Book and save it.
        /// </summary>
        [Fact]
        public void AddBook_ShouldIncreaseCopies_WhenBookExists()
        {
            var existing = new Book { Title = "1984", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("1984")).Returns(existing);

            _service.AddBook("1984", 3);

            Assert.Equal(5, existing.Copies);
            _repoMock.Verify(r => r.SaveBook(existing), Times.Once);
        }

        /// <summary>
        /// AddBook should throw ArgumentException on invalid inputs and not find or save a book.
        /// </summary>
        [Theory]
        [InlineData("", 2)]
        [InlineData(null, 2)]
        [InlineData("Book", 0.1)]
        [InlineData("Book", -1)]
        [InlineData("Book", 0)]
        public void AddBook_ShouldThrowAndNotSaveOrNotify_WhenInvalidInput(string title, int copies)
        {
            Assert.ThrowsAny<ArgumentException>(() => _service.AddBook(title, copies));
            _repoMock.Verify(r => r.FindBook(It.IsAny<string>()), Times.Never);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        /// <summary>
        /// BorrowBook should decrease copies, return true for valid member and available book, and notify borrower.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldDecreaseCopies_WhenValidMemberAndAvailable()
        {
            var book = new Book { Title = "The Metamorphosis", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("The Metamorphosis")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "The Metamorphosis");

            Assert.True(result);
            Assert.Equal(1, book.Copies);
            _notifMock.Verify(n => n.NotifyBorrow(1, "The Metamorphosis"), Times.Once);
        }

        /// <summary>
        /// BorrowBook should return false and not save or notify when no copies are available.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldReturnFalse_WhenNoCopies()
        {
            var book = new Book { Title = "The Metamorphosis", Copies = 0 };
            _repoMock.Setup(r => r.FindBook("The Metamorphosis")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "The Metamorphosis");

            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// BorrowBook should throw InvalidOperationException for invalid member and must not call repository save or notifications.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldThrowAndNotSaveOrNotify_WhenInvalidMember()
        {
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(1, "The Metamorphosis"));
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// BorrowBook persists the updated book by calling SaveBook exactly once when borrowing succeeds.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldCallSaveBook_ExactlyOnce_WhenSuccessful()
        {
            var book = new Book { Title = "The Metamorphosis", Copies = 2 };
            _repoMock.Setup(r => r.FindBook("The Metamorphosis")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, "The Metamorphosis");

            Assert.True(result);
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == "The Metamorphosis" && b.Copies == 1)), Times.Exactly(1));
        }

        /// <summary>
        /// ReturnBook should increase copies, return true and notify return.
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldIncreaseCopies()
        {
            var book = new Book { Title = "Animal Farm", Copies = 1 };
            _repoMock.Setup(r => r.FindBook("Animal Farm")).Returns(book);

            bool result = _service.ReturnBook(1, "Animal Farm");

            Assert.True(result);
            Assert.Equal(2, book.Copies);
            _notifMock.Verify(n => n.NotifyReturn(1, "Animal Farm"), Times.Once);
        }

        /// <summary>
        /// ReturnBook should return false and not save or notify when the book isn't found.
        /// </summary>
        [Fact]
        public void ReturnBook_ShouldReturnFalse_WhenBookNotFound()
        {
            _repoMock.Setup(r => r.FindBook("Unknown")).Returns((Book)null);

            bool result = _service.ReturnBook(1, "Unknown");

            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyReturn(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// GetAvailableBooks should return only books with copies > 0.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ShouldReturnOnlyBooksWithCopies()
        {
            var all = new List<Book>
            {
                new Book { Title = "1984", Copies = 2 },
                new Book { Title = "Animal Farm", Copies = 0 },
                new Book { Title = "The Metamorphosis", Copies = 3 }
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            var available = _service.GetAvailableBooks();

            Assert.NotEmpty(available);
            Assert.Contains(available, a => a.Title == "1984");
            Assert.Contains(available, a => a.Title == "The Metamorphosis");
            Assert.Equal(2, available.Count);
        }

        /// <summary>
        /// GetAvailableBooks should return an empty but non-null list when repository has no available books.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ShouldReturnEmpty_WhenNoBooksAvailable()
        {
            var all = new List<Book> { new Book { Title = "A", Copies = 0 } };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            var result = _service.GetAvailableBooks();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Verify FindBook was called at least once when ReturnBook is executed.
        /// </summary>
        [Fact]
        public void Verify_MethodsCalled_AtLeastOnce()
        {
            var book = new Book { Title = "Animal Farm", Copies = 1 };
            _repoMock.Setup(r => r.FindBook("Animal Farm")).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            _service.ReturnBook(1, "Animal Farm");

            _repoMock.Verify(r => r.FindBook("Animal Farm"), Times.AtLeastOnce);
        }
    }
}
