using HireFlow.Domain.Vacancies;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class VacancyTests
{
    private static Vacancy NewVacancy() =>
        new("QA Engineer", "QA", "Описание", Requirements(["alpha"]), 90);

    private static Vacancy ArchivedVacancy()
    {
        var vacancy = NewVacancy();
        vacancy.Archive();
        return vacancy;
    }

    [Fact]
    public void Constructor_CreatesOpenVacancyAndTrimsTitleAndDirection()
    {
        var vacancy = new Vacancy("  QA Engineer  ", "  QA ", null, Requirements(["alpha"]), 90);

        Assert.Equal(VacancyStatus.Open, vacancy.Status);
        Assert.Equal("QA Engineer", vacancy.Title);
        Assert.Equal("QA", vacancy.Direction);
        Assert.Equal(90, vacancy.FreezePeriodDays);
    }

    [Fact]
    public void Update_TrimsTitleAndDirectionAndReplacesContent()
    {
        var vacancy = NewVacancy();
        var requirements = Requirements(["bravo"]);

        vacancy.Update("  Dev ", " Backend  ", "новое", requirements, 30);

        Assert.Equal("Dev", vacancy.Title);
        Assert.Equal("Backend", vacancy.Direction);
        Assert.Equal("новое", vacancy.Description);
        Assert.Same(requirements, vacancy.Requirements);
        Assert.Equal(30, vacancy.FreezePeriodDays);
    }

    [Fact]
    public void CloseAndOpen_ChangeStatus()
    {
        var vacancy = NewVacancy();

        vacancy.Close();
        Assert.Equal(VacancyStatus.Closed, vacancy.Status);

        vacancy.Open();
        Assert.Equal(VacancyStatus.Open, vacancy.Status);
    }

    [Fact]
    public void Archive_SetsArchivedStatus()
    {
        var vacancy = NewVacancy();

        vacancy.Archive();

        Assert.Equal(VacancyStatus.Archived, vacancy.Status);
    }

    [Fact]
    public void Update_ThrowsWhenArchived()
    {
        var vacancy = ArchivedVacancy();

        Assert.Throws<DomainException>(() => vacancy.Update("x", "y", null, Requirements(["a"]), 1));

        Assert.Equal("QA Engineer", vacancy.Title);
    }

    [Fact]
    public void Open_ThrowsWhenArchived()
    {
        var vacancy = ArchivedVacancy();

        Assert.Throws<DomainException>(() => vacancy.Open());
        Assert.Equal(VacancyStatus.Archived, vacancy.Status);
    }

    [Fact]
    public void Close_ThrowsWhenArchived()
    {
        var vacancy = ArchivedVacancy();

        Assert.Throws<DomainException>(() => vacancy.Close());
        Assert.Equal(VacancyStatus.Archived, vacancy.Status);
    }

    [Fact]
    public void Unarchive_FromArchivedBecomesClosed()
    {
        var vacancy = ArchivedVacancy();

        vacancy.Unarchive();

        Assert.Equal(VacancyStatus.Closed, vacancy.Status);
    }

    [Theory]
    [InlineData(VacancyStatus.Open)]
    [InlineData(VacancyStatus.Closed)]
    public void Unarchive_ThrowsWhenNotArchived(VacancyStatus status)
    {
        var vacancy = NewVacancy();
        if (status == VacancyStatus.Closed)
            vacancy.Close();

        Assert.Throws<DomainException>(() => vacancy.Unarchive());

        Assert.Equal(status, vacancy.Status);
    }

    [Fact]
    public void EnsureAcceptsApplications_PassesForOpen() =>
        NewVacancy().EnsureAcceptsApplications();

    [Fact]
    public void EnsureAcceptsApplications_ThrowsForClosed()
    {
        var vacancy = NewVacancy();
        vacancy.Close();

        Assert.Throws<DomainException>(() => vacancy.EnsureAcceptsApplications());
    }

    [Fact]
    public void EnsureAcceptsApplications_ThrowsForArchived() =>
        Assert.Throws<DomainException>(() => ArchivedVacancy().EnsureAcceptsApplications());
}
