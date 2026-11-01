using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Backpack.Sdk;
using Backpack.Visual.Plugins;

namespace Backpack.App;

/// <summary>
/// <c>Backpack.exe --plugin-host &lt;папка&gt;</c>: отдельный процесс одного плагина. Загружает сборку точки входа в
/// собственный <see cref="AssemblyLoadContext"/> (это не песочница — изоляцию даёт отдельный процесс и Job Object, см.
/// ADR-0001), находит тип из manifest (наследник <see cref="BackpackPlugin"/>) и выполняет протокол SDK через stdin/stdout.
/// Окон не создаёт, захват ввода не запускает, настроек Backpack не читает.
/// </summary>
internal static class PluginHostMode
{
    public static int Run(string directory)
    {
        var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
        string dir = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(dir, PluginManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            return 10;
        }

        var (m, _) = PluginManifest.Parse(File.ReadAllText(manifestPath));
        if (m is null || !SdkInfo.IsCompatible(m.Sdk))
        {
            return 11;
        }

        string entry = Path.GetFullPath(Path.Combine(dir, m.Entry));
        if (!entry.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(entry))
        {
            return 12;
        }

        var alc = new PluginLoadContext(entry);
        BackpackPlugin plugin;
        try
        {
            var asm = alc.LoadFromAssemblyPath(entry);
            var type = asm.GetType(m.Type, throwOnError: false);
            if (type is null || !typeof(BackpackPlugin).IsAssignableFrom(type) || type.IsAbstract)
            {
                return 13;
            }

            plugin = (BackpackPlugin)Activator.CreateInstance(type)!;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or TargetInvocationException or MissingMethodException)
        {
            return 14;
        }

        return PluginRunner.Run(plugin, stdin, stdout);
    }

    /// <summary>Зависимости плагина — из его папки; SDK и сборки платформы — общие (один тип BackpackPlugin).</summary>
    private sealed class PluginLoadContext(string entry) : AssemblyLoadContext("plugin", isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(entry);

        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(BackpackPlugin).Assembly.GetName().Name)
            {
                return null; // общий SDK из Backpack
            }

            return _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromAssemblyPath(path) : null;
        }
    }
}
