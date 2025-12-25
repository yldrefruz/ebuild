using ebuild.cli;

namespace ebuild.Commands
{
    [Command("property get", Description = "get the value of a property")]
    public class PropertyGetCommand : BaseCommand
    {
        [Argument(0, Description = "the name of the property to get")]
        public string PropertyName = string.Empty;

        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (PropertyName == "ebuild.api.dll")
            {
                Console.WriteLine(EBuild.FindEBuildApiDllPath());
            }
            else
            {
                throw new Exception($"Unknown property '{PropertyName}'");
            }
            return 0;
        }
    }
}