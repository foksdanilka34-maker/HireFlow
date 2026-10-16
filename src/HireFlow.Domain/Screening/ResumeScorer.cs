using HireFlow.Domain.Vacancies;

namespace HireFlow.Domain.Screening;

public static class ResumeScorer
{
    private const int RequiredWeight = 80;
    private const int NiceToHaveWeight = 20;

    // Оценивает резюме по требованиям вакансии: балл 0–100, вердикт и чего не хватает.
    public static ScreeningResult Evaluate(string resumeText, int yearsOfExperience, VacancyRequirements requirements)
    {
        var text = SkillMatcher.Normalize(resumeText);

        var missingRequired = FindMissing(text, requirements.RequiredSkills);
        var missingNiceToHave = FindMissing(text, requirements.NiceToHaveSkills);
        var experienceIsEnough = yearsOfExperience >= requirements.MinYearsOfExperience;

        var score = CalculateScore(requirements, missingRequired.Count, missingNiceToHave.Count);
        var missingCount = missingRequired.Count + (experienceIsEnough ? 0 : 1);
        var verdict = DecideVerdict(score, missingCount, requirements);

        return new ScreeningResult(score, verdict, missingRequired, missingNiceToHave, experienceIsEnough);
    }

    // Возвращает названия навыков, которых нет в тексте.
    private static List<string> FindMissing(string normalizedText, List<Skill> skills)
    {
        return skills.Where(s => !SkillMatcher.Contains(normalizedText, s)).Select(s => s.Name).ToList();
    }

    // Считает балл по доле найденных навыков: 80 — за обязательные, 20 — за желательные (если они есть).
    private static int CalculateScore(VacancyRequirements requirements, int missingRequired, int missingNiceToHave)
    {
        var requiredShare = FoundShare(requirements.RequiredSkills.Count, missingRequired);
        if (requirements.NiceToHaveSkills.Count == 0)
            return Round(100 * requiredShare);

        var niceToHaveShare = FoundShare(requirements.NiceToHaveSkills.Count, missingNiceToHave);
        return Round(RequiredWeight * requiredShare + NiceToHaveWeight * niceToHaveShare);
    }

    // Прошёл — всё найдено и балл не ниже порога; пограничный — близко к порогу или не хватает одного требования.
    private static ScreeningVerdict DecideVerdict(int score, int missingCount, VacancyRequirements requirements)
    {
        if (missingCount == 0 && score >= requirements.PassingScore)
            return ScreeningVerdict.Passed;

        if (missingCount == 1 || score >= requirements.PassingScore - requirements.BorderlineWidth)
            return ScreeningVerdict.Borderline;

        return ScreeningVerdict.Failed;
    }

    // Доля найденных навыков от 0 до 1.
    private static double FoundShare(int total, int missing) =>
        total == 0 ? 1 : (total - missing) / (double)total;

    // Округляет балл до целого: 2.5 → 3.
    private static int Round(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
