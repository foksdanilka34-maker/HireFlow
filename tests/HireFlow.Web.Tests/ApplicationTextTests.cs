using HireFlow.Web.Api;
using HireFlow.Web.Applications;

namespace HireFlow.Web.Tests;

public class ApplicationTextTests
{
    // Решения только те, которые API примет в статусе: иначе кнопка всегда даёт 409.
    [Theory]
    [InlineData(ApplicationStatus.Received, new RecruiterDecision[0])]
    [InlineData(ApplicationStatus.RecruiterReview, new[] { RecruiterDecision.Invite, RecruiterDecision.Reject })]
    [InlineData(ApplicationStatus.AutoRejected, new[] { RecruiterDecision.Invite })]
    [InlineData(ApplicationStatus.Invited, new RecruiterDecision[0])]
    [InlineData(ApplicationStatus.Rejected, new RecruiterDecision[0])]
    public void Decisions_ReturnsOnlyDecisionsAllowedForStatus(ApplicationStatus status, RecruiterDecision[] expected)
    {
        Assert.Equal(expected, ApplicationText.Decisions(status));
    }

    // Подтверждение нужно только при приглашении после автоотказа, который кандидату уже отправлен.
    [Theory]
    [InlineData(ApplicationStatus.AutoRejected, true, true)]
    [InlineData(ApplicationStatus.AutoRejected, false, false)]
    [InlineData(ApplicationStatus.RecruiterReview, true, false)]
    [InlineData(ApplicationStatus.Rejected, true, false)]
    public void NeedsInviteConfirmation_OnlyForAutoRejectedWithSentRejection(
        ApplicationStatus status, bool rejectionSent, bool expected)
    {
        var application = new ApplicationDto(
            Guid.NewGuid(), Guid.NewGuid(), "Vacancy", Guid.NewGuid(), "Name", "79991234567", status, 3, "resume.pdf",
            ResumeFormat.Pdf, rejectionSent, new ScreeningDto(50, ScreeningVerdict.Failed, [], [], true),
            DateTime.UtcNow, DateTime.UtcNow, []);

        Assert.Equal(expected, ApplicationText.NeedsInviteConfirmation(application));
    }
}
