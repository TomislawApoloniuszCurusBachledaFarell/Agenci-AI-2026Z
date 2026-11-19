using Microsoft.Extensions.AI;

namespace SteamAdvisor.App.Skills;

public interface ISkill
{
    string Name { get; }

    string Instructions { get; }

    AITool? Tool => null;
}
