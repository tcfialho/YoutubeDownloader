using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
#if ANDROID
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Engine;
using Android.Content;
using Android.Views.InputMethods;
using Android.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using FileProvider = Microsoft.Maui.Storage.FileProvider;
#endif

namespace YoutubeDownloader
{
    public partial class MainPage : ContentPage
    {
        private string _downloadedFilePath;
#if ANDROID
        private readonly dynamic _inspector;
#else
        private readonly dynamic _inspector;
#endif
        private CancellationTokenSource _cancellationTokenSource;

        public MainPage()
        {
            InitializeComponent();
#if ANDROID
            _inspector = new MimeDetective.ContentInspectorBuilder().Build();
#else
            _inspector = null;
#endif
        }

        private async void OnDownloadButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            string url = YouTubeLink.Text;
            bool isVideo = DownloadVideo.IsChecked;

            if (string.IsNullOrEmpty(url))
            {
                StatusLabel.Text = "Please enter a YouTube link.";
                return;
            }

            // Desabilita os controles
            YouTubeLink.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            PasteButton.IsEnabled = false;
            DownloadButton.Text = "Downloading...";
            StatusLabel.Text = "Downloading...";
            OpenFileButton.IsVisible = false;
            CopyPathButton.IsVisible = false;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await DownloadYouTubeContent(url, isVideo, _cancellationTokenSource.Token);
                StatusLabel.Text = "Download complete!";
                OpenFileButton.IsVisible = true;
                CopyPathButton.IsVisible = true;
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Download canceled.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Reabilita os controles
                YouTubeLink.IsEnabled = true;
                DownloadButton.IsEnabled = true;
                PasteButton.IsEnabled = true;
                DownloadButton.Text = "Download";
                _cancellationTokenSource = null;
            }
        }

        private async Task DownloadYouTubeContent(string url, bool isVideo, CancellationToken cancellationToken)
        {
            var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(url);
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

            // Define o caminho de download
            string downloadsPath;
#if ANDROID
            downloadsPath = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath);
#else
            downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#endif

            if (isVideo)
            {
                // Seleciona os melhores streams de áudio e vídeo (filtra para container MP4)
                var audioStream = streamManifest.GetAudioStreams()
                    .Where(s => s.Container == Container.Mp4)
                    .GetWithHighestBitrate();
                var videoStream = streamManifest.GetVideoStreams()
                    .Where(s => s.Container == Container.Mp4)
                    .GetWithHighestVideoQuality();

                var streams = new IStreamInfo[] { audioStream, videoStream };
                var outputFileName = $"{SanitizeFileName(video.Title)}.mp4";
                var finalFilePath = Path.Combine(downloadsPath, outputFileName);

                // Baixa e realiza o muxing via FFmpeg usando o caminho configurado
                await youtube.Videos.DownloadAsync(streams, new ConversionRequestBuilder(finalFilePath)
                    .SetFFmpegPath(MauiProgram.FFmpegPath)
                    .Build());
                _downloadedFilePath = finalFilePath;
            }
            else
            {
                // Áudio apenas: seleciona o melhor stream de áudio disponível
                var audioStream = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                var streams = new IStreamInfo[] { audioStream };
                var outputFileName = $"{SanitizeFileName(video.Title)}.mp3";
                var finalFilePath = Path.Combine(downloadsPath, outputFileName);

                await youtube.Videos.DownloadAsync(streams, new ConversionRequestBuilder(finalFilePath)
                    .SetFFmpegPath(MauiProgram.FFmpegPath)
                    .Build());
                _downloadedFilePath = finalFilePath;
            }
        }

        private void OnOpenFileButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            if (!string.IsNullOrEmpty(_downloadedFilePath))
            {
                OpenDownloadedFile(_downloadedFilePath);
            }
        }

        private void OpenDownloadedFile(string filePath)
        {
#if ANDROID
            var file = new Java.IO.File(filePath);
            var extension = Path.GetExtension(filePath);
            var mimeType = GetMimeTypeFromExtension(extension);
            var uri = FileProvider.GetUriForFile(Android.App.Application.Context, $"{Android.App.Application.Context.PackageName}.fileprovider", file);

            Intent intent = new Intent(Intent.ActionView);
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
            intent.SetDataAndType(uri, mimeType);

            // Adiciona categorias para ajudar na seleção do app correto
            intent.AddCategory(Intent.CategoryDefault);
            intent.AddCategory(Intent.CategoryBrowsable);

            Intent chooserIntent = Intent.CreateChooser(intent, "Open With");
            chooserIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

            Android.App.Application.Context.StartActivity(chooserIntent);
#else
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = Path.GetDirectoryName(filePath),
                FileName = Path.GetFileName(filePath),
                UseShellExecute = true
            };
            process.Start();
