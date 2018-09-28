using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;

namespace SQLIO2
{
    [Command]
    [Subcommand("client", typeof(ClientCommand))]
    [Subcommand("proxy", typeof(ProxyCommand))]
    class Program
    {
        static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

        private int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();

            return 1;
        }
    }
}
