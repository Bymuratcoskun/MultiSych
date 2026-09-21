using System;
using Gtk;
using MultiSych.Desktop.Localization;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Data;

namespace MultiSych.Desktop.Views;

public class CalendarView : Gtk.Box
{
    private CalendarViewModel? _viewModel;
    private Gtk.ListBox? _eventsList;
    private Gtk.Label? _statusLabel;
    private Gtk.Button? _refreshButton;
    private Gtk.Button? _clearFilterButton;
    private Gtk.Calendar? _datePicker;

    public CalendarViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null)
            {
                InitializeBindings();
            }
        }
    }

    public CalendarView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        this.Spacing = 15;

        SetMarginStart(20);
        SetMarginEnd(20);
        SetMarginTop(20);
        SetMarginBottom(20);

        BuildUi();
    }

    private void BuildUi()
    {
        var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);

        var title = Gtk.Label.New(Loc.Get("calendar.title"));
        title.SetHalign(Gtk.Align.Start);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        title.SetHexpand(true);
        header.Append(title);

        _refreshButton = Gtk.Button.NewWithLabel(Loc.Get("calendar.refresh_button"));
        _refreshButton.OnClicked += (s, e) => _viewModel?.RefreshCommand.Execute(null);
        header.Append(_refreshButton);

        _clearFilterButton = Gtk.Button.NewWithLabel(Loc.Get("calendar.clear_filter_button"));
        _clearFilterButton.OnClicked += (s, e) => _viewModel?.ClearFilterCommand.Execute(null);
        header.Append(_clearFilterButton);

        Append(header);

        var mainBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 20);
        mainBox.SetVexpand(true);

        // Sol: tarih seçici (filtre)
        _datePicker = Gtk.Calendar.New();
        _datePicker.OnDaySelected += (s, e) =>
        {
            if (_viewModel == null) return;
            var d = _datePicker.GetDate();
            d.GetYmd(out var year, out var month, out var day);
            _viewModel.SelectedDate = new DateTime(year, month, day);
        };
        mainBox.Append(_datePicker);

        // Sağ: etkinlik listesi
        var rightBox = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        rightBox.SetHexpand(true);

        _statusLabel = Gtk.Label.New(string.Empty);
        _statusLabel.SetHalign(Gtk.Align.Start);
        rightBox.Append(_statusLabel);

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetVexpand(true);
        scroll.SetHexpand(true);
        _eventsList = Gtk.ListBox.New();
        _eventsList.SetSelectionMode(Gtk.SelectionMode.None);
        scroll.SetChild(_eventsList);
        rightBox.Append(scroll);

        mainBox.Append(rightBox);
        Append(mainBox);
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.Events.CollectionChanged += (s, e) =>
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                PopulateEvents();
                return false;
            });
        };

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.IsLoading))
            {
                GLib.Functions.IdleAdd(0, () =>
                {
                    UpdateStatusLabel();
                    return false;
                });
            }
        };

        PopulateEvents();
        UpdateStatusLabel();
    }

    private void UpdateStatusLabel()
    {
        if (_statusLabel == null || _viewModel == null) return;
        _statusLabel.SetText(_viewModel.IsLoading
            ? Loc.Get("calendar.loading")
            : string.Format(Loc.Get("calendar.event_count_format"), _viewModel.Events.Count));
    }

    private void PopulateEvents()
    {
        if (_viewModel == null || _eventsList == null) return;

        var child = _eventsList.GetFirstChild();
        while (child != null)
        {
            _eventsList.Remove(child);
            child = _eventsList.GetFirstChild();
        }

        foreach (var ev in _viewModel.Events)
        {
            _eventsList.Append(BuildEventRow(ev));
        }

        UpdateStatusLabel();
    }

    private static Gtk.ListBoxRow BuildEventRow(CalendarEventEntity ev)
    {
        var row = Gtk.ListBoxRow.New();
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 4);
        box.SetMarginStart(10);
        box.SetMarginEnd(10);
        box.SetMarginTop(8);
        box.SetMarginBottom(8);

        var titleLabel = Gtk.Label.New(ev.Title);
        titleLabel.SetHalign(Gtk.Align.Start);
        titleLabel.SetFontWeight(Pango.Weight.Bold);
        box.Append(titleLabel);

        var timeText = ev.IsAllDay
            ? ev.StartTime.ToLocalTime().ToString("dd.MM.yyyy") + " · " + Loc.Get("calendar.all_day")
            : $"{ev.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {ev.EndTime.ToLocalTime():HH:mm}";
        var timeLabel = Gtk.Label.New(timeText);
        timeLabel.SetHalign(Gtk.Align.Start);
        timeLabel.SetFontSize(11);
        box.Append(timeLabel);

        if (!string.IsNullOrWhiteSpace(ev.Location))
        {
            var locationLabel = Gtk.Label.New($"📍 {ev.Location}");
            locationLabel.SetHalign(Gtk.Align.Start);
            locationLabel.SetFontSize(11);
            box.Append(locationLabel);
        }

        row.SetChild(box);
        return row;
    }
}
