using Godot;
using System;
using ProjectDeviation.UI;
using ProjectDeviation.Models;
using ProjectDeviation.Core;
using ProjectDeviation.Language; // Netjerovels Dil Motoru için eklendi
using ProjectDeviation.News; // Haber Motoru için eklendi

namespace ProjectDeviation.Managers
{
    public partial class CoreManager : Node
    {
        [Export] private ProgressionManager _progressionManager;
        [Export] private UIManager _uiManager;
        [Export] private CPSEngine _cpsEngine;
        [Export] private PrestigeManager _prestigeManager;
        [Export] private SoundManager _soundManager;
        [Export] private VisualEffectsManager _vfxManager;
        
        private NetjerovelsEngine _languageEngine = new NetjerovelsEngine(); // Dil Motoru Referansı
        private UltraNewsEngine _newsEngine = new UltraNewsEngine(); // Haber Motoru Referansı

        public override void _Ready()
        {
            if (_uiManager != null)
            {
                _uiManager.LogToTerminal("[CORE MANAGER] Devreye alındı. XCORE Motoru başlatılıyor...");
            }
            
            // CPSEngine Event Bağlantıları (Event Wiring)
            if (_cpsEngine != null && _uiManager != null)
            {
                _cpsEngine.OnDataUpdated += _uiManager.UpdateDigitalData;
                _cpsEngine.OnAnalogDataUpdated += _uiManager.UpdateAnalogData;
                _cpsEngine.OnSystemAlert += _uiManager.LogToTerminal; // Terminal uyarılarını doğrudan bağlar

                // Milestone 6: Nemesis Krizi veya Overflow (Hata) anlarında VFX Tetiklemesi
                _cpsEngine.OnSystemAlert += (msg, isError) => 
                {
                    if (isError && _vfxManager != null) 
                    {
                        _vfxManager.TriggerGlitch(1.5f);
                        _vfxManager.TriggerCameraShake(5.0f, 1.0f);
                    }
                };
            }
            
            // Prestige Event Bağlantıları
            if (_prestigeManager != null && _uiManager != null)
            {
                _prestigeManager.OnPrestigeAlert += _uiManager.LogToTerminal;
            }

            // Milestone 3: UI üzerinden girilen komutları dinle
            if (_uiManager != null)
            {
                _uiManager.OnCommandEntered += HandleTerminalCommand;
                
                // Sekme değişimlerinde dinamik müziği (BGM) değiştir
                _uiManager.OnTabSwitched += (tab) => _soundManager?.PlayBGMForTab(tab);
            }

            // Test simülasyonu: Oyun başladıktan 3 saniye sonra otomatik bir test dizisi tetikliyoruz
            GetTree().CreateTimer(3.0f).Timeout += TestPhaseProgression;
            
            // Nemesis Sabotaj Testi: Phase testinden sonra, 7. saniyede başlatıyoruz
            GetTree().CreateTimer(7.0f).Timeout += TestNemesisSabotageLoop;
            
            // Overclock ve Kuantum Testi: Nemesis testinden sonra, 20. saniyede başlat
            GetTree().CreateTimer(20.0f).Timeout += TestOverclockAndQuantum;
            
            // Milestone 4: Arka Planda Prosedürel Haber Akışı (15 Saniyede Bir)
            var newsTimer = new Timer();
            newsTimer.WaitTime = 15.0f;
            newsTimer.Autostart = true;
            newsTimer.Timeout += () => 
            {
                _uiManager?.LogToTerminal($"[color=yellow][HABER AĞI][/color] {_newsEngine.GenerateUniqueNews()}");
            };
            AddChild(newsTimer);

            // Milestone 5: Prestige ve Mekanik Evrim Testi: 30. Saniyede başlat
            GetTree().CreateTimer(30.0f).Timeout += TestPrestigeAndEvolution;
            
            // Milestone 6: Multimedya (VFX & Sound) Testi: 40. Saniyede başlat
            GetTree().CreateTimer(40.0f).Timeout += TestMultimediaIntegration;
        }

