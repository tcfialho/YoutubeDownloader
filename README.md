# YoutubeDownloader
YoutubeDownloader é um aplicativo MAUI para download de vídeos e áudios do YouTube.

## Recursos
- Download de vídeos do YouTube em alta qualidade
- Extração de áudio em formato MP3
- Suporte para Windows e Android
- Gerenciamento automático do FFmpeg (não requer instalação manual)

## Notas Técnicas
- O FFmpeg é incluído como um recurso incorporado no aplicativo
- A extração e configuração do FFmpeg é feita automaticamente na primeira execução
- O FFmpeg é armazenado em:
  - Windows: Pasta AppData do aplicativo
  - Android: Diretório de cache do aplicativo
