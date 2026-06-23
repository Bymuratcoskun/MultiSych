using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Data;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.ViewModels;

public class CalendarViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _isLoading;
    private DateTime? _selectedDate;
    private List<CalendarEventEntity> _allEvents = [];

    public ObservableCollection<CalendarEventEntity> Events { get; } = [];

    public CalendarViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        ClearFilterCommand = new RelayCommand(_ => SelectedDate = null, _ => SelectedDate.HasValue);
        Task.Run(LoadEventsAsync);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set 
        { 
            if (SetProperty(ref _selectedDate, value)) 
            {
                ApplyFilter();
                (ClearFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand ClearFilterCommand { get; }

    private void ApplyFilter()
    {
        Events.Clear();
        var filtered = _allEvents.AsEnumerable();
        if (SelectedDate.HasValue)
        {
            var date = SelectedDate.Value.Date;
            filtered = filtered.Where(e => e.StartTime.Date <= date && e.EndTime.Date >= date);
        }
        
        foreach (var ev in filtered) Events.Add(ev);
    }

    private async Task LoadEventsAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LocalCacheDbContext>();
            
            var events = await dbContext.CachedEvents.OrderByDescending(e => e.StartTime).ToListAsync();
                
            Dispatcher.UIThread.Post(() => 
            { 
                _allEvents = events;
                ApplyFilter();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }
}
