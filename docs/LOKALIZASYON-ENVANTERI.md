# Lokalizasyon Envanteri

Kaynak kapsamı: `MultiSych.Desktop/Views/*.cs`. Tarama,
`tools/lokalizasyon-testi.sh` ile aynı beş metin API'sine göre yapıldı:
`NewWithLabel`, `Label.New`, `SetTitle`, `SetPlaceholderText` ve
`SetTooltipText`. Aynı literal birden fazla yerde kullanılıyorsa tek anahtar
önerildi ve bütün kaynakları aynı satırda gösterildi.

| Anahtar | TR | EN (önerilen) | Kaynak |
|---|---|---|---|
| documents.title | Belgeler | Documents | DocumentsView.cs:90 |
| documents.create_button | Yeni Belge | New Document | DocumentsView.cs:101 |
| documents.refresh_button | Yenile 🔄 | Refresh 🔄 | DocumentsView.cs:105 |
| documents.search_placeholder | Belgelerde ara… | Search documents… | DocumentsView.cs:137 |
| documents.file_list_title | Dosya Listesi | File List | DocumentsView.cs:152 |
| documents.select_file_prompt | Bir belge seçin | Select a document | DocumentsView.cs:178 |
| documents.save_text_button | Metni Kaydet | Save Text | DocumentsView.cs:212 |
| documents.revert_changes_button | Değişiklikleri Geri Al | Revert Changes | DocumentsView.cs:215 |
| documents.chat.placeholder | Belge hakkında soru sorun… | Ask a question about the document… | DocumentsView.cs:231 |
| common.send | Gönder | Send | DocumentsView.cs:240; ChatView.cs:88; AIChatWindow.cs:78 |
| documents.chat.clear_button | Sohbeti Temizle | Clear Chat | DocumentsView.cs:243 |
| documents.create.name_placeholder | Dosya adı | File name | DocumentsView.cs:260 |
| documents.create.confirm_button | Oluştur | Create | DocumentsView.cs:279 |
| common.cancel | İptal | Cancel | DocumentsView.cs:283; NewEmailWindow.cs:88 |
| documents.calendar.close_suggestions_button | Önerileri Kapat | Close Suggestions | DocumentsView.cs:299 |
| documents.calendar.add_button | Takvime Ekle | Add to Calendar | DocumentsView.cs:389 |
| error_report.title | Hata Bildirimi / GitHub Issue Aç | Report an Issue / Open a GitHub Issue | ErrorReportView.cs:38 |
| error_report.description | Başlığı ve ayrıntıları girin. Gönder düğmesi GitHub issue sayfasını tarayıcıda açar. | Enter a title and details. The Send button opens the GitHub issue page in your browser. | ErrorReportView.cs:44 |
| error_report.title_placeholder | Kısa ve açıklayıcı başlık | Short, descriptive title | ErrorReportView.cs:51 |
| error_report.submit_button | GitHub'da Issue Aç | Open Issue on GitHub | ErrorReportView.cs:72 |
| document_analyzer.title | Belge ve E-Posta Analizi (AI) | Document and Email Analysis (AI) | DocumentAnalyzerView.cs:49 |
| document_analyzer.load_file_button | Dosya Yükle | Load File | DocumentAnalyzerView.cs:66 |
| document_analyzer.summarize_button | Özetle | Summarize | DocumentAnalyzerView.cs:70 |
| document_analyzer.export_password_label | Dışa aktarma parolası: | Export password: | DocumentAnalyzerView.cs:91 |
| document_analyzer.email_from_placeholder | Kimden | From | DocumentAnalyzerView.cs:110 |
| document_analyzer.email_subject_placeholder | Konu | Subject | DocumentAnalyzerView.cs:119 |
| document_analyzer.analyze_email_button | E-Postayı Analiz Et | Analyze Email | DocumentAnalyzerView.cs:137 |
| ai_overview.title | AI Genel Bakış | AI Overview | AIOverviewView.cs:37 |
| app.window_title | MultiSych - Cloud AI Platform | MultiSych - Cloud AI Platform | MainWindow.cs:55 — ❓ EMİN DEĞİLİM: ürün adı ve sloganı yerelleştirilmeyebilir |
| app.title | MultiSych | MultiSych | MainWindow.cs:97 — ❓ EMİN DEĞİLİM: marka adı çevrilmemeli olabilir; mevcut JSON anahtarı |
| main.ai_assistants_title | AI ASSISTANTS | AI ASSISTANTS | MainWindow.cs:135 — ❓ EMİN DEĞİLİM: mevcut literal zaten İngilizce |
| main.ai.copilot_button | 🤖 Microsoft Copilot | 🤖 Microsoft Copilot | MainWindow.cs:141 |
| main.ai.gemini_button | ✨ Google Gemini | ✨ Google Gemini | MainWindow.cs:145 |
| main.ai.yandex_button | 🧠 Yandex AI | 🧠 Yandex AI | MainWindow.cs:149 |
| app.version_label | MultiSych v1.0.0-beta | MultiSych v1.0.0-beta | MainWindow.cs:161 — ❓ EMİN DEĞİLİM: sürüm metni kaynak sözlük yerine biçimlendirilmiş olabilir |
| main.ram_usage_label | RAM Usage: | Memory Usage: | MainWindow.cs:166 — ❓ EMİN DEĞİLİM: mevcut literal zaten İngilizce |
| common.calculating | Calculating... | Calculating… | MainWindow.cs:168 — ❓ EMİN DEĞİLİM: mevcut literal zaten İngilizce |
| security.window_title | MultiSych Güvenlik Doğrulaması | MultiSych Security Verification | SecurityGateWindow.cs:23 |
| security.title | Başlangıç Güvenlik Kontrolü | Startup Security Check | SecurityGateWindow.cs:47 |
| security.password_label | Parola | Password | SecurityGateWindow.cs:55 |
| security.two_factor_placeholder | 6 haneli 2FA kodu | 6-digit 2FA code | SecurityGateWindow.cs:73 |
| security.exit_button | Çıkış | Quit | SecurityGateWindow.cs:94 |
| security.login_button | Giriş | Sign In | SecurityGateWindow.cs:98 |
| compose.window_title | Yeni E-Posta Oluştur | Compose New Email | NewEmailWindow.cs:21 |
| compose.to_label | Kime: | To: | NewEmailWindow.cs:39 |
| compose.subject_label | Konu: | Subject: | NewEmailWindow.cs:53 |
| compose.send_button | Gönder ✉️ | Send ✉️ | NewEmailWindow.cs:80 |
| chat.title | 💬 Sohbet | 💬 Chat | ChatView.cs:47 |
| chat.data_mode_off_button | 🗂️ Verilerim: Kapalı | 🗂️ My Data: Off | ChatView.cs:54 |
| chat.data_mode_tooltip | Açıkken sorular e-posta/dosya/takvim verilerinizde aranır (RAG). | When enabled, your questions are answered using your email, files, and calendar data (RAG). | ChatView.cs:55; AIChatWindow.cs:47 |
| chat.message_placeholder | Mesajınızı yazın… | Type your message… | ChatView.cs:74; AIChatWindow.cs:64 |
| chat.voice_message_tooltip | Sesli mesaj (dikte) | Voice message (dictation) | ChatView.cs:84 |
| add_account.window_title | Yeni Hesap Ekle | Add Account | AddAccountWindow.cs:19 |
| add_account.title | Bulut Hesabı Ekle | Add Cloud Account | AddAccountWindow.cs:35 |
| add_account.provider_prompt | Eklemek istediğiniz bulut sağlayıcısını seçin: | Select a cloud provider to add: | AddAccountWindow.cs:40 |
| add_account.google_button | 🤖 Google Drive / Gmail | 🤖 Google Drive / Gmail | AddAccountWindow.cs:44 |
| add_account.microsoft_button | ✨ Microsoft OneDrive / Outlook | ✨ Microsoft OneDrive / Outlook | AddAccountWindow.cs:51 |
| add_account.yandex_button | 🧠 Yandex Disk / Mail | 🧠 Yandex Disk / Mail | AddAccountWindow.cs:58 |
| file_explorer.up_button | ⬆️ Üst Klasör | ⬆️ Up One Folder | FileExplorerView.cs:48 |
| common.refresh_button | 🔄 Yenile | 🔄 Refresh | FileExplorerView.cs:52; EmailView.cs:66 |
| accounts.title | Bağlı Hesaplar | Connected Accounts | AccountsView.cs:45 |
| accounts.add_button | Yeni Hesap Ekle ➕ | Add Account ➕ | AccountsView.cs:52 |
| accounts.unmount_button | Sürücüyü Ayır | Unmount Drive | AccountsView.cs:127 |
| accounts.mount_button | Sürücüyü Bağla | Mount Drive | AccountsView.cs:133 |
| accounts.sync_button | Senkronize Et 🔄 | Sync 🔄 | AccountsView.cs:139 |
| accounts.remove_button | Kaldır 🗑️ | Remove 🗑️ | AccountsView.cs:144 |
| email.compose_button | ✏️ Yeni E-posta | ✏️ Compose | EmailView.cs:61 |
| email.search_placeholder | E-postalarda ara… | Search emails… | EmailView.cs:93 |
| email.select_prompt | Bir e-posta seçin | Select an email | EmailView.cs:128 |
| email.delete_button | 🗑️ Sil | 🗑️ Delete | EmailView.cs:146 |
| dashboard.title | Genel Bakış | Overview | DashboardView.cs:49 |
| dashboard.header.ai_summary | ✨ Günün Yapay Zeka Özeti | ✨ Today's AI Summary | DashboardView.cs:63 |
| common.loading | Yükleniyor... | Loading… | DashboardView.cs:67 |
| common.ready | Ready. | Ready. | DashboardView.cs:97 — ❓ EMİN DEĞİLİM: mevcut literal zaten İngilizce |
| sync.title | Senkronizasyon Kontrolü | Sync Control | SyncView.cs:47 |
| sync.manual_options_title | Manuel Senkronizasyon Seçenekleri | Manual Sync Options | SyncView.cs:54 |
| sync.account_selection_title | Hesap Seçimi | Account Selection | SyncView.cs:58 |
| sync.type_title | Senkronizasyon Türü | Sync Type | SyncView.cs:79 |
| sync.start_button | Senkronizasyonu Başlat 🔄 | Start Sync 🔄 | SyncView.cs:104 |
| sync.analyze_emails_button | E-Postaları AI ile Analiz Et ✨ | Analyze Emails with AI ✨ | SyncView.cs:108 |
| settings.title | Ayarlar | Settings | SettingsView.cs:45 |
| settings.general_title | Genel Ayarlar | General Settings | SettingsView.cs:53 |
| settings.language_title | Dil | Language | SettingsView.cs:57 |
| settings.theme_title | Tema | Theme | SettingsView.cs:67 |
| settings.data_maintenance_title | Veri ve Bakım | Data and Maintenance | SettingsView.cs:79 |
| settings.database_backup_title | Veritabanı Yedekleme | Database Backup | SettingsView.cs:82 |
| settings.backup_button | Yedekle | Back Up | SettingsView.cs:83 |
| settings.database_restore_title | Yedekten Geri Yükleme | Restore from Backup | SettingsView.cs:89 |
| settings.restore_button | Geri Yükle | Restore | SettingsView.cs:90 |
| settings.cache_cleanup_title | Önbellek Temizleme | Cache Cleanup | SettingsView.cs:96 |
| settings.clear_cache_button | Önbelleği Temizle | Clear Cache | SettingsView.cs:97 |
| settings.toggle_logs_button | Durdur / Başlat | Pause / Resume | SettingsView.cs:123 |
| settings.clear_logs_button | Temizle | Clear | SettingsView.cs:125 |

## Özet

- İncelenen View dosyası: **16**
- Eşleşen literal kullanımı: **99**
- Önerilen benzersiz anahtar: **93**
- Birden fazla yerde geçen benzersiz metin: **5**
- Tekrar eden metin barındıran dosya: **6** (`DocumentsView.cs`,
  `NewEmailWindow.cs`, `ChatView.cs`, `AIChatWindow.cs`,
  `FileExplorerView.cs`, `EmailView.cs`)
- Ortaklaştırılan tekrarlar: `Gönder`, `İptal`, `🔄 Yenile`,
  `Mesajınızı yazın…` ve RAG veri modu tooltip metni.

## Belirsizlik notları

- `app.window_title`, `app.title` ve `app.version_label` ürün
  markası/sürüm bilgisi içeriyor. Kaynak sözlüğe alınmaları envanter
  bütünlüğü için önerildi; gerçekten çevrilip çevrilmeyecekleri baş
  mühendis kararına bırakıldı.
- `main.ai_assistants_title`, `main.ram_usage_label`,
  `common.calculating` ve `common.ready` Türkçe arayüzde hâlihazırda
  İngilizce literal. Tabloda mevcut değer bozulmadan gösterildi.
