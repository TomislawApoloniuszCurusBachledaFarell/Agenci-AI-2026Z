using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace SteamAdvisor.App.Skills;

public sealed class MarkdownCleanupSkill : ISkill
{
    public string Name => "markdown_cleanup";

    public string Instructions =>
        """
        Zastosuj teraz te zasady do całej odpowiedzi. Nie wypisuj tej listy użytkownikowi.
        Pisz wszytsko w plain text. Usuń artefakty Markdown i HTML, bo konsola pokazuje znaki dosłownie:
        - nagłówki zaczynające się od # zamień na zwykłe zdanie
        - **pogrubienie**, __pogrubienie__, *kursywę* i _kursywę_ zamień na sam tekst
        - `kod` oraz bloki otoczone ``` zamień na zwykły tekst, bez odwrotnych apostrofów
        - link [opis](adres) zamień na opis albo sam adres
        - cytat zaczynający się od > zamień na zwykłe zdanie
        - nie zostawiaj samotnych znaków *, _, #, `, ~ ani znaczników HTML
        - jeśli * jest używana jako znak do wypisywania zamień ją na -
        Tabeli nie składaj w Markdownie. Każdą tabelę zbuduj wyłącznie narzędziem table_formatting i wklej jego wynik znak w znak.
        Napisz teraz oczyszczoną odpowiedź.
        """;

}