#endif
        }

        private string GetMimeTypeFromContent(string filePath)
        {
            var results = _inspector.Inspect(filePath);
            // Retorna o primeiro tipo MIME detectado
            var mimeType = results.ByMimeType().FirstOrDefault()?.MimeType;
            Debug.WriteLine($"Detected MIME type: {mimeType}");

            if (string.IsNullOrEmpty(mimeType))
            {
                mimeType = "application/octet-stream";
            }

            return mimeType;
        }

        private string GetExtensionFromMimeType(string mimeType)
        {
            return mimeType switch
            {
                "video/mp4" => "mp4",
                "audio/mpeg" => "mp3",
                "audio/mp4" => "m4a",
                _ => null,
            };
        }

        private string GetMimeTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".mp4" => "video/mp4",
                ".m4a" => "audio/mp4",
                ".mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }

        private string SanitizeFileName(string fileName)
        {
            var allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            var sanitizedFileName = new string(fileName.Select(c => allowedChars.Contains(c) ? c : '_').ToArray());
            const int maxLength = 100;
            if (sanitizedFileName.Length > maxLength)
            {
                sanitizedFileName = sanitizedFileName.Substring(0, maxLength);
            }
            return sanitizedFileName;
        }

        private async void OnPasteButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            // Desabilita os controles
            YouTubeLink.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            PasteButton.IsEnabled = false;

            try
            {
#if ANDROID
                await Task.Run(() =>
                {
                    var clipboard = (ClipboardManager)Android.App.Application.Context.GetSystemService(Context.ClipboardService);
                    if (clipboard.HasPrimaryClip && clipboard.PrimaryClip.Description.HasMimeType(ClipDescription.MimetypeTextPlain))
                    {
                        var item = clipboard.PrimaryClip.GetItemAt(0);
                        MainThread.BeginInvokeOnMainThread(() => YouTubeLink.Text = item.Text);
                    }
                });
#elif WINDOWS
                var text = await Clipboard.Default.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    YouTubeLink.Text = text;
                }
#endif
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Reabilita os controles
                YouTubeLink.IsEnabled = true;
                DownloadButton.IsEnabled = true;
                PasteButton.IsEnabled = true;
            }
        }

        private void OnClearButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            // Cancela o download em andamento
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
            }

            YouTubeLink.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            CopyPathButton.IsVisible = false;
            OpenFileButton.IsVisible = false;

            // Reabilita os controles
            YouTubeLink.IsEnabled = true;
            DownloadButton.IsEnabled = true;
            PasteButton.IsEnabled = true;
            DownloadButton.Text = "Download";
        }

        private async void OnCopyPathButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            if (!string.IsNullOrEmpty(_downloadedFilePath))
            {
#if ANDROID
                await Task.Run(() =>
                {
                    var clipboard = (ClipboardManager)Android.App.Application.Context.GetSystemService(Context.ClipboardService);
                    var clip = ClipData.NewPlainText("File Path", _downloadedFilePath);
                    clipboard.PrimaryClip = clip;
                });
#elif WINDOWS
                await Clipboard.Default.SetTextAsync(_downloadedFilePath);
#endif
                StatusLabel.Text = "File path copied to clipboard!";
            }
        }

        private void CloseKeyboard()
        {
#if ANDROID
            var inputMethodManager = (InputMethodManager)Android.App.Application.Context.GetSystemService(Context.InputMethodService);
            var currentFocus = (Android.App.Application.Context as MainActivity)?.CurrentFocus;
            if (currentFocus != null)
            {
                inputMethodManager.HideSoftInputFromWindow(currentFocus.WindowToken, HideSoftInputFlags.None);
                currentFocus.ClearFocus();
            }
#endif
        }
    }
}
