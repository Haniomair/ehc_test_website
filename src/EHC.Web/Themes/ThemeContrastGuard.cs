using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace EHC.Web.Themes;

/// <summary>Cancels saving a Theme node whose colour overrides would make text unreadable (WCAG AA).</summary>
public sealed class ThemeContrastGuard : INotificationHandler<ContentSavingNotification>
{
    private static readonly string[] Aliases = ["brand500", "brand600", "deep900", "accent400"];

    public void Handle(ContentSavingNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (content.ContentType.Alias != "theme") continue;

            var overrides = Aliases.ToDictionary(a => a, a => content.GetValue<string>(a));
            var problems = Contrast.Check(overrides);
            if (problems.Count == 0) continue;

            notification.CancelOperation(new EventMessage(
                "Theme colours fail the contrast check",
                string.Join(" ", problems) + " Choose a darker or lighter shade, or clear the override to use the preset.",
                EventMessageType.Error));
            return;
        }
    }
}
