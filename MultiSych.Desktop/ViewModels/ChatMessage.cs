using System;

namespace MultiSych.Desktop.ViewModels;

public sealed class ChatMessage
{
    public ChatMessage(string senderName, string message, bool isUser)
    {
        SenderName = senderName;
        Message = message;
        IsUser = isUser;
        Timestamp = DateTimeOffset.Now;
    }

    public string SenderName { get; }
    public string Message { get; }
    public bool IsUser { get; }
    public DateTimeOffset Timestamp { get; }
}
