using System.IO;
using System.Text;
using System.Text.Json;
using CitySim.Match;

namespace CitySim;

/// <summary>Local-only JSONL spool. No network calls; a future uploader can read closed recordings.</summary>
public static class LocalBalanceAnalytics
{
	public const string Folder="analytics/v1";
	const int MaxFiles=64,MaxRecordingBytes=2*1024*1024;
	public static BalanceAnalytics Start(MatchState state,RoundMachine rounds,Guid matchId,string mode,IEnumerable<Guid> bots=null,string difficulty=null,IReadOnlyDictionary<Guid,BotAnalyticsConfiguration> botConfigurations=null)
	{
		try
		{
			FileSystem.Data.CreateDirectory(Folder);
			if (FileSystem.Data.FindFile(Folder,"*.jsonl",false).Count()>=MaxFiles)
			{
				Log.Warning($"City Sim local analytics storage is full ({MaxFiles} recordings). Move or delete files in {Folder} to resume recording."); return null;
			}
			var path=$"{Folder}/{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.jsonl";
			var bytes=0;
			var result=new BalanceAnalytics(state,rounds,matchId,mode,bots,record =>
			{
				var line=JsonSerializer.Serialize(record)+"\n";
				var content=Encoding.UTF8.GetBytes(line);
				if (bytes+content.Length>MaxRecordingBytes) throw new IOException("Recording reached its 2 MiB limit; local tracking stopped for this recording.");
				using var stream=FileSystem.Data.OpenWrite(path,FileMode.Append);
				stream.Write(content,0,content.Length); bytes+=content.Length;
			},error=>Log.Warning($"City Sim local analytics stopped: {error.Message}"),difficulty,botConfigurations);
			Log.Info($"City Sim local analytics: {FileSystem.Data.GetFullPath(path) ?? path}");
			return result;
		}
		catch (Exception error) { Log.Warning($"Could not start City Sim local analytics: {error.Message}"); return null; }
	}
}
