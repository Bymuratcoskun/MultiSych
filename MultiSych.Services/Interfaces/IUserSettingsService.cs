using System.Threading.Tasks;
using MultiSych.Services.Models;

namespace MultiSych.Services.Interfaces;

public interface IUserSettingsService
{
    UserSettings Settings { get; }
    Task SaveAsync();
    Task LoadAsync();
}