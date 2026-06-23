using System;
using Avalonia.Controls;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace MultiSych.Desktop.Views;

public partial class MediaPlayerWindow : Window
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;

    public MediaPlayerWindow()
    {
        InitializeComponent();
    }

    // Sanal sürücüdeki (Z:\video.mp4 gibi) mutlak dosya yolunu alacak constructor
    public MediaPlayerWindow(string filePath) : this()
    {
        // VLC Çekirdeğini Başlat
        Core.Initialize();
        
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        
        var videoView = this.FindControl<VideoView>("VideoView");
        if (videoView != null)
        {
            videoView.MediaPlayer = _mediaPlayer;
        }

        // Medyayı sanal disk üzerinden aç ve oynatmaya başla
        using var media = new Media(_libVLC, filePath, FromType.FromPath);
        _mediaPlayer.Play(media);
    }

    protected override void OnClosed(EventArgs e)
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnClosed(e);
    }
}