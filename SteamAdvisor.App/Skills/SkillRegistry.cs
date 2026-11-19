using System.Text;
using Microsoft.Extensions.AI;

namespace SteamAdvisor.App.Skills;

public sealed class SkillRegistry
{
    private readonly List<ISkill> _skills = [];

    public SkillRegistry Register(ISkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills.Add(skill);
        return this;
    }

    public IReadOnlyList<AITool> Tools => _skills.Select(skill => skill.Tool).OfType<AITool>().ToList();

    public bool IsSkill(string? name) =>
        name is not null && _skills.Exists(skill => string.Equals(skill.Name, name, StringComparison.Ordinal));

    public string BuildPromptSection()
    {
        if (_skills.Count == 0)
            return string.Empty;

        var prompt = new StringBuilder();
        prompt.AppendLine("Skille — stosuj je przy każdej odpowiedzi:");
        foreach (var skill in _skills)
        {
            var kind = skill.Tool is null
                ? "instrukcja, nie wywołuj jej jako narzędzia"
                : "wywołaj";
            prompt.Append("- ").Append(skill.Name).Append(" (").Append(kind).Append("): ").AppendLine(skill.Instructions);
        }

        return prompt.ToString().TrimEnd();
    }
}
