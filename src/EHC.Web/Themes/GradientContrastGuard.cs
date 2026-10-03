using EHC.Web.Blocks;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace EHC.Web.Themes;

/// <summary>
/// Checks a Gradient node on save: white text must stay readable on every colour stop. Below 3:1 (fails even for
/// large headings) the save is cancelled; below 4.5:1 (fails for normal text) a warning is shown.
/// The gradient editor shows the same figures while editing.
/// </summary>
public sealed class GradientContrastGuard : INotificationHandler<ContentSavingNotification>
{
    public const double Minimum = 3.0;
    public const double Normal = 4.5;

    public void Handle(ContentSavingNotification notification)
    {
        foreach (var content in notification.SavedEntities)
        {
            if (content.ContentType.Alias != "gradient") continue;

            var design = Gradients.Parse(content.GetValue("design")?.ToString());
            if (design is null)
            {
                notification.CancelOperation(new EventMessage("Gradient is incomplete", "Add at least two colours.", EventMessageType.Error));
                return;
            }

            var ratios = Gradients.Readability(design);
            var failing = ratios.Where(r => r.Ratio < Minimum).ToList();
            if (failing.Count > 0)
            {
                notification.CancelOperation(new EventMessage(
                    "Gradient is too light for white text",
                    string.Join(" ", failing.Select(r => $"Colour {r.Stop}: {r.Ratio}:1.")) + $" At least {Minimum}:1 is needed. Choose a darker colour.",
                    EventMessageType.Error));
                return;
            }
            var weak = ratios.Where(r => r.Ratio < Normal).ToList();
            if (weak.Count > 0)
            {
                notification.Messages.Add(new EventMessage(
                    "Gradient: small text may be hard to read",
                    string.Join(" ", weak.Select(r => $"Colour {r.Stop}: {r.Ratio}:1.")) + $" Normal-size text needs {Normal}:1; headings are fine. Keep the text on the darker side.",
                    EventMessageType.Warning));
            }
        }
    }
}
