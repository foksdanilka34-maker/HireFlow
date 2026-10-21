using HireFlow.Web.Api;
using HireFlow.Web.Vacancies;

namespace HireFlow.Web.Tests;

public class VacancyStatusTextTests
{
    // Действия строго по статусу, в порядке кнопок.
    [Theory]
    [InlineData(VacancyStatus.Open, new[] { VacancyAction.Close, VacancyAction.Archive })]
    [InlineData(VacancyStatus.Closed, new[] { VacancyAction.Open, VacancyAction.Archive })]
    [InlineData(VacancyStatus.Archived, new[] { VacancyAction.Unarchive })]
    public void Actions_ReturnsOnlyTransitionsAllowedForStatus(VacancyStatus status, VacancyAction[] expected)
    {
        Assert.Equal(expected, VacancyStatusText.Actions(status));
    }

    [Theory]
    [InlineData(VacancyStatus.Open, true)]
    [InlineData(VacancyStatus.Closed, true)]
    [InlineData(VacancyStatus.Archived, false)]
    public void CanEdit_IsFalseOnlyForArchived(VacancyStatus status, bool expected)
    {
        Assert.Equal(expected, VacancyStatusText.CanEdit(status));
    }
}
