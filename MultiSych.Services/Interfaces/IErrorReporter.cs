namespace MultiSych.Services.Interfaces
{
    public interface IErrorReporter
    {
        Task<string> GenerateReportAsync(Exception exception, string format = "text");
        Task<string> GenerateReportAsync(string title, string description, string format = "text");
    }
}
