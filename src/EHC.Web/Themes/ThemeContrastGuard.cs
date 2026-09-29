using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace EHC.Web.Themes;

/// <summary>
/// Checks a Theme node on save with the same <see cref="Palette"/> the site renders with: a fine-tuned shade that makes
/// text unreadable (WCAG AA) cancels the save; base colours that had to be nudged are reported as a warning.
/// </summary>
public sealed class ThemeContrastGuard : INotificationHandler<ContentSavingNotification>
{
    public void Handle(ContentSavingNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (content.ContentType.Alias != "theme") continue;

            string? V(string alias) => content.GetValue<string>(alias);
            var result = Palette.Build(
                new ThemeColors(V("primaryColor"), V("darkColor"), V("accentColor"), V("highlightColor"), V("supportColor")),
                Palette.FineTune.ToDictionary(f => f.Alias, f => V(f.Alias)));

            if (result.Problems.Count > 0)
            {
                notification.CancelOperation(new EventMessage(
                    "Theme colours fail the contrast check",
                    string.Join(" ", result.Problems) + " Choose a darker or lighter shade, or clear the fine-tuned value to use the generated one.",
                    EventMessageType.Error));
                return;
            }
            if (result.Adjustments.Count > 0)
                notification.Messages.Add(new EventMessage("Theme colours adjusted for readability", string.Join(" ", result.Adjustments), EventMessageType.Warning));
        }
    }
}
