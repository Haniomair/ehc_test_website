using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using EHC.Web.Site;
using static EHC.Web.Composers.BlockField;

namespace EHC.Web.Composers;

/// <summary>
/// Once: gives every care navigator on the site a starter symptom guide — five care levels with their advice and a list of
/// common symptoms mapped to a level, modelled on public triage guides ("where to go for care"). This is clinical content:
/// the list, the levels and the advice need review and approval by EHC's clinical team before launch (said in the
/// editor descriptions too). Navigators that already have symptoms are left alone.
/// </summary>
public sealed class NavigatorSymptomsSeeder(IContentService contents, IContentTypeService contentTypes, IKeyValueService keyValues, IRuntimeState runtime, ILogger<NavigatorSymptomsSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.Navigator.SymptomsSeeded";
    private const string SehhatyAr = "https://www.moh.gov.sa/eservices/sehhaty/pages/default.aspx";
    private const string SehhatyEn = "https://www.moh.gov.sa/en/eservices/sehhaty/pages/default.aspx";

    private sealed record Symptom(string Ar, string En, string WordsAr, string WordsEn, string Level, string NoteAr = "", string NoteEn = "");

    private static IEnumerable<Symptom> Starter() =>
    [
        // emergency: call 997 or go to an emergency department now
        new("ألم في الصدر", "Chest pain", "ضيق في الصدر، ألم القلب، نوبة قلبية، ذبحة", "tight chest, heart pain, heart attack, angina", "emergency"),
        new("صعوبة شديدة في التنفس", "Severe difficulty breathing", "ضيق تنفس، اختناق، لا أستطيع التنفس", "shortness of breath, can't breathe, choking", "emergency"),
        new("علامات الجلطة الدماغية", "Signs of a stroke", "ارتخاء الوجه، ضعف الذراع، صعوبة الكلام، سكتة دماغية", "face drooping, arm weakness, slurred speech, stroke", "emergency"),
        new("نزيف شديد", "Severe bleeding", "نزيف لا يتوقف، جرح عميق", "bleeding won't stop, deep wound", "emergency"),
        new("إغماء أو تشنجات", "Fainting or seizure", "فقدان الوعي، نوبة صرع، تشنج", "passed out, unconscious, fit, convulsions, epilepsy", "emergency"),
        new("إصابة شديدة أو حادث", "Serious injury or accident", "كسر واضح، حادث سيارة، سقوط من ارتفاع، إصابة في الرأس", "broken bone, car accident, fall from height, head injury", "emergency"),
        new("رد فعل تحسسي شديد", "Severe allergic reaction", "تورم الوجه أو الحلق، حساسية شديدة", "swollen face or throat, anaphylaxis", "emergency"),
        new("تسمم أو ابتلاع مادة ضارة", "Poisoning or swallowing something harmful", "جرعة زائدة، ابتلاع دواء، مواد كيميائية", "overdose, swallowed medicine, chemicals", "emergency"),
        new("حرارة عند رضيع عمره أقل من 3 أشهر", "Fever in a baby under 3 months", "حمى رضيع، مولود حرارة", "newborn fever, infant temperature", "emergency"),
        new("أفكار بإيذاء النفس", "Thoughts of harming yourself", "انتحار، إيذاء النفس", "suicide, self-harm, hurting myself", "emergency",
            "لست وحدك، اطلب المساعدة الآن.", "You are not alone. Get help now."),
        // urgent: needs care today, usually not the emergency department
        new("التواء أو شد عضلي", "Sprain or strain", "التواء الكاحل، شد عضلي، كدمة", "twisted ankle, pulled muscle, bruise", "urgent"),
        new("جرح قد يحتاج إلى غرز", "Cut that may need stitches", "جرح، خياطة", "cut, laceration, stitches", "urgent"),
        new("حرق بسيط", "Minor burn", "حرق، ماء ساخن", "burn, scald", "urgent"),
        new("قيء أو إسهال لا يتوقف", "Vomiting or diarrhoea that won't stop", "استفراغ، جفاف، نزلة معوية", "being sick, dehydration, stomach bug", "urgent"),
        new("ألم في العين أو احمرارها", "Eye pain or redness", "عين حمراء، التهاب العين، جسم غريب في العين", "red eye, eye infection, something in my eye", "urgent"),
        // primary care: book with your family doctor
        new("حمى", "Fever", "حرارة، سخونة", "temperature, high temperature", "primary",
            "إذا كانت الحرارة مرتفعة جدًا أو استمرت عدة أيام فاطلب رعاية عاجلة.", "If it is very high or lasts several days, get urgent care."),
        new("ألم في الأذن", "Earache", "التهاب الأذن، وجع الأذن", "ear infection, ear pain", "primary"),
        new("التهاب الحلق", "Sore throat", "ألم الحلق، اللوزتين", "throat pain, tonsils", "primary"),
        new("طفح جلدي", "Rash", "حكة، حبوب، حساسية جلدية", "itching, spots, skin allergy", "primary"),
        new("ألم الظهر", "Back pain", "أسفل الظهر، الديسك", "lower back, slipped disc", "primary"),
        new("حرقة أو ألم عند التبول", "Burning or pain when passing urine", "التهاب المسالك البولية", "urine infection, UTI", "primary"),
        new("صداع متكرر", "Recurring headaches", "صداع، شقيقة", "headache, migraine", "primary"),
        new("متابعة السكري أو ضغط الدم", "Diabetes or blood pressure check-up", "سكر، ضغط، أمراض مزمنة", "sugar, hypertension, chronic disease", "primary"),
        new("القلق أو الحزن المستمر", "Anxiety or low mood", "اكتئاب، توتر، أرق", "depression, stress, can't sleep", "primary"),
        // virtual: a doctor can help without a visit
        new("تجديد وصفة طبية", "Renewing a prescription", "صرف دواء، تجديد الدواء", "repeat prescription, refill", "virtual"),
        new("سؤال عن دواء", "A question about a medicine", "جرعة الدواء، آثار جانبية", "dose, side effects", "virtual"),
        // self-care: usually gets better at home
        new("زكام أو رشح", "Cold or runny nose", "انفلونزا خفيفة، احتقان، عطاس", "mild flu, blocked nose, sneezing", "self"),
        new("سعال خفيف", "Mild cough", "كحة", "cough", "self"),
        new("حروق الشمس", "Sunburn", "ضربة شمس خفيفة، حرق الشمس", "sun burn", "self"),
    ];

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Seeding the care navigator symptom guide failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run || keyValues.GetValue(DoneKey) is not null) return;
        var navigator = contentTypes.Get("careNavigatorBlock");
        if (navigator is null || contentTypes.Get("navigatorSymptom") is null || contentTypes.Get("navigatorLevel") is null) return;   // schema not imported yet: next start

        Guid TypeKey(string alias) => contentTypes.Get(alias)?.Key ?? throw new InvalidOperationException($"Element type {alias} is missing");
        var symptoms = Symptoms(TypeKey);
        var levels = Levels(TypeKey);

        var pageTypes = new[] { "home", "landingPage", "contentPage" }.Select(a => contentTypes.Get(a)?.Id).OfType<int>().ToArray();
        var navKey = navigator.Key.ToString();
        var seeded = 0;
        foreach (var page in contents.GetPagedOfTypes(pageTypes, 0, 2000, out _, null, null).Where(p => !p.Trashed))
        {
            if (page.Properties["blocks"] is not { } property) continue;
            var hadDraft = page.Edited;
            var changed = 0;
            foreach (var pv in property.Values.ToList())
            {
                if (pv.EditedValue is not string raw || !raw.Contains(navKey, StringComparison.OrdinalIgnoreCase)) continue;
                var root = JsonNode.Parse(raw);
                foreach (var block in root?["contentData"]?.AsArray() ?? [])
                {
                    if (!string.Equals((string?)block?["contentTypeKey"], navKey, StringComparison.OrdinalIgnoreCase)) continue;
                    var values = block!["values"]!.AsArray();
                    if (values.Any(v => (string?)v?["alias"] == "symptoms")) continue;
                    values.Add(new JsonObject { ["alias"] = "symptoms", ["culture"] = null, ["segment"] = null, ["value"] = symptoms });
                    values.Add(new JsonObject { ["alias"] = "careLevels", ["culture"] = null, ["segment"] = null, ["value"] = levels });
                    changed++;
                }
                if (changed > 0) page.SetValue("blocks", root!.ToJsonString(), pv.Culture, pv.Segment);
            }
            if (changed == 0) continue;
            ContentChanges.SaveKeepingDrafts(contents, page, hadDraft, logger, "Care navigator symptom guide added");
            seeded += changed;
        }
        keyValues.SetValue(DoneKey, "1");
        logger.LogInformation("Care navigator: starter symptom guide added to {Count} navigators (needs clinical review)", seeded);
    }

    internal static string Symptoms(Func<string, Guid> typeKey)
    {
        var list = new BlockJson(typeKey).Items();
        foreach (var s in Starter())
        {
            var fields = new List<BlockField> { Text("label", s.Ar, s.En), Text("keywords", s.WordsAr, s.WordsEn), Pick("level", s.Level) };
            if (s.NoteAr.Length > 0) fields.Add(Text("note", s.NoteAr, s.NoteEn));
            list.Add("navigatorSymptom", [.. fields]);
        }
        return list.Build();
    }

    internal string Levels(Func<string, Guid> typeKey)
    {
        var library = contentTypes.Get("healthLibrary") is { } lib ? contents.GetPagedOfTypes([lib.Id], 0, 1, out _, null, null).FirstOrDefault()?.Key : null;
        var self = library is { } key
            ? Link("ctaLink", new SeedLink("المكتبة الصحية", Page: key), new SeedLink("Health library", Page: key))
            : new BlockField([]);
        return new BlockJson(typeKey).Items()
            .Add("navigatorLevel", Pick("level", "emergency"),
                Text("title", "اتصل بـ 997 أو توجّه إلى أقرب طوارئ الآن", "Call 997 or go to the nearest emergency department now"),
                Text("text", "لا تنتظر: قد تكون هذه علامة على حالة خطيرة.", "Don't wait: this can be a sign of something serious."),
                Text("ctaLabel", "اتصل بـ 997", "Call 997"),
                Link("ctaLink", new SeedLink("997", "tel:997"), new SeedLink("997", "tel:997")),
                Pick("nearest", "emergency"))
            .Add("navigatorLevel", Pick("level", "urgent"),
                Text("title", "احصل على رعاية عاجلة اليوم", "Get urgent care today"),
                Text("text", "يحتاج إلى تقييم اليوم، لكنه غالبًا لا يستدعي الطوارئ. توجّه إلى أقرب مركز صحي، أو اتصل على 937 للاستشارة.", "It needs to be seen today, but usually not in the emergency department. Go to your nearest health centre, or call 937 for advice."),
                Text("ctaLabel", "اتصل على 937", "Call 937"),
                Link("ctaLink", new SeedLink("937", "tel:937"), new SeedLink("937", "tel:937")),
                Pick("nearest", "primaryCare"))
            .Add("navigatorLevel", Pick("level", "primary"),
                Text("title", "احجز موعدًا مع طبيب الأسرة", "Book with your family doctor"),
                Text("text", "مركزك الصحي هو المكان المناسب لهذه الحالة.", "Your health centre is the right place for this."),
                Text("ctaLabel", "احجز عبر صحتي", "Book via Sehhaty"),
                Link("ctaLink", new SeedLink("صحتي", SehhatyAr, NewWindow: true), new SeedLink("Sehhaty", SehhatyEn, NewWindow: true)),
                Pick("nearest", "primaryCare"))
            .Add("navigatorLevel", Pick("level", "virtual"),
                Text("title", "استشارة عن بُعد", "Get advice without a visit"),
                Text("text", "يمكن لطبيب مساعدتك دون زيارة: استشارة عبر تطبيق صحتي أو اتصل على 937.", "A doctor can help without a visit: a consultation in the Sehhaty app, or call 937."),
                Text("ctaLabel", "افتح صحتي", "Open Sehhaty"),
                Link("ctaLink", new SeedLink("صحتي", SehhatyAr, NewWindow: true), new SeedLink("Sehhaty", SehhatyEn, NewWindow: true)),
                Pick("nearest", "none"))
            .Add("navigatorLevel", Pick("level", "self"),
                Text("title", "العناية الذاتية في المنزل", "Self-care at home"),
                Text("text", "غالبًا تتحسن هذه الحالة بالراحة والسوائل. راجع طبيب الأسرة إذا ساءت أو استمرت أكثر من بضعة أيام.", "This usually gets better with rest and fluids. See your family doctor if it gets worse or lasts more than a few days."),
                Text("ctaLabel", "اقرأ في المكتبة الصحية", "Read in the health library"),
                self,
                Pick("nearest", "none"))
            .Build();
    }
}
