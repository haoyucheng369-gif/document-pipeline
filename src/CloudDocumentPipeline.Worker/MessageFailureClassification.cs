using System.Text.Json;
using CloudDocumentPipeline.Application.Exceptions;

namespace CloudDocumentPipeline.Worker;

public enum MessageFailureDisposition
{
    Retry,
    DeadLetter
}

public static class MessageFailureClassification
{
    public static MessageFailureDisposition Classify(Exception exception)
    {
        return exception switch
        {
            JobNotFoundException => MessageFailureDisposition.DeadLetter,
            InvalidJobStateException => MessageFailureDisposition.DeadLetter,
            JsonException => MessageFailureDisposition.DeadLetter,
            NotSupportedException => MessageFailureDisposition.DeadLetter,
            _ => MessageFailureDisposition.Retry
        };
    }
}
