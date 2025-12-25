using System.Threading;
using System.Threading.Tasks;
using ebuild.cli;

namespace ebuild.Commands
{
    public abstract class BaseCommand : Command
    {
        [Option("verbose", ShortName = "v", Description = "enable verbose logging")]
        public bool Verbose;

        public override Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (Verbose)
                EBuild.VerboseEnabled = true;
            return Task.FromResult(0);
        }
    }
}