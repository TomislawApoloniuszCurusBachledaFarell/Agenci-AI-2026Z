namespace SteamAdvisor.App.Skills;

public sealed class GameTitleSkill : ISkill
{
    public string Name => "game_title";

    public string Instructions =>
        """
        Gdy użytkownik poda grę skrótem, potocznie albo po polsku, ustal oficjalną nazwę na Steamie zanim wywołasz narzędzie.
        Jeśli skrót lub nazwa jest jednoznaczna (np. BG3, RDR2, CS2, Wiedźmin 3), rozwiń ją samodzielnie do oficjalnego tytułu i od razu wywołaj potrzebną funkcję.
        Jeśli nazwa jest niejednoznaczna (sama seria, np. Wiedźmin, Fallout, GTA, albo wieloznaczny skrót, np. GoW — God of War albo Gears of War) i nie wynika to z rozmowy, nie wywołuj jeszcze narzędzia. Krótko dopytaj, o który tytuł chodzi, i wymień najwyżej 2–4 najpopularniejsze możliwości.
        """;
}
