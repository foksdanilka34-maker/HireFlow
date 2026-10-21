namespace HireFlow.Application.Common;

public class TooManyRequestsException(string message) : Exception(message);