        // PhaseModel ve Sert Blok (Progression Lock) mekanizmasını test eden metot
        private void TestPhaseProgression()
        {
            if (_progressionManager == null || _uiManager == null) return;

            _uiManager.LogToTerminal("\n[SİSTEM] Görev deşifre test dizisi başlatılıyor...");
            
            var currentPhase = _progressionManager.GetCurrentPhase();
            if (currentPhase != null)
            {
                // TEST 1: Görevler bitmeden erken geçiş denemesi (Progression Lock testi)
                _uiManager.LogToTerminal($"TEST 1: Faz {currentPhase.PhaseID} tamamlanmadan geçiş deneniyor...", false);
                bool isAdvanced = _progressionManager.TryAdvancePhase();
                
                if (!isAdvanced)
                {
                    _uiManager.LogToTerminal("[BAŞARILI TEST] Sert Blok çalıştı! 7 görev bitmeden geçiş engellendi.", true);
                }

                // TEST 2: Görevleri sahte (mock) olarak tamamlama simülasyonu
                _uiManager.LogToTerminal("\nTEST 2: Sistemdeki 7 görev %100 tamamlanıyor...", false);
                foreach (var mission in currentPhase.SystemMissions)
                {
                    mission.IsCompleted = true; // Tüm görevleri tamamlandı olarak işaretle
                }

                // TEST 3: Tekrar geçiş denemesi
                bool isAdvancedNow = _progressionManager.TryAdvancePhase();
                if (isAdvancedNow)
                {
                    _uiManager.LogToTerminal($"[BAŞARILI TEST] Kilit açıldı! Yeni Faz: {_progressionManager.CurrentPhaseID}", false);
                }
            }
        }

        // Milestone 2: Nemesis Sabotaj Döngüsü Test Simülasyonu
        private void TestNemesisSabotageLoop()
        {
            if (_cpsEngine == null || _uiManager == null) return;

            _uiManager.LogToTerminal("\n[SİSTEM] MILESTONE 2: Nemesis Sabotaj Döngüsü Testi Başlatılıyor...");
            
            // 1. Üretimi başlat
            _uiManager.LogToTerminal("TEST 1: Otonom Ajanlar devreye alınıyor (10 b/s)...", false);
            _cpsEngine.AddAgent(10.0); // 10 Byte/saniye kazanç veriyoruz

            // 2. Birkaç saniye üretim olduktan sonra Nemesis'i tetikle
            GetTree().CreateTimer(3.0f).Timeout += () => 
            {
                _cpsEngine.TriggerNemesisSabotage(4.0); // 4 saniyelik kriz süresi, bu sürede üretim KESİLİR
                
                // 3. Nemesis'i son anda (3 saniye sonra) Defuse et (kurtar)
                GetTree().CreateTimer(3.0f).Timeout += () => 
                {
                    _uiManager.LogToTerminal("TEST 2: Elena'nın kalkanı ile Nemesis durduruluyor...", false);
                    _cpsEngine.DefuseNemesis(); // Üretim tekrar normale döner
                    
                    // 4. İkinci bir saldırı simüle et ve bu sefer bilerek çökmesine izin ver
                    GetTree().CreateTimer(4.0f).Timeout += () => 
                    {
                        _uiManager.LogToTerminal("\nTEST 3: Yeni bir Nemesis saldırısı! Bu sefer müdahale edilmeyecek...", false);
                        _cpsEngine.TriggerNemesisSabotage(2.0); // 2 saniye sonra sistem çökecek ve ajanlar %50 kaybolacak
                    };
                };
            };
        }

