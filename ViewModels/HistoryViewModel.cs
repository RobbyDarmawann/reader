using System.Collections.ObjectModel;
using ComicReader.Infrastructure.Reading;

namespace ComicReader.ViewModels;

public sealed class HistoryViewModel
{
	public ObservableCollection<ReadingProgress> Entries { get; } = new();

	public bool HasEntries => Entries.Count > 0;
	public bool IsEmpty => Entries.Count == 0;

	public HistoryViewModel()
	{
		try
		{
			foreach (var entry in new ReadingProgressService().GetAllLatest())
				Entries.Add(entry);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Gagal memuat history: {ex}");
		}
	}
}