using System.Collections.ObjectModel;

namespace KIScheduler.Core.Domain;

public sealed class ProjectDefinition
{
    public ProjectDefinition(ProjectId id, string name, string rootPath, string targetBranch,
        IEnumerable<ValidationCommand>? validationCommands = null)
    {
        DomainValidation.Id(id.Value, nameof(id));
        Id = id;
        Name = DomainValidation.Required(name, nameof(name));
        RootPath = DomainValidation.Required(rootPath, nameof(rootPath));
        TargetBranch = DomainValidation.Required(targetBranch, nameof(targetBranch));
        ValidationCommands = new ReadOnlyCollection<ValidationCommand>((validationCommands ?? []).ToList());
    }

    public ProjectId Id { get; }
    public string Name { get; }
    public string RootPath { get; }
    public string TargetBranch { get; }
    public IReadOnlyList<ValidationCommand> ValidationCommands { get; }
}

public sealed record ValidationCommand
{
    public ValidationCommand(string executable, IEnumerable<string>? arguments = null, bool required = false)
    {
        Executable = DomainValidation.Required(executable, nameof(executable));
        Arguments = new ReadOnlyCollection<string>((arguments ?? []).Select(value => value ?? string.Empty).ToList());
        Required = required;
    }

    public string Executable { get; }
    public IReadOnlyList<string> Arguments { get; }
    public bool Required { get; }
}

public sealed class PlatformDefinition
{
    public PlatformDefinition(PlatformId id, string executable, IEnumerable<PlatformModel> models, int capacity = 1)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Executable = DomainValidation.Required(executable, nameof(executable));
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Die Kapazität muss größer als null sein.");
        }

        var configuredModels = models?.ToList() ?? throw new ArgumentNullException(nameof(models));
        if (configuredModels.Count == 0)
        {
            throw new ArgumentException("Mindestens ein Modell muss konfiguriert sein.", nameof(models));
        }

        if (configuredModels.Select(model => model.Id).Distinct().Count() != configuredModels.Count)
        {
            throw new ArgumentException("Modell-IDs müssen innerhalb einer Plattform eindeutig sein.", nameof(models));
        }

        Models = new ReadOnlyCollection<PlatformModel>(configuredModels);
        Capacity = capacity;
    }

    public PlatformId Id { get; }
    public string Executable { get; }
    public IReadOnlyList<PlatformModel> Models { get; }
    public int Capacity { get; }

    public bool Supports(ModelId modelId, EffortLevel effort) => Models.Any(model => model.Supports(modelId, effort));
}

public sealed class PlatformModel
{
    public PlatformModel(ModelId id, IEnumerable<EffortLevel> supportedEfforts)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        var efforts = supportedEfforts?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(supportedEfforts));
        if (efforts.Count == 0)
        {
            throw new ArgumentException("Mindestens eine Effort-Stufe muss unterstützt werden.", nameof(supportedEfforts));
        }

        SupportedEfforts = new ReadOnlyCollection<EffortLevel>(efforts);
    }

    public ModelId Id { get; }
    public IReadOnlyList<EffortLevel> SupportedEfforts { get; }
    public bool Supports(ModelId modelId, EffortLevel effort) => Id == modelId && SupportedEfforts.Contains(effort);
}
