using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace YoutubeDownloader;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    const int RequestStorageId = 0;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestStoragePermission();
    }

    private void RequestStoragePermission()
    {
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.WriteExternalStorage) != (int)Permission.Granted)
        {
            ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.WriteExternalStorage }, RequestStorageId);
        }
    }
}
