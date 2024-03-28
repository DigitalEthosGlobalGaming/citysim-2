

using Sandbox;

namespace Degg.Util
{
	public partial class AdvLog
	{
		enum ReportSeverity
		{
			Info,
			Warning,
			Error
		}

		// This function could report to a database.
		private static void Report( string message, ReportSeverity severity = ReportSeverity.Info )
		{
			
		}

		private static void ClientPrint( object message, ReportSeverity severity = ReportSeverity.Info )
		{
			return;
				//switch ( severity )
				//{
				//	case ReportSeverity.Info:
				//		AdvLog.Info( $"[CLIENT] {message}" );
				//		break;
				//	case ReportSeverity.Warning:
				//		AdvLog.Warning( $"[CLIENT] {message}" );
				//		break;
				//	case ReportSeverity.Error:
				//		AdvLog.Error( $"[CLIENT] {message}" );
				//		break;
				//}
		}

		private static void ServerPrint( object message, ReportSeverity severity = ReportSeverity.Info )
		{
				switch ( severity )
				{
					case ReportSeverity.Info:
						Log.Info( $"[SERVER] {message}" );
						break;
					case ReportSeverity.Warning:
						Log.Warning( $"[SERVER] {message}" );
						break;
					case ReportSeverity.Error:
						Log.Error( $"[SERVER] {message}" );
						break;
				}
		}

		public static void Info( params object[] args )
		{
			string message = "";
			foreach ( object arg in args )
			{
				message += arg.ToString();
				message += "\t";
			}

			ClientPrint( message.ToString() );
			ServerPrint( message.ToString() );
		}

		public static void Warning( params object[] args )
		{
			string message = "";
			foreach ( object arg in args )
			{
				message += arg.ToString();
				message += "\t";
			}

			ClientPrint( message.ToString(), ReportSeverity.Warning );
			ServerPrint( message.ToString(), ReportSeverity.Warning );
		}

		public static void Error( params object[] args )
		{
			string message = "";
			foreach ( object arg in args )
			{
				message += arg.ToString();
				message += "\t";
			}

			ClientPrint( message.ToString(), ReportSeverity.Error );
			ServerPrint( message.ToString(), ReportSeverity.Error );
		}
	}
}
