using System;
using System.Linq;
using System.Windows;

namespace GraphSimulator
{
    public partial class App : Application
    {
        public static string? FileToOpen { get; private set; }
        public static bool AutoExecute { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check command-line arguments
            if (e.Args.Length > 0)
            {
                FileToOpen = e.Args[0];
                
                // Check if --execute flag is present
                AutoExecute = e.Args.Any(arg => arg.Equals("--execute", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
