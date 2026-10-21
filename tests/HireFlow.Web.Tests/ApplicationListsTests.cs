using HireFlow.Web.Api;
using HireFlow.Web.Applications;

namespace HireFlow.Web.Tests;

public class ApplicationListsTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid VacancyA = Guid.NewGuid();
    private static readonly Guid VacancyB = Guid.NewGuid();

    // Элемент списка откликов; имя кандидата служит меткой в проверках.
    private static ApplicationListItemDto Item(
        string name, int score, int hoursAgo = 0, Guid? vacancy = null,
        ScreeningVerdict verdict = ScreeningVerdict.Passed, string title = "Vacancy") =>
        new(Guid.NewGuid(), vacancy ?? VacancyA, title, Guid.NewGuid(), name,
            ApplicationStatus.RecruiterReview, score, verdict, Start.AddHours(-hoursAgo));

    private static string[] Names(IEnumerable<ApplicationListItemDto> items) => items.Select(i => i.CandidateName).ToArray();

    [Fact]
    public void Queue_SortsByScoreDescendingAndEqualScoreOlderFirst()
    {
        var items = new[]
        {
            Item("low", 40),
            Item("newer-70", 70, hoursAgo: 1),
            Item("top", 90),
            Item("older-70", 70, hoursAgo: 5),
        };

        Assert.Equal(["top", "older-70", "newer-70", "low"], Names(ApplicationLists.Queue(items)));
    }

    [Fact]
    public void Filter_ByVacancyAndBorderlineKeepsOrder()
    {
        var items = new[]
        {
            Item("a-passed", 90, vacancy: VacancyA),
            Item("b-borderline", 60, vacancy: VacancyB, verdict: ScreeningVerdict.Borderline),
            Item("a-borderline-1", 65, vacancy: VacancyA, verdict: ScreeningVerdict.Borderline),
            Item("a-borderline-2", 62, vacancy: VacancyA, verdict: ScreeningVerdict.Borderline),
        };

        Assert.Equal(Names(items), Names(ApplicationLists.Filter(items, null, false)));
        Assert.Equal(["a-passed", "a-borderline-1", "a-borderline-2"], Names(ApplicationLists.Filter(items, VacancyA, false)));
        Assert.Equal(["b-borderline", "a-borderline-1", "a-borderline-2"], Names(ApplicationLists.Filter(items, null, true)));
        Assert.Equal(["a-borderline-1", "a-borderline-2"], Names(ApplicationLists.Filter(items, VacancyA, true)));
    }

    [Fact]
    public void Vacancies_AreDistinctAndSortedByTitle()
    {
        var items = new[]
        {
            Item("1", 50, vacancy: VacancyB, title: "Backend"),
            Item("2", 50, vacancy: VacancyA, title: "Analyst"),
            Item("3", 50, vacancy: VacancyB, title: "Backend"),
        };

        var vacancies = ApplicationLists.Vacancies(items);

        Assert.Equal([new VacancyOption(VacancyA, "Analyst"), new VacancyOption(VacancyB, "Backend")], vacancies);
    }
}
