using System.Diagnostics;

namespace PhasmoAssist
{
    internal class OffsetStopwatch
    {
        private readonly Stopwatch _sw = new();
        private TimeSpan _offset;

        public OffsetStopwatch()
        {
            _offset = TimeSpan.Zero;
        }

        public OffsetStopwatch(TimeSpan startOffset)
        {
            _offset = startOffset;
        }

        public void Start() => _sw.Start();
        
        public void Start(TimeSpan elapsedSw)
        {
            _offset = elapsedSw;
            _sw.Start();
        }

        public void Stop() => _sw.Stop();

        public void Reset()
        {
            _sw.Reset();
            _offset = TimeSpan.Zero;
        }

        public TimeSpan Elapsed => _offset + _sw.Elapsed;
    }
}
