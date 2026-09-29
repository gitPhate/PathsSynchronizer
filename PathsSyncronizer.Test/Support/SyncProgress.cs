using PathsSynchronizer;

namespace PathsSyncronizer.Test.Support
{
    public sealed class SyncProgress : IProgress<HashProgress>
    {
        private readonly List<HashProgress> _reports = [];

        public HashProgress[] Reports
        {
            get
            {
                lock (_reports)
                {
                    return _reports.ToArray();
                }
            }
        }

        public void Report(HashProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }
}
