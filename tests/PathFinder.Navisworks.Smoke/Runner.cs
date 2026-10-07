using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api.Automation;

internal static class RunNavisworksSmoke
{
    private static string install;
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 5 || args.Length > 6)
        {
            Console.Error.WriteLine("Usage: PathFinder.Navisworks.SmokeRunner.exe <Manage-install-directory> <model> <installed-AddinRibbon.dll> <SmokePlugin.dll> <results.json> [extract-model|background-transparency]");
            return 2;
        }
        install = Path.GetFullPath(args[0]);
        if (args.Length == 6 && args[5] != "extract-model" && args[5] != "background-transparency")
        {
            Console.Error.WriteLine("The optional mode must be extract-model or background-transparency.");
            return 2;
        }
        foreach (int index in new[] { 1, 2, 3 })
        {
            args[index] = Path.GetFullPath(args[index]);
            if (!File.Exists(args[index])) { Console.Error.WriteLine("Missing input: " + args[index]); return 2; }
        }
        args[4] = Path.GetFullPath(args[4]);
        Directory.CreateDirectory(Path.GetDirectoryName(args[4]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, arguments) =>
        {
            string path = Path.Combine(install, new AssemblyName(arguments.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Environment.SetEnvironmentVariable("PATH", install + ";" + Environment.GetEnvironmentVariable("PATH"));
        try { return Run(args); }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        Console.WriteLine("Starting a fresh invisible Manage instance.");
        using (var host = new NavisworksApplication())
        {
            host.DisableProgress();
            Console.WriteLine("Opening the model in the isolated in-memory test session.");
            host.OpenFile(args[1], new string[0]);
            host.AddPluginAssembly(args[2]);
            host.AddPluginAssembly(args[3]);
            Console.WriteLine("Running smoke checks inside Navisworks.");
            int result = host.ExecuteAddInPlugin("PathFinderSmoke.TEST", args.Length > 5 ? new[] { args[4], args[5] } : new[] { args[4] });
            Console.WriteLine("Smoke plugin exit code: " + result);
            return result;
        }
    }
}
