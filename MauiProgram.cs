﻿﻿﻿﻿﻿using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives;

namespace YoutubeDownloader;

public static class MauiProgram
{
    private static string _ffmpegPath;

    public static string FFmpegPath
    {
        get
        {
            if (string.IsNullOrEmpty(_ffmpegPath))
            {
                _ffmpegPath = EnsureFFmpegAvailable();
            }
            return _ffmpegPath;
        }
    }

    private static string EnsureFFmpegAvailable()
    {
        try
        {
#if WINDOWS
            var ffmpegDir = Path.Combine(FileSystem.Current.AppDataDirectory, "ffmpeg");
            var ffmpegPath = Path.Combine(ffmpegDir, "ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
            {
                Directory.CreateDirectory(ffmpegDir);
                
                // Copiar o arquivo ffmpeg.7z do diretório de recursos
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("YoutubeDownloader.Resources.Raw.ffmpeg.7z"))
                using (var archive = SevenZipArchive.Open(stream))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Key.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.WriteToFile(ffmpegPath);
                            break;
                        }
                    }
                }
            }
            return ffmpegPath;
#elif ANDROID
            var ffmpegDir = Path.Combine(FileSystem.Current.CacheDirectory, "ffmpeg");
            var ffmpegPath = Path.Combine(ffmpegDir, "ffmpeg");

            if (!File.Exists(ffmpegPath))
            {
                Directory.CreateDirectory(ffmpegDir);

                // Copiar o arquivo ffmpeg.7z do diretório de recursos
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("YoutubeDownloader.Resources.Raw.ffmpeg.7z"))
                using (var archive = SevenZipArchive.Open(stream))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Key.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.WriteToFile(ffmpegPath);
                            break;
                        }
                    }
                }

                // Definir permissões de execução no Android
                Java.IO.File file = new Java.IO.File(ffmpegPath);
                file.SetExecutable(true, false);
            }
            return ffmpegPath;
#else
            return string.Empty;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao extrair FFmpeg: {ex}");
            throw;
        }
    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Garantir que o FFmpeg esteja disponível durante a inicialização
        var _ = FFmpegPath;

        return builder.Build();
    }
}
