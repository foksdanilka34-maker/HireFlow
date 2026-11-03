namespace HireFlow.Domain.JobApplications;

public enum ApplicationStatus
{
    Received,
    AiReview,
    AwaitingQuestions,
    AiAnswersReview,
    Expired,
    RecruiterReview,
    AutoRejected,
    Invited,
    Rejected,
}
