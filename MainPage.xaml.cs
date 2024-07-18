using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Text.RegularExpressions;
using System.IO;
using MimeDetective;
using MimeDetective.Definitions;
using System.Threading;
using System.Diagnostics;

#if ANDROID
using Android.Content;
using Android.Views.InputMethods;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using FileProvider = Microsoft.Maui.Storage.FileProvider;
#endif

namespace YoutubeDownloader
{
    public partial class MainPage : ContentPage
    {
        private string _downloadedFilePath;
        private readonly ContentInspector _inspector;
        private CancellationTokenSource _cancellationTokenSource;

        public MainPage()
        {
            InitializeComponent();
            _inspector = new ContentInspectorBuilder()
            {
                Definitions = MimeDetective.Definitions.Default.All()
            }.Build();
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

            // Desabilita a caixa de texto e os botões
            YouTubeLink.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            PasteButton.IsEnabled = false;
            DownloadButton.Text = "Downloading...";

            StatusLabel.Text = "Downloading...";
            OpenFileButton.IsVisible = false;
            CopyPathButton.IsVisible = false;

            // Inicializa o CancellationTokenSource
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
                // Habilita a caixa de texto e os botões ao finalizar
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

            // Get video info
            var video = await youtube.Videos.GetAsync(url);

            // Define a variável streamManifest
            StreamManifest streamManifest = null;

            // Use Task.Run para executar a operação assíncrona
            streamManifest = await Task.Run(async () =>
            {
                return await youtube.Videos.Streams.GetManifestAsync(video.Id);
            }, cancellationToken);

            // Select streams
            IStreamInfo streamInfo;
            if (isVideo)
            {
                streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            }
            else
            {
                streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            }

            // Set file path to Downloads folder
            string downloadsPath;
#if ANDROID
            downloadsPath = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath);
#else
            downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#endif
            var tempFileName = $"{SanitizeFileName(video.Title)}.tmp";
            var tempFilePath = Path.Combine(downloadsPath, tempFileName);

            // Download stream to a temporary file
            await youtube.Videos.Streams.DownloadAsync(streamInfo, tempFilePath, default, cancellationToken);

            // Determine MIME type and proper extension
            string mimeType = GetMimeTypeFromContent(tempFilePath);
            string extension = GetExtensionFromMimeType(mimeType);

            if (string.IsNullOrEmpty(extension))
            {
                extension = isVideo ? "mp4" : "m4a";
            }

            var finalFileName = $"{SanitizeFileName(video.Title)}.{extension}";
            var finalFilePath = Path.Combine(downloadsPath, finalFileName);

            // Remove existing file if it exists
            if (File.Exists(finalFilePath))
            {
                File.Delete(finalFilePath);
            }

            // Rename the temporary file to the final file name
            File.Move(tempFilePath, finalFilePath);

            // Store the file path for later use
            _downloadedFilePath = finalFilePath;
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

            // Adiciona um log para verificar o tipo MIME detectado
            Debug.WriteLine($"Detected MIME type: {mimeType}");

            if (string.IsNullOrEmpty(mimeType))
            {
                mimeType = "application/octet-stream"; // Tipo MIME padrão para conteúdo desconhecido
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
            // Substituir caracteres não permitidos
            var allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            var sanitizedFileName = new string(fileName.Select(c => allowedChars.Contains(c) ? c : '_').ToArray());

            // Limitar o tamanho do nome do arquivo
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

            // Desabilita a caixa de texto e os botões
            YouTubeLink.IsEnabled = false;
            DownloadButton.IsEnabled = false;
            PasteButton.IsEnabled = false;

            try
            {
#if ANDROID
                var clipboard = (ClipboardManager)Android.App.Application.Context.GetSystemService(Context.ClipboardService);
                if (clipboard.HasPrimaryClip && clipboard.PrimaryClip.Description.HasMimeType(ClipDescription.MimetypeTextPlain))
                {
                    var item = clipboard.PrimaryClip.GetItemAt(0);
                    YouTubeLink.Text = item.Text;
                }
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
                // Habilita a caixa de texto e os botões ao finalizar
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

            // Habilita a caixa de texto e os botões
            YouTubeLink.IsEnabled = true;
            DownloadButton.IsEnabled = true;
            PasteButton.IsEnabled = true;
            DownloadButton.Text = "Download";
        }

        private void OnCopyPathButtonClicked(object sender, EventArgs e)
        {
            CloseKeyboard();

            if (!string.IsNullOrEmpty(_downloadedFilePath))
            {
#if ANDROID
                var clipboard = (ClipboardManager)Android.App.Application.Context.GetSystemService(Context.ClipboardService);
                var clip = ClipData.NewPlainText("File Path", _downloadedFilePath);
                clipboard.PrimaryClip = clip;
#elif WINDOWS
                Clipboard.Default.SetTextAsync(_downloadedFilePath);
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
