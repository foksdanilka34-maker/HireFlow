using System.Text.RegularExpressions;
using HireFlow.Bot.Api;
using HireFlow.Bot.Telegram;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireFlow.Bot.Dialog;

public partial class DialogHandler(
    IHireFlowApiClient api,
    IChatGateway chat,
    IOptions<BotOptions>? options = null,
    ILogger<DialogHandler>? logger = null)
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private const int MaxExperience = 50;
    private const int MaxListedVacancies = 100;

    private readonly int _pageSize = Math.Max(1, options?.Value.PageSize ?? 3);

    // Число лет опыта: «5» или «5 лет».
    [GeneratedRegex(@"^(\d{1,3})(?:\s*(?:лет|года|год))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExperienceText();

    // API или Telegram недоступны: диалог отвечает «попробуйте позже» и ничего не сохраняет.
    private sealed class ServiceUnavailableException : Exception;

    // Состояние одной обработки сообщения: сессия, ответы и кэш вакансий.
    private sealed class Turn(IncomingMessage message)
    {
        public IncomingMessage Message { get; } = message;
        public BotSessionState Session { get; set; } = new() { Step = BotSteps.AwaitingResume };
        public List<string> Replies { get; } = [];
        public bool Save { get; set; }
        public Dictionary<Guid, VacancyDto> Vacancies { get; } = [];
        public HashSet<Guid> NotFound { get; } = [];
    }

    // Обрабатывает одно входящее сообщение целиком: меняет сессию через API и отвечает кандидату.
    public async Task HandleAsync(IncomingMessage message, CancellationToken ct)
    {
        var turn = new Turn(message);

        try
        {
            await ProcessAsync(turn, ct);

            if (turn.Save)
            {
                var saved = await api.PutSessionAsync(message.UserId, turn.Session, ct);
                if (!saved.IsOk)
                    throw new ServiceUnavailableException();
            }
        }
        catch (ServiceUnavailableException)
        {
            logger?.LogWarning("Dialog turn failed at step {Step}", turn.Session.Step);
            turn.Replies.Clear();
            turn.Replies.Add(BotTexts.Error);
        }

        foreach (var reply in turn.Replies)
            await chat.SendTextAsync(message.ChatId, reply, ct);
    }

    // Выбирает обработчик: команда, документ или текст по текущему шагу.
    private async Task ProcessAsync(Turn turn, CancellationToken ct)
    {
        var text = turn.Message.Text?.Trim();
        var (command, argument) = ParseCommand(text);

        if (command == "/start")
        {
            await StartAsync(turn, argument, ct);
            return;
        }

        await LoadSessionAsync(turn, ct);

        switch (command)
        {
            case "/help":
                turn.Replies.Add(BotTexts.About + "\n\n" + StepHint(turn.Session));
                return;
            case "/status":
                await ShowStatusAsync(turn, ct);
                return;
        }

        turn.Save = true;

        if (turn.Message.Document is not null)
            await HandleDocumentAsync(turn, turn.Message.Document, ct);
        else if (string.IsNullOrEmpty(text) || command is not null)
            turn.Replies.Add(StepHint(turn.Session));
        else
            await HandleTextAsync(turn, text, ct);
    }

    // Выделяет команду и аргумент: «/start@bot abc» → («/start», «abc»); не команда → (null, null).
    private static (string? Command, string? Argument) ParseCommand(string? text)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '/')
            return (null, null);

        var parts = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0].Split('@')[0].ToLowerInvariant();
        return (command, parts.Length > 1 ? parts[1] : null);
    }

    // Загружает сессию; нет сессии — новая на шаге «жду резюме».
    private async Task LoadSessionAsync(Turn turn, CancellationToken ct)
    {
        var result = await api.GetSessionAsync(turn.Message.UserId, ct);
        switch (result.Status)
        {
            case ApiStatus.Ok when result.Value is not null:
                turn.Session = result.Value;
                turn.Session.Draft ??= new BotDraft();
                break;
            case ApiStatus.NotFound:
                break;
            default:
                throw new ServiceUnavailableException();
        }
    }

    // /start: сбрасывает сессию; с id открытой вакансии привязывает её.
    private async Task StartAsync(Turn turn, string? argument, CancellationToken ct)
    {
        turn.Save = true;
        turn.Session = new BotSessionState { Step = BotSteps.AwaitingResume };

        if (string.IsNullOrEmpty(argument))
        {
            turn.Replies.Add(BotTexts.Welcome);
            return;
        }

        if (!Guid.TryParse(argument, out var vacancyId))
        {
            turn.Replies.Add(BotTexts.ClosedLinked);
            return;
        }

        var vacancy = await GetVacancyAsync(turn, vacancyId, ct);
        if (vacancy is { Status: "open" })
        {
            turn.Session.LinkedVacancyId = vacancy.Id;
            turn.Replies.Add(BotTexts.LinkedWelcome(vacancy.Title));
        }
        else
        {
            turn.Replies.Add(BotTexts.ClosedLinked);
        }
    }

    // /status: показывает отклики кандидата, шаг не меняется.
    private async Task ShowStatusAsync(Turn turn, CancellationToken ct)
    {
        var result = await api.GetCandidateApplicationsAsync(turn.Message.UserId, ct);
        if (!result.IsOk || result.Value is null)
            throw new ServiceUnavailableException();

        turn.Replies.Add(result.Value.Count == 0 ? BotTexts.NoApplications : BotTexts.Applications(result.Value));
    }

    // Разбирает текст кандидата в зависимости от шага.
    private async Task HandleTextAsync(Turn turn, string text, CancellationToken ct)
    {
        switch (turn.Session.Step)
        {
            case BotSteps.AwaitingExperience:
                await HandleExperienceAsync(turn, text, ct);
                break;
            case BotSteps.ChoosingVacancy:
                await HandleChoiceAsync(turn, text, ct);
                break;
            case BotSteps.ConfirmingLinked:
                await HandleConfirmationAsync(turn, text, ct);
                break;
            case BotSteps.AwaitingPhone:
                await HandlePhoneAsync(turn, text, ct);
                break;
            default:
                turn.Replies.Add(BotTexts.SendResumeHint);
                break;
        }
    }

    // Принимает резюме на любом шаге: подбор без опыта, затем вопрос об опыте или показ вакансий.
    private async Task HandleDocumentAsync(Turn turn, IncomingDocument document, CancellationToken ct)
    {
        if (document.FileSize > MaxFileSize)
        {
            turn.Replies.Add(BotTexts.FileTooBig);
            return;
        }

        var file = await DownloadAsync(document.FileId, ct);
        var result = await api.FindMatchesAsync(
            turn.Message.UserId, file, document.FileName ?? "resume", null, ct);

        if (result.Status == ApiStatus.BadRequest)
        {
            turn.Replies.Add(BotTexts.UnreadableFile);
            return;
        }
        if (!result.IsOk || result.Value is null)
            throw new ServiceUnavailableException();

        var draft = new BotDraft
        {
            ResumeFileId = document.FileId,
            ResumeFileName = document.FileName,
            YearsOfExperience = result.Value.YearsOfExperience,
            PhoneFound = result.Value.PhoneFound,
        };
        await ApplyMatchesAsync(turn, draft, result.Value, ct);
    }

    // Применяет результат подбора: либо спрашивает опыт, либо показывает вакансии.
    private async Task ApplyMatchesAsync(Turn turn, BotDraft draft, MatchesDto matches, CancellationToken ct)
    {
        turn.Session.Draft = draft;

        if (matches.ExperienceRequired)
        {
            turn.Session.Step = BotSteps.AwaitingExperience;
            turn.Replies.Add(BotTexts.AskExperience);
            return;
        }

        foreach (var vacancy in matches.Vacancies)
            turn.Vacancies[vacancy.Id] = vacancy with { Status = "open" };

        draft.VacancyIds = matches.Vacancies.Select(v => v.Id).Take(MaxListedVacancies).ToList();
        draft.Page = 0;
        await RenderChoiceAsync(turn, ct);
    }

    // Шаг «опыт»: число 0–50 — повторный подбор с опытом.
    private async Task HandleExperienceAsync(Turn turn, string text, CancellationToken ct)
    {
        var match = ExperienceText().Match(text.Trim().TrimEnd('.', '!', ',', ';', '?', '…').Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var years) || years > MaxExperience)
        {
            turn.Replies.Add(BotTexts.ExperienceHint);
            return;
        }

        var draft = turn.Session.Draft;
        if (draft.ResumeFileId is null)
        {
            ResetToAwaitingResume(turn);
            return;
        }

        var file = await DownloadAsync(draft.ResumeFileId, ct);
        var result = await api.FindMatchesAsync(turn.Message.UserId, file, draft.ResumeFileName ?? "resume", years, ct);

        if (result.Status == ApiStatus.BadRequest)
        {
            turn.Replies.Add(BotTexts.UnreadableFile);
            return;
        }
        if (!result.IsOk || result.Value is null)
            throw new ServiceUnavailableException();

        draft.YearsOfExperience = years;
        draft.PhoneFound = result.Value.PhoneFound;
        await ApplyMatchesAsync(turn, draft, result.Value, ct);
    }

    // Шаг «выбор вакансии»: номер, «ещё», «все» или «0».
    private async Task HandleChoiceAsync(Turn turn, string text, CancellationToken ct)
    {
        var draft = turn.Session.Draft;
        var answer = Normalize(text);

        if (answer == "ещё")
        {
            if ((draft.Page + 1) * _pageSize >= draft.VacancyIds.Count)
            {
                turn.Replies.Add(BotTexts.NoMoreVacancies);
                return;
            }

            var target = draft.Page + 1;
            draft.Page = target;
            await PruneUnavailableAsync(turn, ct, rollbackPage: false);
            if (draft.Page * _pageSize >= draft.VacancyIds.Count)
            {
                var lastPage = draft.VacancyIds.Count == 0 ? 0 : (draft.VacancyIds.Count - 1) / _pageSize;
                draft.Page = Math.Max(0, Math.Min(target - 1, lastPage));
                turn.Replies.Add(draft.VacancyIds.Count == 0 ? BotTexts.NoMatches(null) : BotTexts.NoMoreVacancies);
                return;
            }

            turn.Replies.Add(await BuildPageAsync(turn, header: null, ct));
            return;
        }

        if (answer == "все")
        {
            await ShowAllOpenAsync(turn, ct);
            return;
        }

        if (int.TryParse(answer, out var number))
        {
            var linked = turn.Session.LinkedVacancyId;
            if (number == 0 && linked is not null && !draft.VacancyIds.Contains(linked.Value))
            {
                await ApplyAsync(turn, linked.Value, null, ct);
                return;
            }

            var shown = Math.Min(draft.VacancyIds.Count, (draft.Page + 1) * _pageSize);
            if (number >= 1 && number <= shown)
            {
                await ApplyAsync(turn, draft.VacancyIds[number - 1], null, ct);
                return;
            }
        }

        turn.Replies.Add(StepHint(turn.Session));
    }

    // «все»: заменяет список всеми открытыми вакансиями.
    private async Task ShowAllOpenAsync(Turn turn, CancellationToken ct)
    {
        var result = await api.GetOpenVacanciesAsync(ct);
        if (!result.IsOk || result.Value is null)
            throw new ServiceUnavailableException();

        var draft = turn.Session.Draft;
        foreach (var vacancy in result.Value)
            turn.Vacancies[vacancy.Id] = vacancy;

        draft.VacancyIds = result.Value.Where(v => v.Status == "open").Select(v => v.Id).Take(MaxListedVacancies).ToList();
        draft.Page = 0;
        turn.Session.Step = BotSteps.ChoosingVacancy;

        if (draft.VacancyIds.Count == 0)
        {
            turn.Replies.Add(BotTexts.NoOpenVacancies);
            return;
        }

        turn.Replies.Add(await BuildPageAsync(turn, "Открытые вакансии:", ct));
    }

    // Шаг «подтверждение вакансии по ссылке»: «да» — отклик, «нет» — обычный список.
    private async Task HandleConfirmationAsync(Turn turn, string text, CancellationToken ct)
    {
        var answer = Normalize(text);
        var linked = turn.Session.LinkedVacancyId;

        if (linked is null)
        {
            turn.Session.Step = BotSteps.ChoosingVacancy;
            await RenderChoiceAsync(turn, ct);
            return;
        }

        if (answer == "да")
        {
            await ApplyAsync(turn, linked.Value, null, ct);
        }
        else if (answer == "нет")
        {
            turn.Session.LinkedVacancyId = null;
            turn.Session.Draft.Page = 0;
            await RenderChoiceAsync(turn, ct);
        }
        else
        {
            turn.Replies.Add(BotTexts.ConfirmHint);
        }
    }

    // Шаг «телефон»: введённый номер уходит в API вместе с откликом.
    private async Task HandlePhoneAsync(Turn turn, string text, CancellationToken ct)
    {
        var pending = turn.Session.Draft.PendingVacancyId;
        if (pending is null)
        {
            turn.Session.Step = BotSteps.ChoosingVacancy;
            await RenderChoiceAsync(turn, ct);
            return;
        }

        await ApplyAsync(turn, pending.Value, text, ct);
    }

    // Показывает подбор: подтверждение вакансии по ссылке или страницу списка.
    private async Task RenderChoiceAsync(Turn turn, CancellationToken ct)
    {
        var session = turn.Session;
        var draft = session.Draft;
        VacancyDto? linked = null;

        if (session.LinkedVacancyId is { } linkedId)
        {
            linked = await GetOpenVacancyAsync(turn, linkedId, ct);
            if (linked is null)
                session.LinkedVacancyId = null;
        }

        await PruneUnavailableAsync(turn, ct);

        if (linked is not null && draft.VacancyIds.Contains(linked.Id))
        {
            session.Step = BotSteps.ConfirmingLinked;
            turn.Replies.Add(BotTexts.ConfirmLinked(linked.Title));
            return;
        }

        session.Step = BotSteps.ChoosingVacancy;
        turn.Replies.Add(draft.VacancyIds.Count == 0
            ? BotTexts.NoMatches(linked?.Title)
            : await BuildPageAsync(turn, "Вам подходят вакансии:", ct));
    }

    // Убирает из списка недоступные (не открытые или удалённые) вакансии текущей страницы, пока страница не заполнится открытыми.
    private async Task PruneUnavailableAsync(Turn turn, CancellationToken ct, bool rollbackPage = true)
    {
        var draft = turn.Session.Draft;

        while (true)
        {
            var pageIds = draft.VacancyIds.Skip(draft.Page * _pageSize).Take(_pageSize).ToList();
            await LoadVacanciesAsync(turn, pageIds, ct);

            var unavailable = pageIds.Where(id => !turn.Vacancies.TryGetValue(id, out var v) || v.Status != "open").ToList();
            if (unavailable.Count == 0)
                return;

            draft.VacancyIds.RemoveAll(unavailable.Contains);
            if (rollbackPage && draft.Page > 0 && draft.Page * _pageSize >= draft.VacancyIds.Count)
                draft.Page--;
        }
    }

    // Собирает текст текущей страницы списка (все вакансии страницы уже открыты); нумерация сквозная.
    private async Task<string> BuildPageAsync(Turn turn, string? header, CancellationToken ct)
    {
        await PruneUnavailableAsync(turn, ct);

        var draft = turn.Session.Draft;
        var start = draft.Page * _pageSize;
        var lines = new List<string>();

        for (var i = 0; i < _pageSize && start + i < draft.VacancyIds.Count; i++)
        {
            var vacancy = turn.Vacancies[draft.VacancyIds[start + i]];
            lines.Add(BotTexts.VacancyLine(start + i + 1, vacancy.Title, vacancy.Direction));
        }

        string? linkedTitle = null;
        if (turn.Session.LinkedVacancyId is { } linkedId && !draft.VacancyIds.Contains(linkedId))
        {
            linkedTitle = (await GetOpenVacancyAsync(turn, linkedId, ct))?.Title;
            if (linkedTitle is null)
                turn.Session.LinkedVacancyId = null;
        }

        var hasMore = start + _pageSize < draft.VacancyIds.Count;
        return BotTexts.VacancyPage(header, lines, hasMore, linkedTitle);
    }

    // Откликается на вакансию: проверяет кандидата, телефон, качает резюме и вызывает API.
    private async Task ApplyAsync(Turn turn, Guid vacancyId, string? phoneInput, CancellationToken ct)
    {
        var session = turn.Session;
        var draft = session.Draft;

        if (draft.ResumeFileId is null)
        {
            ResetToAwaitingResume(turn);
            return;
        }

        var vacancy = await GetOpenVacancyAsync(turn, vacancyId, ct);
        if (vacancy is null)
        {
            await HandleClosedAsync(turn, vacancyId, null, ct);
            return;
        }

        if (draft.YearsOfExperience is not { } years)
        {
            session.Step = BotSteps.AwaitingExperience;
            turn.Replies.Add(BotTexts.AskExperience);
            return;
        }

        var candidate = await api.GetCandidateAsync(turn.Message.UserId, ct);
        if (candidate.Status is not (ApiStatus.Ok or ApiStatus.NotFound))
            throw new ServiceUnavailableException();

        var candidateExists = candidate.IsOk;
        if (!candidateExists && phoneInput is null && !draft.PhoneFound)
        {
            AskForPhone(turn, vacancyId);
            return;
        }

        var file = await DownloadAsync(draft.ResumeFileId, ct);
        var result = await api.CreateApplicationAsync(new CreateApplicationCall(
            turn.Message.UserId,
            vacancyId,
            years,
            file,
            draft.ResumeFileName ?? "resume",
            FullName(turn.Message),
            phoneInput), ct);

        switch (result.Status)
        {
            case ApiStatus.Ok:
                draft.PendingVacancyId = null;
                if (session.LinkedVacancyId == vacancyId)
                    session.LinkedVacancyId = null;
                session.Step = BotSteps.ChoosingVacancy;
                turn.Replies.Add(BotTexts.Applied(vacancy.Title));
                break;
            case ApiStatus.Conflict:
                await HandleConflictAsync(turn, vacancy, ct);
                break;
            case ApiStatus.NotFound:
                await HandleClosedAsync(turn, vacancyId, vacancy.Title, ct);
                break;
            case ApiStatus.BadRequest when phoneInput is not null:
                turn.Replies.Add(BotTexts.InvalidPhone);
                break;
            case ApiStatus.BadRequest when !candidateExists:
                AskForPhone(turn, vacancyId);
                break;
            case ApiStatus.BadRequest:
                turn.Replies.Add(BotTexts.UnreadableFile);
                break;
            default:
                throw new ServiceUnavailableException();
        }
    }

    // Просит телефон текстом и запоминает вакансию, на которую откликаемся.
    private static void AskForPhone(Turn turn, Guid vacancyId)
    {
        turn.Session.Draft.PendingVacancyId = vacancyId;
        turn.Session.Step = BotSteps.AwaitingPhone;
        turn.Replies.Add(BotTexts.AskPhone);
    }

    // 409 на отклик: либо уже откликались (показываем статус), либо набор закрыт.
    private async Task HandleConflictAsync(Turn turn, VacancyDto vacancy, CancellationToken ct)
    {
        var applications = await api.GetCandidateApplicationsAsync(turn.Message.UserId, ct);
        if (!applications.IsOk || applications.Value is null)
            throw new ServiceUnavailableException();

        var existing = applications.Value.FirstOrDefault(a => a.VacancyId == vacancy.Id);
        if (existing is not null)
        {
            turn.Session.Draft.PendingVacancyId = null;
            turn.Session.Step = BotSteps.ChoosingVacancy;
            turn.Replies.Add(BotTexts.AlreadyApplied(vacancy.Title, existing.Status));
            return;
        }

        await HandleClosedAsync(turn, vacancy.Id, vacancy.Title, ct);
    }

    // Набор закрыт: убираем вакансию из списка и показываем остальные подходящие.
    private async Task HandleClosedAsync(Turn turn, Guid vacancyId, string? title, CancellationToken ct)
    {
        var session = turn.Session;
        session.Draft.VacancyIds.Remove(vacancyId);
        session.Draft.PendingVacancyId = null;
        session.Draft.Page = 0;
        if (session.LinkedVacancyId == vacancyId)
            session.LinkedVacancyId = null;

        turn.Replies.Add(title is null ? BotTexts.ClosedVacancy : BotTexts.Closed(title));
        await RenderChoiceAsync(turn, ct);
    }

    // Возвращает диалог к ожиданию резюме, если в черновике нет файла.
    private static void ResetToAwaitingResume(Turn turn)
    {
        turn.Session.Step = BotSteps.AwaitingResume;
        turn.Session.Draft = new BotDraft();
        turn.Replies.Add(BotTexts.SendResumeHint);
    }

    // Подсказка по текущему шагу.
    private string StepHint(BotSessionState session)
    {
        var draft = session.Draft;
        return session.Step switch
        {
            BotSteps.AwaitingExperience => BotTexts.ExperienceHint,
            BotSteps.ConfirmingLinked => BotTexts.ConfirmHint,
            BotSteps.AwaitingPhone => BotTexts.PhoneHint,
            BotSteps.ChoosingVacancy => BotTexts.ChooseHint(
                hasMore: (draft.Page + 1) * _pageSize < draft.VacancyIds.Count,
                hasLinkedOption: session.LinkedVacancyId is { } id && !draft.VacancyIds.Contains(id),
                hasList: draft.VacancyIds.Count > 0),
            _ => BotTexts.SendResumeHint,
        };
    }

    // Возвращает вакансию из кэша или API; null — вакансии нет.
    private async Task<VacancyDto?> GetVacancyAsync(Turn turn, Guid id, CancellationToken ct)
    {
        await LoadVacanciesAsync(turn, [id], ct);
        return turn.Vacancies.GetValueOrDefault(id);
    }

    // Возвращает вакансию, только если она открыта; закрытая, архивная или отсутствующая — null.
    private async Task<VacancyDto?> GetOpenVacancyAsync(Turn turn, Guid id, CancellationToken ct)
    {
        var vacancy = await GetVacancyAsync(turn, id, ct);
        return vacancy is { Status: "open" } ? vacancy : null;
    }

    // Догружает недостающие вакансии из API параллельно; 404 запоминается, другой сбой — «попробуйте позже».
    private async Task LoadVacanciesAsync(Turn turn, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var missing = ids.Where(id => !turn.Vacancies.ContainsKey(id) && !turn.NotFound.Contains(id)).Distinct().ToList();
        if (missing.Count == 0)
            return;

        var results = await Task.WhenAll(missing.Select(id => api.GetVacancyAsync(id, ct)));
        for (var i = 0; i < missing.Count; i++)
        {
            switch (results[i].Status)
            {
                case ApiStatus.Ok when results[i].Value is not null:
                    turn.Vacancies[missing[i]] = results[i].Value!;
                    break;
                case ApiStatus.NotFound:
                    turn.NotFound.Add(missing[i]);
                    break;
                default:
                    throw new ServiceUnavailableException();
            }
        }
    }

    // Скачивает файл из Telegram; любой сбой — «попробуйте позже».
    private async Task<byte[]> DownloadAsync(string fileId, CancellationToken ct)
    {
        try
        {
            return await chat.DownloadFileAsync(fileId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger?.LogWarning("File download failed: {ErrorType}", ex.GetType().Name);
            throw new ServiceUnavailableException();
        }
    }

    // Имя кандидата из профиля Telegram.
    private static string FullName(IncomingMessage message)
    {
        var name = $"{message.FirstName} {message.LastName}".Trim();
        return name.Length == 0 ? "Кандидат" : name;
    }

    // Приводит ответ кандидата к виду для сравнения: без регистра и «ё», без точки и восклицательного знака в конце.
    private static string Normalize(string text) =>
        text.Trim().TrimEnd('.', '!').Trim().ToLowerInvariant().Replace("еще", "ещё").Replace("всё", "все");
}
