using ComicReader.Infrastructure.Storage;

namespace ComicReader.ViewModels;

public sealed class DownloadsViewModel
{
	public string DownloadFolder { get; }
	public string QueueStatus => "Belum ada chapter dalam antrean.";

	public DownloadsViewModel()
	{
		var preferences = new UserPreferencesService().Load();
		DownloadFolder = string.IsNullOrWhiteSpace(preferences.DownloadFolder)
			? "Default: LocalAppData\\ComicReader\\Downloads"
			: preferences.DownloadFolder;
	}
}