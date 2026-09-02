using System.Xml.Linq;

namespace SquadUp.ArchitectureTests;

public sealed class LobbyLayerDependencyTests
{
    [Fact]
    public void LobbyProjectsMustHaveOnlyTheAllowedDirectDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var rules = new[]
        {
            new DependencyRule(
                "src/Lobby/SquadUp.LobbyService.Domain/SquadUp.LobbyService.Domain.csproj",
                []),
            new DependencyRule(
                "src/Lobby/SquadUp.LobbyService.Application/SquadUp.LobbyService.Application.csproj",
                ["SquadUp.LobbyService.Domain"]),
            new DependencyRule(
                "src/Lobby/SquadUp.LobbyService.Infrastructure/SquadUp.LobbyService.Infrastructure.csproj",
                ["SquadUp.LobbyService.Application"]),
            new DependencyRule(
                "src/Lobby/SquadUp.LobbyService.Api/SquadUp.LobbyService.Api.csproj",
                [
                    "SquadUp.LobbyService.Application",
                    "SquadUp.LobbyService.Infrastructure",
                    "SquadUp.ServiceDefaults"
                ])
        };

        foreach (var rule in rules)
        {
            var projectPath = Path.Combine(repositoryRoot, rule.ProjectPath);
            var project = XDocument.Load(projectPath);
            var actualDependencies = project
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)
                .Select(reference => reference.Replace('\\', '/'))
                .Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var expectedDependencies = rule.AllowedDependencies
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedDependencies, actualDependencies);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SquadUp.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private sealed record DependencyRule(
        string ProjectPath,
        IReadOnlyCollection<string> AllowedDependencies);
}
