using System.Xml.Linq;
using KaguERP.DatabaseIntegrationChecks;

if (args is ["database"])
{
    return await DatabaseIntegrationCheck.RunAsync();
}

if (args is ["seed-auth-smoke"])
{
    return await AuthSmokeFixture.SeedAsync();
}

if (args is ["cleanup-auth-smoke"])
{
    return await AuthSmokeFixture.CleanupAsync();
}

var repositoryRoot = FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory));
var sourceRoot = Path.Combine(repositoryRoot.FullName, "src");
var projectPaths = Directory
    .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
    .Select(Path.GetFullPath)
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var projects = projectPaths.ToDictionary(
    path => path,
    path => ReadProject(path),
    StringComparer.OrdinalIgnoreCase);

var failures = new List<string>();

foreach (var project in projects.Values)
{
    foreach (var referencePath in project.ReferencePaths)
    {
        if (!projects.TryGetValue(referencePath, out var referencedProject))
        {
            failures.Add($"{project.RelativePath} bilinmeyen source projesine başvuruyor: {referencePath}");
            continue;
        }

        if (!AllowedLayers.Value[project.Layer].Contains(referencedProject.Layer))
        {
            failures.Add(
                $"{project.RelativePath}: {project.Layer} katmanı " +
                $"{referencedProject.Layer} katmanına başvuramaz ({referencedProject.RelativePath}).");
        }

        if (referencedProject.Layer == Layer.Infrastructure &&
            project.ModuleName is not null &&
            referencedProject.ModuleName is not null &&
            !string.Equals(project.ModuleName, referencedProject.ModuleName, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{project.RelativePath} başka modülün Infrastructure projesine başvuramaz " +
                $"({referencedProject.RelativePath}).");
        }
    }
}

if (failures.Count == 0)
{
    await ApiContractCheck.RunAsync();
    Console.WriteLine($"Architecture checks passed for {projects.Count} source projects.");
    return 0;
}

Console.Error.WriteLine("Architecture checks failed:");
foreach (var failure in failures)
{
    Console.Error.WriteLine($"- {failure}");
}

return 1;

ProjectInfo ReadProject(string projectPath)
{
    var relativePath = Path.GetRelativePath(repositoryRoot.FullName, projectPath);
    var document = XDocument.Load(projectPath);
    var references = document
        .Descendants("ProjectReference")
        .Select(element => element.Attribute("Include")?.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => NormalizeProjectReferencePath(projectPath, value!))
        .ToArray();

    return new ProjectInfo(
        relativePath,
        GetLayer(Path.GetFileNameWithoutExtension(projectPath)),
        GetModuleName(relativePath),
        references);
}

static string NormalizeProjectReferencePath(string projectPath, string referencePath)
{
    var platformPath = referencePath
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar);

    return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, platformPath));
}

static DirectoryInfo FindRepositoryRoot(DirectoryInfo start)
{
    for (var current = start; current is not null; current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "KaguERP.slnx")))
        {
            return current;
        }
    }

    throw new InvalidOperationException("KaguERP.slnx içeren repository kökü bulunamadı.");
}

static Layer GetLayer(string projectName)
{
    if (projectName.EndsWith(".Domain", StringComparison.Ordinal)) return Layer.Domain;
    if (projectName.EndsWith(".Contracts", StringComparison.Ordinal)) return Layer.Contracts;
    if (projectName.EndsWith(".Application", StringComparison.Ordinal)) return Layer.Application;
    if (projectName.EndsWith(".Infrastructure", StringComparison.Ordinal)) return Layer.Infrastructure;
    if (projectName.EndsWith(".Bootstrap", StringComparison.Ordinal)) return Layer.Bootstrap;
    if (projectName.EndsWith(".Migrator", StringComparison.Ordinal)) return Layer.Migrator;
    if (projectName.EndsWith(".Api", StringComparison.Ordinal)) return Layer.Api;
    if (projectName.EndsWith(".Worker", StringComparison.Ordinal)) return Layer.Worker;

    throw new InvalidOperationException($"Tanınmayan source proje katmanı: {projectName}");
}

static string? GetModuleName(string relativePath)
{
    var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var modulesIndex = Array.FindIndex(
        segments,
        segment => string.Equals(segment, "Modules", StringComparison.OrdinalIgnoreCase));

    return modulesIndex >= 0 && modulesIndex + 1 < segments.Length
        ? segments[modulesIndex + 1]
        : null;
}

internal sealed record ProjectInfo(
    string RelativePath,
    Layer Layer,
    string? ModuleName,
    IReadOnlyCollection<string> ReferencePaths);

internal enum Layer
{
    Domain,
    Contracts,
    Application,
    Infrastructure,
    Bootstrap,
    Migrator,
    Api,
    Worker,
}

internal static class AllowedLayers
{
    public static IReadOnlyDictionary<Layer, IReadOnlySet<Layer>> Value { get; } =
        new Dictionary<Layer, IReadOnlySet<Layer>>
        {
            [Layer.Domain] = new HashSet<Layer>(),
            [Layer.Contracts] = new HashSet<Layer>(),
            [Layer.Application] = new HashSet<Layer> { Layer.Domain, Layer.Contracts },
            [Layer.Infrastructure] = new HashSet<Layer> { Layer.Application, Layer.Domain, Layer.Contracts },
            [Layer.Bootstrap] = new HashSet<Layer> { Layer.Application, Layer.Infrastructure, Layer.Contracts },
            [Layer.Migrator] = new HashSet<Layer>(),
            [Layer.Api] = new HashSet<Layer> { Layer.Bootstrap, Layer.Application, Layer.Contracts },
            [Layer.Worker] = new HashSet<Layer> { Layer.Bootstrap, Layer.Application, Layer.Contracts },
        };
}
