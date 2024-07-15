using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Text.RegularExpressions;
using System.IO;
using MimeDetective;
using MimeDetective.Definitions;

#if ANDROID
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using FileProvider = Microsoft.Maui.Storage.FileProvider;
#endif
using System.Diagnostics;

namespace YoutubeDownloader
{
    public partial class MainPage : ContentPage
    {
        private string _downloadedFilePath;
        private readonly ContentInspector _inspector;

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
            string url = YouTubeLink.Text;
            bool isVideo = DownloadVideo.IsChecked;

            if (string.IsNullOrEmpty(url))
            {
                StatusLabel.Text = "Please enter a YouTube link.";
                return;
            }

            StatusLabel.Text = "Downloading...";
            OpenFileButton.IsVisible = false;
            CopyPathButton.IsVisible = false;

            try
            {
                await DownloadYouTubeContent(url, isVideo);
                StatusLabel.Text = "Download complete!";
                OpenFileButton.IsVisible = true;
                CopyPathButton.IsVisible = true;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
        }

        private async Task DownloadYouTubeContent(string url, bool isVideo)
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
            });

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
            await youtube.Videos.Streams.DownloadAsync(streamInfo, tempFilePath);

            // Determine MIME type and proper extension
            string mimeType = GetMimeTypeFromContent(tempFilePath);
            string extension = GetExtensionFromMimeType(mimeType);

            if (string.IsNullOrEmpty(extension))
            {
                extension = isVideo ? "mp4" : "m4a";
            }

            var finalFileName = $"{SanitizeFileName(video.Title)}.{extension}";
            var finalFilePath = Path.Combine(downloadsPath, finalFileName);

            // Rename the temporary file to the final file name
            File.Move(tempFilePath, finalFilePath);

            // Store the file path for later use
            _downloadedFilePath = finalFilePath;
        }

        private void OnOpenFileButtonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_downloadedFilePath))
            {
                OpenDownloadedFile(_downloadedFilePath);
            }
        }

        private void OpenDownloadedFile(string filePath)
        {
#if ANDROID
            var file = new Java.IO.File(filePath);
            var mimeType = GetMimeTypeFromContent(filePath);
            var uri = FileProvider.GetUriForFile(Android.App.Application.Context, $"{Android.App.Application.Context.PackageName}.fileprovider", file);

            Intent intent = new Intent(Intent.ActionView);
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
            intent.SetDataAndType(uri, mimeType);

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

            return results.ByMimeType().FirstOrDefault()?.MimeType  ?? "application/octet-stream";
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

        private void OnClearButtonClicked(object sender, EventArgs e)
        {
            YouTubeLink.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            OpenFileButton.IsVisible = false;
        }

        private async void OnCopyPathButtonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_downloadedFilePath))
            {
#if ANDROID
                var clipboard = (ClipboardManager)Android.App.Application.Context.GetSystemService(Context.ClipboardService);
                var clip = ClipData.NewPlainText("File Path", _downloadedFilePath);
                clipboard.PrimaryClip = clip;
#elif WINDOWS
                await Clipboard.Default.SetTextAsync(_downloadedFilePath);
#endif
                StatusLabel.Text = "File path copied to clipboard!";
            }
        }
    }
}
