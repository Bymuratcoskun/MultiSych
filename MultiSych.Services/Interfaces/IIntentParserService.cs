using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface IIntentParserService
{
    Task<string> ParseIntentAsync(string transcribedText);
}