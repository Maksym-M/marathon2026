using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.Feedbacks;
using APIHandler.Repositories.Feedbacks;
using APIHandler.Services.Feedbacks;
using APIHandler.Services.Reservations;
using Moq;

namespace APIHandler.Tests.Services;

public class FeedbackServiceTests
{
    private readonly Mock<IFeedbackRepository> _feedbackRepositoryMock = new();
    private readonly Mock<IReservationService> _reservationServiceMock = new();

    private readonly FeedbackService _service;

    public FeedbackServiceTests()
    {
        _service = new FeedbackService(_feedbackRepositoryMock.Object, _reservationServiceMock.Object);
    }

    private void GivenReservation(ReservationStatus status)
    {
        _reservationServiceMock
            .Setup(s => s.GetReservationByIdAsync("res-1"))
            .ReturnsAsync(new Reservation { Id = "res-1", TableId = "table-1", WaiterId = "waiter-1", Status = status });
        _reservationServiceMock
            .Setup(s => s.GetLocationIdForReservation("res-1"))
            .ReturnsAsync("loc-1");
    }

    private static CreateFeedbackRequest Request() => new()
    {
        ReservationId = "res-1",
        UserId = "user-1",
        Rating = 5,
        Comment = "Great",
        Type = FeedbackType.Service,
    };

    [Fact]
    public async Task CreateFeedbackAsync_CancelledReservation_ThrowsAndSavesNothing()
    {
        GivenReservation(ReservationStatus.Cancelled);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateFeedbackAsync(Request()));

        _feedbackRepositoryMock.Verify(r => r.SaveFeedbackAsync(It.IsAny<Feedback>()), Times.Never);
    }

    [Fact]
    public async Task CreateFeedbackAsync_CompletedReservation_SavesFeedback()
    {
        GivenReservation(ReservationStatus.Completed);

        await _service.CreateFeedbackAsync(Request());

        _feedbackRepositoryMock.Verify(r => r.SaveFeedbackAsync(It.Is<Feedback>(f => f.ReservationId == "res-1")), Times.Once);
    }

    [Fact]
    public async Task UpdateFeedbackAsync_CancelledReservation_ThrowsAndSavesNothing()
    {
        GivenReservation(ReservationStatus.Cancelled);

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateFeedbackAsync(Request()));

        _feedbackRepositoryMock.Verify(r => r.SaveFeedbackAsync(It.IsAny<Feedback>()), Times.Never);
    }
}
