using System;
using System.IO;

namespace Shared.MyLogging
{

    public class Logger
    {
        private string _logFilePath;

        public Logger(string logFilePath)
        {
            _logFilePath = logFilePath;

            // 确保日志文件存在
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Close();
            }
        }

        public void Log(string message)
        {
            // 使用追加模式打开文件
            using (StreamWriter writer = new StreamWriter(_logFilePath, true))
            {
                writer.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        public void Log(Exception exception)
        {
            Log($"Exception: {exception}");
        }

        public void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }
    }
}