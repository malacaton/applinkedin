using System.Text;
using Ali.Cli;

Console.OutputEncoding = Encoding.UTF8;

return CliApp.Run(
    args,
    Console.Out,
    Console.Error,
    Environment.GetEnvironmentVariable,
    Environment.CurrentDirectory);
