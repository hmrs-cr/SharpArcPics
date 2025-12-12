using PicArchiver.Console;
using PicArchiver.Web;

var runWeb = args.FirstOrDefault() == "web-service";
return runWeb ? WebApp.Run() : CmdLineApp.Run(args);

