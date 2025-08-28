using System;
using log4net.Appender;
using log4net.Core;

namespace ACUConsole
{
    public class CustomAppender : AppenderSkeleton
    {
        public static Action<string> MessageHandler { get; set; }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (loggingEvent.Level > Level.Debug)
            {
                MessageHandler?.Invoke(RenderLoggingEvent(loggingEvent));
            }
        }
    }
}