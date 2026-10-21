namespace HireFlow.Application.Common;

public class ConflictException(string message, Exception? inner = null) : Exception(message, inner);
