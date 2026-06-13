using System;

namespace MultiSych.Desktop.ViewModels;

public class ChatMessage
{
    public ChatMessage(string sender, string content, bool isUser = false)
    {
        Sender = sender;
        Content = content;
        Timestamp = DateTime.Now.ToString("HH:mm");
        IsUser = isUser;
    }

    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SenderName => Sender;
    public string Message => Content;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsUser { get; set; }
}