        // Milestone 2 (Kısım 2): Overclock ve Kuantum İlerleme Testi
        private void TestOverclockAndQuantum()
        {
            if (_cpsEngine == null || _uiManager == null) return;

            _uiManager.LogToTerminal("\n[SİSTEM] MILESTONE 2: Overclock & Kuantum Zamanı Testi Başlatılıyor...");
            
            // 1. Overclock Testi
            _uiManager.LogToTerminal("TEST 1: Overclock Aktifleştiriliyor (5 saniyelik demo)...", false);
            _cpsEngine.ActivateOverclock(5.0);

            // 2. Kuantum (Offline) Testi
            GetTree().CreateTimer(6.0f).Timeout += () => 
            {
                _uiManager.LogToTerminal("\nTEST 2: Kuantum Çevrimdışı İlerleme Simüle Ediliyor (1 saat = 3600 sn)...", false);
                _cpsEngine.CalculateQuantumOfflineProgress(3600);
            };
        }

        // Milestone 3: Netjerovels Dil Algoritması Girdi Testi
        private void HandleTerminalCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                string subject = parts[0];
                string verb = parts[1];
                string obj = parts[2];

                bool isValidSyntax = _languageEngine.ValidateSVOSyntax(subject, verb, obj);
                if (isValidSyntax)
                {
                    _uiManager.LogToTerminal($"[NETJEROVELS] SVO Dizilimi (Özne-Yüklem-Nesne) Doğrulandı.");
                    
                    var wordClass = _languageEngine.DetermineWordClass(obj);
                    var harmony = _languageEngine.CheckVowelHarmony(obj);
                    
                    _uiManager.LogToTerminal($"  -> Hedef Kelime: '{obj}' | Sınıf: {wordClass} | Uyum: {harmony}", false);
                    
                    string pluralForm = _languageEngine.AppendSuffix(obj, "plural");
                    _uiManager.LogToTerminal($"  -> Çoğul Çekim Simülasyonu: {pluralForm}", false);
                    
                    // Milestone 6: Netjerovels kelimesini ekrana basarken ses fonetiklerini tetikle
                    _soundManager?.PlayRunicSequence(pluralForm);
                }
            }
            else
            {
                _uiManager.LogToTerminal("[SÖZDİZİMİ HATASI] Netjerovels SVO (Özne Yüklem Nesne) formatına uymuyor! Örn: 'AI execute tet'", true);
            }
        }

        // Milestone 5: Prestige ve Act Evrimi Simülasyonu
        private void TestPrestigeAndEvolution()
        {
            if (_prestigeManager == null || _uiManager == null || _progressionManager == null) return;

            _uiManager.LogToTerminal("\n[SİSTEM] MILESTONE 5: Mekanik Evrim ve Prestige Testi Başlatılıyor...");
            
            // 1. Act Geçişi Simülasyonu (Bunu tetiklediğimizde CPSEngine otomatik olarak Act II algılayıp Analog şebekeyi açacak)
            _uiManager.LogToTerminal("TEST 1: Act II'ye geçiş yapılıyor (Analog Şebeke açılmalı)...", false);
            _progressionManager.AdvanceAct();

            // 2. Prestige (Format) Simülasyonu
            GetTree().CreateTimer(4.0f).Timeout += () => 
            {
                _uiManager.LogToTerminal("\nTEST 2: Sistem Formatlanıyor (Prestige)...", false);
                _prestigeManager.ExecutePrestige();
            };
        }

        // Milestone 6: Multimedya Test Simülasyonu
        private void TestMultimediaIntegration()
        {
            if (_soundManager == null || _vfxManager == null || _uiManager == null) return;

            _uiManager.LogToTerminal("\n[SİSTEM] MILESTONE 6: Multimedya Entegrasyon Testi Başlatılıyor...");
            
            _vfxManager.TriggerGlitch(2.0f);
            _vfxManager.TriggerCameraShake(10.0f, 1.5f);
            
            _soundManager.PlayRunicSequence("AI Terminal Initialized");
            _uiManager.LogToTerminal("[color=purple]>>> Rünik ses simülasyonu ve Glitch efekti oynatıldı.[/color]");
        }
    }
}