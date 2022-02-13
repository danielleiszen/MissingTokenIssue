using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wash2Door.Logging
{
    public class ServiceLogging
    {
        public static void InitializeLogger()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var swich = new LoggingLevelSwitch(environment == "Development" ? LogEventLevel.Debug : LogEventLevel.Information);

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(swich)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();
        }
    }
}
