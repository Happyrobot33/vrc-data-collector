public class VRCLog
{
    public FileStream? logStream = null;

    public void findVRCLog()
    {
        if (logStream != null)
        {
            logStream.Close();
            logStream = null;
        }

        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] logs = Directory.GetFiles(path + "/logs", "output_log_*.txt", SearchOption.TopDirectoryOnly);
        if (logs.Length == 0) return;

        Array.Sort(logs);
        string log = logs[logs.Length - 1];

        logStream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        //forward to the end to wait on it
        if (logStream.Length != 0) {
            logStream.Position = logStream.Length - 1;
        }
    }
}
