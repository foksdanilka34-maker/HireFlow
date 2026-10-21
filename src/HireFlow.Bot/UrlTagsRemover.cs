using System.Diagnostics;
using OpenTelemetry;

namespace HireFlow.Bot;

// Удаляет из спанов адрес запроса: в пути бывает Telegram ID, остаётся только шаблон http.route.
public class UrlTagsRemover : BaseProcessor<Activity>
{
    public static readonly string[] RemovedTags = ["url.path", "url.full", "url.query"];

    // Перед экспортом стирает теги url.* у завершённого спана.
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in RemovedTags)
            activity.SetTag(tag, null);
    }
}
