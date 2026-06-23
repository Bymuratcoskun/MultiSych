using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using Serilog;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.Services;

public static class SoundPlayerService
{
    public static void PlayNotificationSound(NotificationSound soundType = NotificationSound.Default)
    {
        try
        {
            var userSettings = Program.ServiceProvider.GetRequiredService<IUserSettingsService>();
            if (!userSettings.Settings.EnableNotificationSounds)
            {
                return;
            }

            string soundFileName = soundType switch
            {
                NotificationSound.Email => "email.wav",
                NotificationSound.Event => "event.wav",
                NotificationSound.Error => "error.wav",
                NotificationSound.Success => "success.wav",
                _ => "notification.wav"
            };

            // Projenin ana dizinindeki 'Assets' klasöründen sesi arar.
            // Bu dosyanın projenize eklenmesi ve build sırasında kopyalanması gerekir.
            string soundFilePath = Path.Combine(AppContext.BaseDirectory, "Assets", soundFileName);

            if (!File.Exists(soundFilePath))
            {
                Log.Warning("Notification sound file not found at: {Path}", soundFilePath);
                return;
            }

            var waveOut = new WaveOutEvent();
            var audioFileReader = new AudioFileReader(soundFilePath);

            waveOut.Init(audioFileReader);
            waveOut.PlaybackStopped += (s, a) =>
            {
                audioFileReader.Dispose();
                waveOut.Dispose();
            };
            waveOut.Play();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play notification sound.");
        }
    }
}