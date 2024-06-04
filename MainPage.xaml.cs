using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
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

        public MainPage()
        {
            InitializeComponent();
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

            try
            {
                await DownloadYouTubeContent(url, isVideo);
                StatusLabel.Text = "Download complete!";
                OpenFileButton.IsVisible = true;
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
            string fileName = isVideo ? $"{video.Title}.mp4" : $"{video.Title}.mp3";
            var filePath = Path.Combine(downloadsPath, fileName);

            // Download stream
            await youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);

            // Store the file path for later use
            _downloadedFilePath = filePath;
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
            var mimeType = GetMimeType(filePath);
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

        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                _ => "*/*",
            };
        }

        private void OnClearButtonClicked(object sender, EventArgs e)
        {
            YouTubeLink.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            OpenFileButton.IsVisible = false;
        }
    }
}
