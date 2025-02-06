namespace YoutubeDownloader;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = new Window(new AppShell());
        
        // Você pode configurar a janela aqui se necessário
        // window.Width = 800;
        // window.Height = 600;
        
        return window;
    }
}
