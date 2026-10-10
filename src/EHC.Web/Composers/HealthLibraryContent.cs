using System.Net;
using System.Text;

namespace EHC.Web.Composers;

/// <summary>
/// Sample health library content for the showcase (HealthLibrarySeeder): ten conditions and six tests in Arabic and
/// English, written in plain language from widely published guidance (WHO, NHS, MedlinePlus, Ministry of Health). Every
/// page shows "Awaiting clinical review" until a clinician reviews it and sets a review date; the reviewer stays a
/// placeholder. Section text: one paragraph per line, lines starting with "- " form a list.
/// </summary>
internal static class HealthLibraryContent
{
    internal sealed record T(string Ar, string En);

    internal sealed record Source(T Name, string UrlEn, string? UrlAr = null);

    internal sealed record Item(
        string Slug, bool IsTest, T Name, T Aka, T Intro, string[] Systems, IReadOnlyDictionary<string, T> Sections,
        string[]? Audiences = null, bool Featured = false, T? Emergency = null, T? Urgent = null, T? Primary = null,
        string[]? Specialties = null, string[]? Facilities = null, string[]? Tools = null, string[]? Related = null, Source[]? Sources = null);

    internal static readonly T Reviewer = new("اسم الطبيب", "Doctor Name");

    /// <summary>Paragraphs and "- " lists to HTML (text encoded).</summary>
    internal static string Html(string text)
    {
        var b = new StringBuilder();
        var inList = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!inList) { b.Append("<ul>"); inList = true; }
                b.Append("<li>").Append(WebUtility.HtmlEncode(line[2..].Trim())).Append("</li>");
                continue;
            }
            if (inList) { b.Append("</ul>"); inList = false; }
            b.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }
        if (inList) b.Append("</ul>");
        return b.ToString();
    }

    private static Source Who(string ar, string en, string url) => new(new("منظمة الصحة العالمية: " + ar, "World Health Organization: " + en), url);
    private static Source Nhs(string ar, string en, string url) => new(new("هيئة الخدمات الصحية البريطانية (NHS): " + ar, "NHS: " + en), url);
    private static Source Medline(string ar, string en, string url) => new(new("ميدلاين بلس (المكتبة الوطنية الأمريكية للطب): " + ar, "MedlinePlus: " + en), url);

    internal static readonly (string Slug, T Name, T Intro)[] Sections =
    [
        ("conditions", new("الحالات الصحية", "Conditions"), new("الأمراض والحالات الصحية من الألف إلى الياء: الأعراض والأسباب والعلاج ومتى تطلب المساعدة.", "Conditions A–Z: symptoms, causes, treatment and when to get help.")),
        ("tests", new("الفحوصات والإجراءات", "Tests and procedures"), new("ما يحدث في الفحوصات والإجراءات الطبية، وكيف تستعد لها، وماذا تعني النتائج.", "What happens during medical tests and procedures, how to prepare and what results mean.")),
        ("living", new("حياة صحية", "Healthy living"), new("نصائح للتغذية والنشاط والصحة النفسية والوقاية في كل مراحل العمر.", "Advice on eating well, staying active, mental wellbeing and prevention at every age.")),
    ];

    internal static readonly T PolicyName = new("سياسة المحتوى الصحي", "Editorial policy");
    internal static readonly T PolicyIntro = new("كيف نكتب معلوماتنا الصحية ونراجعها ونحدّثها.", "How we write, review and update our health information.");
    internal static readonly T PolicyBody = new(
        """
        <p><strong>هذه سياسة نموذجية أُعدّت لهذا الموقع التجريبي.</strong></p>
        <h2>لغة واضحة</h2>
        <p>نكتب للمرضى وعائلاتهم بلغة بسيطة، ونشرح المصطلحات الطبية عند الحاجة إليها، ونقدّم المحتوى بالعربية والإنجليزية.</p>
        <h2>مصادر موثوقة</h2>
        <p>نعتمد على الإرشادات الصادرة عن وزارة الصحة ومنظمة الصحة العالمية والجهات الصحية المعروفة، ونذكر مصادرنا في كل صفحة.</p>
        <h2>مراجعة طبية</h2>
        <ul><li>يراجع كل صفحة طبيب أو مختص قبل نشرها.</li><li>نعيد مراجعة الصفحات كل سنتين على الأقل، أو قبل ذلك إذا تغيرت الإرشادات.</li><li>تعرض كل صفحة تاريخ آخر مراجعة وموعد المراجعة القادمة.</li></ul>
        <h2>ليست بديلًا عن الاستشارة الطبية</h2>
        <p>هذه المعلومات للتثقيف العام ولا تغني عن زيارة الطبيب. في حالات الطوارئ اتصل على 997.</p>
        <h2>ملاحظاتك</h2>
        <p>نرحّب بملاحظاتك عبر خيار «هل كانت هذه الصفحة مفيدة؟» في أسفل كل صفحة.</p>
        """,
        """
        <p><strong>This is a sample policy prepared for this test website.</strong></p>
        <h2>Plain language</h2>
        <p>We write for patients and their families in plain language, explain medical terms when we need them, and publish in Arabic and English.</p>
        <h2>Trusted sources</h2>
        <p>We follow guidance from the Ministry of Health, the World Health Organization and other recognised health bodies, and list our sources on every page.</p>
        <h2>Clinical review</h2>
        <ul><li>A doctor or specialist reviews every page before it is published.</li><li>We review pages at least every two years, or sooner when guidance changes.</li><li>Every page shows when it was last reviewed and when the next review is due.</li></ul>
        <h2>Not a substitute for medical advice</h2>
        <p>This information is for general education and does not replace seeing a doctor. In an emergency, call 997.</p>
        <h2>Your feedback</h2>
        <p>Tell us what you think with "Was this page helpful?" at the bottom of every page.</p>
        """);

    private static IReadOnlyDictionary<string, T> S(params (string Key, string Ar, string En)[] s) =>
        s.ToDictionary(x => x.Key, x => new T(x.Ar, x.En));

    internal static readonly Item[] Items =
    [
        new("type-2-diabetes", false,
            new("السكري من النوع الثاني", "Type 2 diabetes"),
            new("السكري، السكر، ارتفاع سكر الدم", "diabetes, high blood sugar, diabetes mellitus"),
            new("حالة طويلة الأمد يرتفع فيها سكر الدم لأن الجسم لا يستخدم الإنسولين جيدًا. يمكن التحكم فيها بنمط الحياة والعلاج.",
                "A long-term condition where blood sugar is too high because the body doesn't use insulin well. It can be managed with lifestyle changes and treatment."),
            ["hormones"],
            S(
                ("overview",
                    "الإنسولين هرمون ينقل السكر من الدم إلى خلايا الجسم لتستخدمه في إنتاج الطاقة. في السكري من النوع الثاني لا يصنع الجسم ما يكفي من الإنسولين أو لا يستجيب له جيدًا، فيتراكم السكر في الدم. ومع الوقت قد يضر ارتفاع السكر بالعينين والكلى والأعصاب والقلب والأوعية الدموية.\nالسكري شائع، وقد يعيش كثيرون معه سنوات دون أن يعرفوا. اكتشافه مبكرًا والمحافظة على السكر في المعدل الصحي يقللان خطر المضاعفات.",
                    "Insulin is a hormone that moves sugar from the blood into the body's cells, where it is used for energy. In type 2 diabetes the body doesn't make enough insulin or doesn't respond to it well, so sugar builds up in the blood. Over time, high blood sugar can damage the eyes, kidneys, nerves, heart and blood vessels.\nIt is common, and many people have it for years without knowing. Finding it early and keeping blood sugar in a healthy range lowers the risk of complications."),
                ("symptoms",
                    "كثيرون لا يشعرون بأي أعراض في البداية. وعند ظهور الأعراض قد تشمل:\n- العطش الشديد\n- كثرة التبول، خاصة في الليل\n- التعب الشديد\n- نقص الوزن دون سبب\n- تشوش الرؤية\n- بطء التئام الجروح\n- الحكة أو تكرار الالتهابات",
                    "Many people have no symptoms at first. When symptoms appear, they can include:\n- feeling very thirsty\n- passing urine more often, especially at night\n- feeling very tired\n- losing weight without trying\n- blurred vision\n- cuts or wounds that heal slowly\n- itching or infections that keep coming back"),
                ("causes",
                    "يتطور السكري من النوع الثاني عندما تجتمع عدة عوامل، منها:\n- زيادة الوزن، خاصة حول الخصر\n- قلة النشاط البدني\n- إصابة أحد الوالدين أو الإخوة بالسكري\n- التقدم في العمر، مع أنه يظهر أيضًا لدى الأصغر سنًا\n- الإصابة بسكري الحمل سابقًا\n- ارتفاع ضغط الدم أو الكوليسترول",
                    "Type 2 diabetes develops when several risk factors come together, such as:\n- being overweight, especially around the waist\n- not being physically active\n- a parent, brother or sister with diabetes\n- getting older, although it is also seen in younger people\n- having had diabetes in pregnancy\n- high blood pressure or high cholesterol"),
                ("diagnosis",
                    "يُشخَّص السكري بتحليل دم. قد يطلب طبيبك فحص السكر التراكمي (HbA1c) الذي يُظهر متوسط السكر في آخر شهرين إلى ثلاثة أشهر، أو قياس السكر صائمًا. وقد يُعاد الفحص للتأكد.",
                    "A blood test confirms it. Your doctor may use an HbA1c test, which shows your average blood sugar over the past 2 to 3 months, or a fasting blood sugar test. The test is sometimes repeated to be sure."),
                ("treatment",
                    "يهدف العلاج إلى إبقاء السكر وضغط الدم والكوليسترول في المعدلات الصحية.\n- الغذاء الصحي والنشاط المنتظم هما الأساس\n- إنقاص الوزن عند زيادته يحسّن السكر كثيرًا\n- الأدوية عن طريق الفم أو الحقن إذا لم يكفِ تغيير نمط الحياة\n- الإنسولين لبعض المرضى\n- فحوصات دورية للعينين والقدمين والكلى وضغط الدم",
                    "Treatment aims to keep blood sugar, blood pressure and cholesterol in a healthy range.\n- Healthy eating and regular activity are the foundation\n- Losing weight, if you are overweight, can improve blood sugar a lot\n- Medicines, taken by mouth or as injections, if lifestyle changes are not enough\n- Insulin for some people\n- Regular checks of your eyes, feet, kidneys and blood pressure"),
                ("prevention",
                    "يمكنك أن تقلل خطر الإصابة بالسكري من النوع الثاني أو تؤخرها:\n- حافظ على وزن صحي\n- مارس نشاطًا بدنيًا 150 دقيقة في الأسبوع على الأقل، مثل المشي السريع\n- أكثر من الخضار والحبوب الكاملة والبقول، وقلل المشروبات المحلاة والحلويات\n- لا تدخّن\n- افحص السكر إذا كنت معرضًا للخطر",
                    "You can lower your risk of type 2 diabetes, or delay it:\n- keep a healthy weight\n- be active for at least 150 minutes a week, such as brisk walking\n- eat more vegetables, whole grains and pulses, and fewer sugary drinks and sweets\n- don't smoke\n- have your blood sugar checked if you are at risk"),
                ("livingWith",
                    "مع التحكم الجيد يعيش معظم مرضى السكري حياة كاملة ونشطة.\n- افحص قدميك يوميًا بحثًا عن جروح أو تقرحات أو تغير في اللون\n- افحص عينيك مرة كل سنة\n- تعرّف على علامات انخفاض السكر (الرجفة والتعرق والارتباك) إذا كانت أدويتك قد تسببه\n- استشر طبيبك قبل صيام رمضان ليعدّل علاجك",
                    "With good control, most people with diabetes live full, active lives.\n- Check your feet every day for cuts, blisters or colour changes\n- Have your eyes checked every year\n- Learn the signs of low blood sugar (shaking, sweating, confusion) if your medicines can cause it\n- Talk to your doctor before fasting in Ramadan so your treatment can be adjusted")),
            Audiences: ["men", "women", "elderly"],
            Emergency: new("ارتفاع أو انخفاض شديد في السكر مع ارتباك أو نعاس أو إغماء\nتنفس عميق وسريع مع قيء وألم في البطن\nعلامات جلطة دماغية أو نوبة قلبية، مثل ألم الصدر أو ميلان الوجه أو ضعف الذراع",
                "Very high or very low blood sugar with confusion, drowsiness or fainting\nDeep, fast breathing with vomiting and stomach pain\nSigns of a stroke or heart attack, such as chest pain, a drooping face or a weak arm"),
            Urgent: new("قراءات السكر تبقى أعلى بكثير من هدفك\nجرح في القدم محمر أو متورم أو ساخن أو لا يلتئم\nتتقيأ ولا تستطيع الاحتفاظ بالسوائل أو الأدوية",
                "Blood sugar readings stay much higher than your target\nA foot wound that is red, swollen, hot or not healing\nYou are being sick and cannot keep fluids or medicines down"),
            Primary: new("لديك أعراض مثل العطش أو كثرة التبول أو التعب\nأنت معرض للخطر ولم تفحص السكر من قبل\nتحتاج إلى مراجعة رعاية السكري أو أدويتك",
                "You have symptoms such as thirst, passing urine often or tiredness\nYou are at risk and have never had your blood sugar checked\nYou need a review of your diabetes care or medicines"),
            Tools: ["prediabetes", "bmi"], Related: ["hba1c-test"],
            Sources: [Who("السكري", "Diabetes", "https://www.who.int/news-room/fact-sheets/detail/diabetes"), Nhs("السكري من النوع الثاني", "Type 2 diabetes", "https://www.nhs.uk/conditions/type-2-diabetes/")]),

        new("high-blood-pressure", false,
            new("ارتفاع ضغط الدم", "High blood pressure"),
            new("الضغط، الضغط المرتفع، فرط ضغط الدم", "hypertension, blood pressure"),
            new("حالة شائعة لا تسبب أعراضًا غالبًا، لكنها تزيد خطر النوبات القلبية والجلطات. الطريقة الوحيدة لمعرفتها هي قياس الضغط.",
                "A common condition that usually causes no symptoms but raises the risk of heart attacks and strokes. The only way to know is to have it measured."),
            ["heart"],
            S(
                ("overview",
                    "ضغط الدم هو قوة دفع الدم على جدران الشرايين. عندما يبقى مرتفعًا لفترة طويلة يُجهد القلب والأوعية الدموية ويزيد خطر النوبات القلبية والجلطات الدماغية وأمراض الكلى.\nارتفاع الضغط شائع جدًا ولا يسبب أعراضًا في الغالب، لذلك يُسمّى أحيانًا «القاتل الصامت». الطريقة الوحيدة لمعرفته هي قياسه.",
                    "Blood pressure is the force of blood pushing against the walls of your arteries. When it stays too high for a long time, it strains the heart and blood vessels and raises the risk of heart attack, stroke and kidney disease.\nIt is very common and usually causes no symptoms, which is why it is sometimes called a silent condition. The only way to know is to have it measured."),
                ("symptoms",
                    "معظم المصابين لا يشعرون بأي أعراض. وقد يسبب الارتفاع الشديد أحيانًا:\n- الصداع\n- تشوش الرؤية\n- نزيف الأنف\n- ضيق التنفس",
                    "Most people have no symptoms. Very high blood pressure can sometimes cause:\n- headaches\n- blurred vision\n- nosebleeds\n- shortness of breath"),
                ("causes",
                    "يزيد خطر ارتفاع الضغط مع:\n- التقدم في العمر\n- الإكثار من الملح\n- زيادة الوزن\n- قلة النشاط البدني\n- التدخين\n- وجود تاريخ عائلي\n- السكري أو أمراض الكلى",
                    "Your risk is higher with:\n- getting older\n- eating too much salt\n- being overweight\n- not being active\n- smoking\n- a family history of high blood pressure\n- diabetes or kidney disease"),
                ("diagnosis",
                    "يُقاس الضغط بجهاز يُلف حول أعلى الذراع، وتُكتب القراءة برقمين مثل 120/80. قراءة 140/90 أو أعلى في أكثر من مرة تعني غالبًا ارتفاع الضغط. قد يطلب منك طبيبك القياس في المنزل، وقد يطلب تحاليل للدم والبول وتخطيطًا للقلب للاطمئنان على القلب والكلى.",
                    "Blood pressure is measured with a cuff around your upper arm. A reading is written as two numbers, such as 120/80. A reading of 140/90 or higher on more than one occasion usually means high blood pressure. Your doctor may ask you to measure it at home, and may arrange blood and urine tests and an ECG to check your heart and kidneys."),
                ("treatment",
                    "تغيير نمط الحياة يفيد كل المصابين، ويحتاج كثيرون أيضًا إلى أدوية.\n- قلّل الملح وأكثر من الفواكه والخضار\n- مارس النشاط البدني معظم أيام الأسبوع\n- حافظ على وزن صحي\n- توقف عن التدخين\n- تناول أدويتك يوميًا كما وُصفت لك حتى عندما تشعر أنك بخير",
                    "Lifestyle changes help everyone with high blood pressure, and many people also need medicines.\n- Eat less salt and more fruit and vegetables\n- Be active on most days of the week\n- Keep a healthy weight\n- Stop smoking\n- Take your medicines every day as prescribed, even when you feel well"),
                ("prevention",
                    "- قلّل الملح، وانتبه للأطعمة المصنعة والوجبات السريعة\n- مارس النشاط البدني بانتظام\n- حافظ على وزن صحي\n- لا تدخّن\n- افحص ضغطك مرة في السنة على الأقل بعد سن الأربعين، أو قبل ذلك إذا كنت معرضًا للخطر",
                    "- Limit salt, and watch for it in processed and fast food\n- Stay active\n- Keep a healthy weight\n- Don't smoke\n- Have your blood pressure checked at least once a year from age 40, or earlier if you are at risk"),
                ("livingWith",
                    "- قِس ضغطك في المنزل إذا نُصحت بذلك، وسجّل القراءات لتعرضها على طبيبك\n- لا توقف أدويتك دون استشارة\n- اسأل الصيدلي قبل تناول أدوية الزكام أو المسكنات التي قد ترفع الضغط",
                    "- Measure at home if you are advised to, and keep a record to show your doctor\n- Don't stop your medicines without advice\n- Ask your pharmacist before taking cold remedies or painkillers that can raise blood pressure")),
            Audiences: ["men", "women", "elderly"],
            Emergency: new("ضغط مرتفع جدًا مع ألم في الصدر أو ضيق شديد في التنفس أو صداع شديد\nعلامات جلطة دماغية: ميلان الوجه أو ضعف الذراع أو صعوبة الكلام\nارتباك مفاجئ أو فقدان الرؤية",
                "Very high blood pressure with chest pain, severe shortness of breath or a severe headache\nSigns of a stroke: a drooping face, a weak arm or difficulty speaking\nSudden confusion or loss of vision"),
            Urgent: new("قراءة 180/120 أو أعلى في المنزل دون أعراض أخرى\nدوخة أو إغماء بعد البدء بدواء جديد",
                "A home reading of 180/120 or higher without other symptoms\nDizziness or fainting after starting a new medicine"),
            Primary: new("لم تفحص ضغطك من قبل أو منذ أكثر من سنة\nقراءاتك المنزلية 135/85 أو أعلى في الغالب\nتحتاج إلى مراجعة أدويتك",
                "You have never had your blood pressure checked, or not in the last year\nYour home readings are often 135/85 or higher\nYou need a review of your medicines"),
            Specialties: ["Cardiology"], Facilities: ["Saud Al Babtain Cardiac Center"], Tools: ["bmi"], Related: ["ecg"],
            Sources: [Who("ارتفاع ضغط الدم", "Hypertension", "https://www.who.int/news-room/fact-sheets/detail/hypertension"), Nhs("ارتفاع ضغط الدم", "High blood pressure", "https://www.nhs.uk/conditions/high-blood-pressure/")]),

        new("asthma", false,
            new("الربو", "Asthma"),
            new("حساسية الصدر، الأزمة الصدرية، صفير الصدر", "bronchial asthma, wheezing, chest allergy"),
            new("حالة شائعة تلتهب فيها الشعب الهوائية وتضيق فيصعب التنفس. لا يُشفى الربو لكن يمكن السيطرة عليه جيدًا بالعلاج المناسب.",
                "A common condition where the airways become inflamed and narrow, making it hard to breathe. It can't be cured, but it can be well controlled with the right treatment."),
            ["lungs"],
            S(
                ("overview",
                    "الربو حالة طويلة الأمد تصيب الشعب الهوائية التي تنقل الهواء إلى الرئتين. عند التعرض لمحفزات معينة تلتهب الشعب وتضيق ويزداد المخاط فيها، فيصعب التنفس.\nيصيب الأطفال والكبار، ومع العلاج المناسب يستطيع معظم المصابين ممارسة حياتهم بشكل طبيعي.",
                    "Asthma is a long-term condition of the airways that carry air to the lungs. When they meet certain triggers, they become inflamed, narrow and fill with mucus, which makes breathing hard.\nIt affects children and adults, and with the right treatment most people live normal, active lives."),
                ("symptoms",
                    "تأتي الأعراض وتذهب، ومنها:\n- صفير في الصدر\n- ضيق التنفس\n- الشعور بضيق في الصدر\n- سعال يزداد غالبًا في الليل أو الصباح الباكر أو مع المجهود",
                    "Symptoms come and go, and include:\n- a whistling sound when breathing (wheezing)\n- shortness of breath\n- a tight chest\n- a cough, often worse at night, early in the morning or with exercise"),
                ("causes",
                    "السبب الدقيق غير معروف، ويزداد الاحتمال عند وجود تاريخ عائلي للربو أو الحساسية أو الإكزيما. ومن المحفزات الشائعة:\n- الغبار والعواصف الترابية\n- الدخان، بما فيه دخان السجائر والبخور\n- نزلات البرد والإنفلونزا\n- حبوب اللقاح وعث الغبار وشعر الحيوانات\n- المجهود البدني والهواء البارد\n- الروائح النفاذة والعطور",
                    "The exact cause isn't known. It is more likely if asthma, allergies or eczema run in your family. Common triggers include:\n- dust and sandstorms\n- smoke, including cigarette smoke and incense (bakhoor)\n- colds and flu\n- pollen, house dust mites and pet hair\n- exercise and cold air\n- strong smells and perfumes"),
                ("diagnosis",
                    "يعتمد التشخيص على الأعراض والفحص السريري واختبارات التنفس مثل قياس وظائف التنفس. وفي الأطفال الصغار قد يعتمد على الأعراض ومدى الاستجابة للعلاج.",
                    "It is based on your symptoms, an examination and breathing tests such as spirometry. In young children it may depend on symptoms and how they respond to treatment."),
                ("treatment",
                    "يُعالج الربو غالبًا بالبخاخات:\n- بخاخ إسعافي يفتح الشعب الهوائية بسرعة عند ظهور الأعراض\n- بخاخ وقائي يُستخدم يوميًا ليقلل الالتهاب\n- خطة عمل مكتوبة للربو توضح ما تفعله عند ازدياد الأعراض\n- تعلّم طريقة استخدام البخاخ الصحيحة، واستخدام الحجرة (السبيسر) للأطفال",
                    "Asthma is usually treated with inhalers:\n- a reliever inhaler opens the airways quickly when you have symptoms\n- a preventer inhaler, used every day, reduces inflammation\n- a written asthma action plan tells you what to do when symptoms get worse\n- learn the right inhaler technique, and use a spacer for children"),
                ("prevention",
                    "- تجنّب المحفزات التي تعرفها\n- لا تسمح بالتدخين في المنزل أو السيارة\n- ابقَ في الداخل وأغلق النوافذ وقت العواصف الترابية\n- خذ لقاح الإنفلونزا سنويًا\n- استمر على البخاخ الوقائي حتى عندما تشعر بتحسن",
                    "- Avoid the triggers you know about\n- Keep your home and car smoke-free\n- Stay indoors with the windows closed during sandstorms\n- Have the flu vaccine every year\n- Keep using your preventer inhaler even when you feel well"),
                ("livingWith",
                    "- احمل بخاخك الإسعافي دائمًا\n- راجع طبيبك مرة في السنة على الأقل\n- أعطِ مدرسة طفلك نسخة من خطة عمل الربو",
                    "- Always carry your reliever inhaler\n- Have an asthma review at least once a year\n- Give your child's school a copy of their asthma action plan")),
            Audiences: ["children"],
            Emergency: new("البخاخ الإسعافي لا يساعد\nضيق التنفس يمنعك من الكلام أو الأكل أو النوم\nازرقاق الشفاه أو الأصابع\nطفل لا يستطيع الكلام أو الرضاعة من ضيق التنفس، أو يبدو نعسانًا أو مرتبكًا",
                "Your reliever inhaler isn't helping\nYou are too breathless to speak, eat or sleep\nYour lips or fingers turn blue or grey\nA child is too breathless to talk or feed, or seems drowsy or confused"),
            Urgent: new("تحتاج إلى البخاخ الإسعافي أكثر من المعتاد\nالأعراض توقظك من النوم\nأصبت بنوبة ربو وتحتاج إلى متابعة خلال يومين",
                "You need your reliever inhaler more than usual\nSymptoms wake you at night\nYou had an asthma attack and need a follow-up within 2 days"),
            Primary: new("سعال أو صفير يتكرر\nلم تُراجَع حالة الربو لديك منذ أكثر من سنة\nلست متأكدًا من طريقة استخدام البخاخ",
                "You have a cough or wheeze that keeps coming back\nYour asthma hasn't been reviewed in the past year\nYou are not sure how to use your inhaler"),
            Specialties: ["Pediatrics"], Tools: ["asthma"], Related: ["spirometry"],
            Sources: [Who("الربو", "Asthma", "https://www.who.int/news-room/fact-sheets/detail/asthma"), Nhs("الربو", "Asthma", "https://www.nhs.uk/conditions/asthma/")]),

        new("flu", false,
            new("الإنفلونزا الموسمية", "Seasonal flu"),
            new("الإنفلونزا، النزلة الوافدة", "influenza, flu"),
            new("عدوى فيروسية شائعة تصيب الأنف والحلق والرئتين. يتعافى معظم الناس خلال أسبوع، لكنها قد تكون خطيرة على كبار السن والحوامل وأصحاب الأمراض المزمنة.",
                "A common viral infection of the nose, throat and lungs. Most people recover within a week, but it can be serious for older people, pregnant women and people with long-term conditions."),
            ["lungs", "infections"],
            S(
                ("overview",
                    "تسببها فيروسات الإنفلونزا، وتنتشر بسهولة عبر الرذاذ عند السعال والعطاس، وعبر لمس الأسطح الملوثة. تزداد الحالات في فصل الشتاء.\nيتعافى معظم الناس خلال أسبوع تقريبًا، لكنها قد تسبب مضاعفات مثل الالتهاب الرئوي لدى الفئات الأكثر عرضة.",
                    "Flu is caused by influenza viruses. It spreads easily through droplets when people cough or sneeze, and by touching contaminated surfaces. Cases rise in winter.\nMost people recover in about a week, but it can lead to complications such as pneumonia in people at higher risk."),
                ("symptoms",
                    "تبدأ الأعراض فجأة غالبًا، ومنها:\n- ارتفاع الحرارة\n- آلام الجسم\n- التعب الشديد\n- السعال الجاف\n- التهاب الحلق\n- الصداع\nبخلاف الزكام العادي، تأتي الإنفلونزا بسرعة وتجعلك متعبًا لدرجة لا تستطيع معها ممارسة يومك.",
                    "Symptoms usually start suddenly and include:\n- a high temperature\n- an aching body\n- feeling exhausted\n- a dry cough\n- a sore throat\n- a headache\nUnlike a cold, flu comes on quickly and makes you feel too unwell to carry on as normal."),
                ("causes",
                    "تسببها فيروسات الإنفلونزا التي تتغير من موسم إلى آخر، ولهذا يُحدَّث اللقاح كل عام.",
                    "It is caused by influenza viruses, which change from season to season. This is why the vaccine is updated every year."),
                ("diagnosis",
                    "يُشخَّص غالبًا من الأعراض. وقد يُؤخذ مسح من الأنف أو الحلق في بعض الحالات، خاصة في المستشفى.",
                    "It is usually diagnosed from the symptoms. A nose or throat swab is sometimes taken, especially in hospital."),
                ("treatment",
                    "- الراحة وشرب الكثير من السوائل\n- خافض الحرارة مثل الباراسيتامول للحمى والآلام\n- المضادات الحيوية لا تفيد ضد الفيروسات\n- قد يصف الطبيب دواءً مضادًا للفيروسات للأشخاص الأكثر عرضة للخطر إذا بدأ مبكرًا",
                    "- Rest and drink plenty of fluids\n- Take paracetamol for fever and aches\n- Antibiotics don't work against viruses\n- A doctor may prescribe an antiviral medicine for people at higher risk if it is started early"),
                ("prevention",
                    "- لقاح الإنفلونزا السنوي هو أفضل وقاية، ويُنصح به لمعظم الناس من عمر ستة أشهر، خاصة الأكثر عرضة للخطر\n- اغسل يديك كثيرًا\n- غطِّ فمك وأنفك عند السعال والعطاس\n- ابقَ في المنزل عندما تكون مريضًا",
                    "- The yearly flu vaccine is the best protection. It is recommended for most people from 6 months of age, especially those at higher risk\n- Wash your hands often\n- Cover your mouth and nose when you cough or sneeze\n- Stay at home when you are ill")),
            Audiences: ["children", "elderly", "pregnancy"],
            Emergency: new("صعوبة شديدة مفاجئة في التنفس أو ألم في الصدر\nازرقاق الشفاه أو الوجه\nتشنجات، أو نعاس شديد وصعوبة في الاستيقاظ\nرضيع أو طفل خامل لا يرضع، أو لديه طفح جلدي لا يختفي عند الضغط عليه",
                "Sudden severe difficulty breathing or chest pain\nBlue lips or face\nA seizure, or being very drowsy and hard to wake\nA baby or child who is floppy, not feeding, or has a rash that doesn't fade when pressed"),
            Urgent: new("أنت من الفئات الأكثر عرضة (حامل، أو فوق 65 عامًا، أو لديك مرض مزمن) ولديك أعراض الإنفلونزا\nالأعراض تزداد أو لا تتحسن بعد أسبوع\nحرارة لدى رضيع عمره أقل من ثلاثة أشهر",
                "You are at higher risk (pregnant, over 65 or with a long-term condition) and have flu symptoms\nSymptoms get worse or don't improve after a week\nA fever in a baby under 3 months old"),
            Primary: new("تريد أخذ لقاح الإنفلونزا\nلديك أسئلة عن الوقاية من الإنفلونزا أثناء الحمل",
                "You want the flu vaccine\nYou have questions about flu protection in pregnancy"),
            Sources: [Who("الإنفلونزا الموسمية", "Influenza (seasonal)", "https://www.who.int/news-room/fact-sheets/detail/influenza-(seasonal)"), Nhs("الإنفلونزا", "Flu", "https://www.nhs.uk/conditions/flu/")]),

        new("sickle-cell-disease", false,
            new("فقر الدم المنجلي", "Sickle cell disease"),
            new("الأنيميا المنجلية، أنيميا الخلايا المنجلية، السكلر", "sickle cell anaemia, SCD"),
            new("مرض وراثي في الدم شائع في المنطقة الشرقية، تتحول فيه خلايا الدم الحمراء إلى شكل منجلي فتسبب نوبات ألم وفقر دم. الرعاية المنتظمة تصنع فرقًا كبيرًا.",
                "An inherited blood disorder that is common in the Eastern Province. Red blood cells become sickle shaped, causing pain crises and anaemia. Regular care makes a big difference."),
            ["blood"],
            S(
                ("overview",
                    "خلايا الدم الحمراء مستديرة ومرنة في العادة. في فقر الدم المنجلي تصبح صلبة على شكل هلال (منجل)، فقد تسد الأوعية الدموية الصغيرة وتسبب الألم والضرر، كما تتكسر أسرع من المعتاد فيحدث فقر الدم.\nالمرض يلازم المصاب مدى الحياة، وتختلف شدته من شخص لآخر. ومع الرعاية الجيدة يعيش كثير من المصابين حياة طويلة ونشطة.",
                    "Red blood cells are normally round and flexible. In sickle cell disease they become stiff and shaped like a crescent (sickle). They can block small blood vessels, causing pain and damage, and they break down faster than usual, causing anaemia.\nIt is lifelong, and how severe it is varies from person to person. With good care, many people live long, active lives."),
                ("symptoms",
                    "- نوبات ألم (أزمات) في العظام أو الصدر أو الظهر أو البطن\n- التعب وضيق التنفس بسبب فقر الدم\n- تكرار الالتهابات\n- اصفرار العينين\n- تورم اليدين والقدمين لدى الرضع\n- تأخر النمو لدى الأطفال",
                    "- Episodes of pain (crises) in the bones, chest, back or tummy\n- Tiredness and shortness of breath from anaemia\n- Frequent infections\n- Yellow eyes (jaundice)\n- Swollen hands and feet in babies\n- Delayed growth in children"),
                ("causes",
                    "فقر الدم المنجلي وراثي. يُصاب الطفل عندما يرث جين المرض من الأب والأم معًا. أما من يرث الجين من أحد الوالدين فقط فيكون «حاملًا للسمة»، ويعيش عادة بصحة جيدة لكنه قد ينقل الجين لأبنائه.\nإذا كان الوالدان حاملين للسمة، فاحتمال إصابة الطفل في كل حمل هو واحد من أربعة.",
                    "Sickle cell disease is inherited. A child has it when they inherit the sickle gene from both parents. Someone who inherits the gene from only one parent has sickle cell trait: they are usually healthy but can pass the gene on.\nIf both parents have the trait, each pregnancy has a 1 in 4 chance of a child with sickle cell disease."),
                ("diagnosis",
                    "يكشف تحليل دم بسيط إن كان الشخص مصابًا أو حاملًا للسمة. وفي المملكة يكشف فحص ما قبل الزواج ما إذا كان الخاطبان يحملان الجين.",
                    "A simple blood test shows whether someone has sickle cell disease or carries the trait. In Saudi Arabia, premarital screening shows whether a couple carries the gene."),
                ("treatment",
                    "يُتابَع المرض مع فريق متخصص، ويشمل العلاج:\n- أدوية يومية مثل حمض الفوليك، ولدى بعض المرضى أدوية تقلل عدد النوبات\n- تطعيمات ومضادات حيوية وقائية لحماية الأطفال من الالتهابات\n- مسكنات الألم\n- نقل الدم لبعض المرضى\n- زراعة نخاع العظم التي قد تشفي بعض المرضى",
                    "Care is led by a specialist team and includes:\n- daily medicines such as folic acid, and for some people a medicine that reduces crises\n- vaccinations and preventive antibiotics to protect children from infections\n- pain relief\n- blood transfusions for some people\n- a bone marrow transplant, which can cure some people"),
                ("prevention",
                    "لا يمكن منع المرض إذا كان موروثًا، لكن فحص ما قبل الزواج والاستشارة الوراثية يساعدان الأزواج على معرفة احتمالاتهم.\nولتقليل النوبات:\n- اشرب الكثير من الماء، خاصة في الجو الحار\n- تجنّب البرد الشديد والحرارة الشديدة والإجهاد الزائد\n- حافظ على تطعيماتك محدّثة",
                    "You can't prevent it if it is inherited, but premarital screening and genetic counselling help couples understand their chances.\nTo reduce crises:\n- drink plenty of water, especially in hot weather\n- avoid extreme cold, extreme heat and over-exertion\n- keep vaccinations up to date"),
                ("livingWith",
                    "- احضر مواعيد المتابعة بانتظام\n- تعلّم علامات الخطر واطلب المساعدة مبكرًا\n- أخبر المدرسة أو جهة العمل بحالتك وحاجتك إلى شرب الماء والراحة",
                    "- Go to your regular check-ups\n- Learn the warning signs and get help early\n- Tell the school or your employer about the condition and your need for water and rest")),
            Audiences: ["children", "pregnancy"], Featured: true,
            Emergency: new("ألم في الصدر أو صعوبة في التنفس\nعلامات جلطة دماغية: ضعف في الوجه أو الذراع أو الساق، أو صعوبة في الكلام\nحرارة 38 درجة أو أعلى لدى طفل مصاب\nألم شديد لا تخففه المسكنات المعتادة\nانتصاب مؤلم يستمر أكثر من ساعتين",
                "Chest pain or difficulty breathing\nSigns of a stroke: weakness in the face, arm or leg, or difficulty speaking\nA temperature of 38°C or higher in a child with sickle cell disease\nSevere pain that your usual pain relief doesn't ease\nA painful erection lasting more than 2 hours"),
            Urgent: new("اصفرار العينين أكثر من المعتاد\nقيء وعدم القدرة على الاحتفاظ بالسوائل\nنوبة ألم خفيفة تعالجها في المنزل لكنك قلق منها",
                "Yellow eyes that are worse than usual\nVomiting and being unable to keep fluids down\nA mild pain crisis you are managing at home but are worried about"),
            Primary: new("تريد أنت وشريكك معرفة احتمالاتكما قبل الإنجاب\nتحتاج إلى مراجعة دورية أو تجديد وصفة",
                "You and your partner want to know your chances before planning a family\nYou need a routine review or a repeat prescription"),
            Specialties: ["Pediatrics"], Facilities: ["Prince Mohammad Hospital for Genetic Blood Disease", "Qatif Central Hospital"], Related: ["complete-blood-count", "premarital-screening"],
            Sources: [Who("مرض الخلايا المنجلية", "Sickle cell disease", "https://www.who.int/news-room/fact-sheets/detail/sickle-cell-disease"), Medline("مرض الخلايا المنجلية", "Sickle cell disease", "https://medlineplus.gov/sicklecelldisease.html")]),

        new("g6pd-deficiency", false,
            new("نقص إنزيم G6PD (أنيميا الفول)", "G6PD deficiency"),
            new("أنيميا الفول، نقص خميرة G6PD، الفوال", "favism, glucose-6-phosphate dehydrogenase deficiency"),
            new("حالة وراثية شائعة في المنطقة الشرقية لا تسبب أعراضًا غالبًا، لكن بعض الأطعمة والأدوية قد تسبب تكسر خلايا الدم الحمراء.",
                "An inherited condition that is common in the Eastern Province. It usually causes no symptoms, but certain foods and medicines can make red blood cells break down."),
            ["blood"],
            S(
                ("overview",
                    "إنزيم G6PD يحمي خلايا الدم الحمراء. عند نقصه تكون هذه الخلايا أكثر عرضة للتلف. معظم المصابين لا يشعرون بشيء في حياتهم اليومية، لكن بعض الأطعمة أو الأدوية أو الالتهابات قد تسبب تكسرًا سريعًا لخلايا الدم الحمراء (انحلال الدم).\nالحالة أكثر شيوعًا لدى الذكور، وهي شائعة في المنطقة الشرقية.",
                    "The G6PD enzyme protects red blood cells. Without enough of it, red blood cells are easily damaged. Most people feel fine day to day, but certain foods, medicines or infections can make red blood cells break down quickly (haemolysis).\nIt is more common in boys and men, and it is common in the Eastern Province."),
                ("symptoms",
                    "لا توجد أعراض في الغالب. وعند حدوث نوبة تكسر قد تظهر:\n- شحوب الجلد\n- اصفرار الجلد أو العينين\n- بول داكن بلون الشاي\n- التعب وسرعة ضربات القلب وضيق التنفس\nوقد يظهر لدى المواليد على شكل يرقان (صفار).",
                    "There are usually no symptoms. During an episode, signs can include:\n- pale skin\n- yellow skin or eyes\n- dark, tea-coloured urine\n- tiredness, a fast heartbeat and shortness of breath\nIn newborns it can show as jaundice."),
                ("causes",
                    "الحالة وراثية وتنتقل على الكروموسوم X، لذلك يُصاب الذكور أكثر، وقد تكون الإناث حاملات أو مصابات. ومن محفزات النوبات:\n- أكل الفول أو منتجاته\n- بعض الأدوية، منها بعض المضادات الحيوية وأدوية الملاريا\n- الالتهابات\n- كرات النفثالين (العثّة)",
                    "It is inherited on the X chromosome, so boys are affected more often; girls can be carriers or affected. Episodes can be triggered by:\n- eating fava beans (broad beans, ful) or foods made from them\n- some medicines, including some antibiotics and antimalarials\n- infections\n- naphthalene mothballs"),
                ("diagnosis",
                    "يقيس تحليل دم مستوى الإنزيم. وقد تُكتشف الحالة في فحص المواليد أو بعد حدوث نوبة.",
                    "A blood test measures the enzyme level. It may be found through newborn screening or after an episode."),
                ("treatment",
                    "لا يوجد علاج شافٍ، ومعظم المصابين لا يحتاجون إلى علاج. الأهم تجنّب المحفزات، وتهدأ النوبة عادة بعد إزالة المحفز. قد تحتاج النوبات الشديدة إلى دخول المستشفى ونقل الدم، ويُعالج صفار المواليد بالعلاج الضوئي.",
                    "There is no cure, and most people need no treatment. The key is avoiding triggers; an episode usually settles once the trigger is removed. Severe episodes may need hospital care and a blood transfusion, and jaundice in newborns is treated with light therapy."),
                ("prevention",
                    "- تجنّب الفول ومنتجاته\n- أخبر الطبيب وطبيب الأسنان والصيدلي دائمًا بحالتك قبل تناول أي دواء\n- تجنّب كرات النفثالين\n- اطلب قائمة بالأدوية التي يجب تجنبها",
                    "- Avoid fava beans and foods made from them\n- Always tell doctors, dentists and pharmacists before taking any medicine\n- Avoid naphthalene mothballs\n- Ask for a list of medicines to avoid"),
                ("livingWith",
                    "- احمل بطاقة تذكر حالتك\n- علّم أفراد أسرتك المحفزات وعلامات النوبة\n- اقرأ مكونات الأطعمة والأدوية",
                    "- Carry a card that mentions the condition\n- Teach your family the triggers and the signs of an episode\n- Read food and medicine labels")),
            Audiences: ["children", "men"], Featured: true,
            Emergency: new("شحوب شديد أو تعب شديد أو ضيق تنفس أو إغماء\nبول داكن مع اصفرار العينين وشعور شديد بالمرض\nمولود بشرته أو عيناه صفراوان جدًا ويبدو نعسانًا أو لا يرضع",
                "Very pale, very tired, short of breath or fainting\nDark urine with yellow eyes and feeling very unwell\nA newborn whose skin or eyes are very yellow and who is sleepy or not feeding"),
            Urgent: new("بول داكن بلون الشاي بعد أكل الفول أو تناول دواء جديد\nاصفرار الجلد أو العينين",
                "Dark, tea-coloured urine after eating fava beans or taking a new medicine\nYellow skin or eyes"),
            Primary: new("تريد إجراء الفحص، أو اكتُشفت الحالة لدى طفلك\nتحتاج إلى نصيحة حول الأدوية التي يجب تجنبها",
                "You want to be tested, or your child was found to have G6PD deficiency\nYou need advice on which medicines to avoid"),
            Specialties: ["Pediatrics"], Facilities: ["Prince Mohammad Hospital for Genetic Blood Disease"], Related: ["complete-blood-count"],
            Sources: [Medline("نقص إنزيم G6PD", "Glucose-6-phosphate dehydrogenase deficiency", "https://medlineplus.gov/genetics/condition/glucose-6-phosphate-dehydrogenase-deficiency/")]),

        new("breast-cancer", false,
            new("سرطان الثدي", "Breast cancer"),
            new("أورام الثدي، كتلة الثدي", "breast tumour, breast lump"),
            new("أكثر أنواع السرطان شيوعًا بين النساء. اكتشافه مبكرًا يجعل علاجه ناجحًا في كثير من الحالات.",
                "The most common cancer among women. When it is found early, it can often be treated successfully."),
            ["cancer", "reproductive"],
            S(
                ("overview",
                    "سرطان الثدي هو نمو غير طبيعي لخلايا في الثدي. وهو أكثر أنواع السرطان شيوعًا بين النساء في العالم. نادرًا ما يصيب الرجال.\nكلما اكتُشف مبكرًا زادت فرص نجاح العلاج، لذلك من المهم أن تعرفي شكل ثدييك الطبيعي وأن تجري الفحوصات الدورية.",
                    "Breast cancer is the abnormal growth of cells in the breast. It is the most common cancer among women worldwide; men can get it too, but rarely.\nThe earlier it is found, the more likely treatment is to succeed, so it helps to know what is normal for your breasts and to have regular screening."),
                ("symptoms",
                    "- كتلة جديدة أو سماكة في الثدي أو تحت الإبط\n- تغير في حجم الثدي أو شكله\n- تغير في الجلد مثل التنقّر أو الاحمرار\n- تغير في الحلمة: انقلابها للداخل أو طفح حولها أو إفرازات منها، خاصة الدموية\nالألم وحده نادرًا ما يكون علامة على السرطان، لكن الألم المستمر يستحق الفحص.",
                    "- A new lump or thickening in the breast or armpit\n- A change in the size or shape of the breast\n- Skin changes such as dimpling or redness\n- Nipple changes: turning inwards, a rash around it, or discharge, especially if bloody\nPain on its own is rarely a sign of cancer, but pain that doesn't go away should be checked."),
                ("causes",
                    "السبب الدقيق غير معروف، وتزيد بعض العوامل الخطر:\n- التقدم في العمر\n- تاريخ عائلي لسرطان الثدي أو المبيض\n- زيادة الوزن بعد انقطاع الطمث\n- قلة النشاط البدني\nكثير من المصابات ليس لديهن عامل خطر واضح، والرضاعة الطبيعية تقلل الخطر.",
                    "The exact cause isn't known. Some things raise the risk:\n- getting older\n- a family history of breast or ovarian cancer\n- being overweight after the menopause\n- not being physically active\nMany women who get breast cancer have no clear risk factor. Breastfeeding lowers the risk."),
                ("diagnosis",
                    "يبدأ بفحص سريري، ثم تصوير الثدي بالأشعة (الماموجرام) أو الموجات فوق الصوتية، وقد تؤخذ خزعة صغيرة لتأكيد التشخيص.",
                    "It starts with an examination, then a mammogram or an ultrasound scan. A small sample (biopsy) may be taken to confirm the diagnosis."),
                ("treatment",
                    "يضع فريق متعدد التخصصات خطة علاج تناسب كل مريضة، وقد تشمل:\n- الجراحة\n- العلاج الإشعاعي\n- العلاج الكيميائي\n- العلاج الهرموني أو الموجّه",
                    "A multidisciplinary team plans treatment for each person. It may include:\n- surgery\n- radiotherapy\n- chemotherapy\n- hormone therapy or targeted therapy"),
                ("prevention",
                    "- اعرفي شكل ثدييك الطبيعي وافحصيهما بانتظام\n- اسألي مركزك الصحي عن فحص الماموجرام إذا كان عمرك 40 عامًا أو أكثر\n- حافظي على وزن صحي ونشاط منتظم\n- الرضاعة الطبيعية تقلل الخطر",
                    "- Know what is normal for your breasts and check them regularly\n- Ask your health centre about screening mammograms if you are 40 or over\n- Keep a healthy weight and stay active\n- Breastfeeding lowers the risk"),
                ("livingWith",
                    "- لا تترددي في طلب الدعم النفسي، فهو جزء من العلاج\n- احضري مواعيد المتابعة بعد انتهاء العلاج\n- أخبري فريقك الطبي بأي أعراض جديدة",
                    "- Ask for emotional support: it is part of your care\n- Go to your follow-up appointments after treatment\n- Tell your care team about any new symptoms")),
            Audiences: ["women"],
            Primary: new("لاحظتِ كتلة أو أي تغير في الثدي أو الإبط\nلديكِ إفرازات من الحلمة أو انقلبت الحلمة للداخل\nعمركِ 40 عامًا أو أكثر ولم تُجري فحص الماموجرام",
                "You notice a lump or any change in your breast or armpit\nYou have nipple discharge or a nipple that has turned inwards\nYou are 40 or over and have never had a mammogram"),
            Specialties: ["Oncology", "General surgery"], Facilities: ["King Fahad Specialist Hospital"], Related: ["mammogram"],
            Sources: [Who("سرطان الثدي", "Breast cancer", "https://www.who.int/news-room/fact-sheets/detail/breast-cancer"), Nhs("سرطان الثدي لدى النساء", "Breast cancer in women", "https://www.nhs.uk/conditions/breast-cancer-in-women/")]),

        new("migraine", false,
            new("الصداع النصفي", "Migraine"),
            new("الشقيقة، الصداع", "migraine headache"),
            new("نوع شائع من الصداع يسبب ألمًا نابضًا متوسطًا أو شديدًا، غالبًا في جانب واحد من الرأس، وقد يصاحبه غثيان وحساسية للضوء.",
                "A common type of headache that causes moderate or severe throbbing pain, usually on one side of the head, often with sickness and sensitivity to light."),
            ["brain"],
            S(
                ("overview",
                    "الصداع النصفي حالة شائعة، خاصة لدى النساء. تستمر النوبة من 4 ساعات إلى 3 أيام، ويختلف عدد النوبات من شخص لآخر.",
                    "Migraine is common, especially in women. An attack lasts from 4 hours to 3 days, and how often attacks happen varies from person to person."),
                ("symptoms",
                    "- صداع نابض، غالبًا في جانب واحد من الرأس\n- غثيان أو قيء\n- حساسية للضوء أو الصوت أو الروائح\nقد يسبق الصداع لدى بعض الناس «هالة» مثل ومضات ضوئية أو خطوط متعرجة أو تنميل.",
                    "- A throbbing headache, usually on one side of the head\n- Feeling or being sick\n- Sensitivity to light, sound or smells\nSome people have an aura before the headache, such as flashing lights, zigzag lines or tingling."),
                ("causes",
                    "السبب الدقيق غير معروف. ومن المحفزات الشائعة:\n- قلة النوم أو تغير مواعيده\n- تأخير الوجبات وقلة شرب الماء\n- التوتر\n- الأضواء الساطعة والروائح القوية\n- التغيرات الهرمونية مثل الدورة الشهرية\n- التوقف المفاجئ عن الكافيين، كما في بداية الصيام",
                    "The exact cause isn't known. Common triggers include:\n- too little sleep or a changed sleep pattern\n- skipped meals and not drinking enough water\n- stress\n- bright lights and strong smells\n- hormonal changes, such as periods\n- stopping caffeine suddenly, as at the start of a fast"),
                ("diagnosis",
                    "يُشخَّص من نمط الأعراض. قد يطلب منك الطبيب تسجيل نوبات الصداع في مفكرة، ونادرًا ما تحتاج إلى أشعة.",
                    "It is diagnosed from the pattern of your symptoms. Your doctor may ask you to keep a headache diary; scans are rarely needed."),
                ("treatment",
                    "- الراحة في غرفة هادئة ومظلمة\n- تناول المسكن في بداية النوبة\n- أدوية للغثيان عند الحاجة\n- أدوية خاصة بالصداع النصفي يصفها الطبيب\n- أدوية وقائية إذا تكررت النوبات\nتجنّب الإفراط في المسكنات، فتناولها معظم أيام الشهر قد يسبب صداعًا بحد ذاته.",
                    "- Rest in a quiet, dark room\n- Take a painkiller early in the attack\n- Anti-sickness medicine if needed\n- Migraine-specific medicines prescribed by your doctor\n- Preventive medicines if attacks are frequent\nAvoid taking painkillers on most days of the month, as this can itself cause headaches."),
                ("prevention",
                    "- حافظ على مواعيد منتظمة للنوم والوجبات\n- اشرب الماء بكثرة\n- سجّل نوباتك لتتعرف على المحفزات\n- قلّل الكافيين تدريجيًا قبل رمضان",
                    "- Keep regular times for sleep and meals\n- Drink plenty of water\n- Keep a diary to find your triggers\n- Cut down caffeine gradually before Ramadan")),
            Audiences: ["women"],
            Emergency: new("صداع مفاجئ وشديد جدًا، الأسوأ في حياتك\nصداع مع ضعف أو تنميل أو تلعثم في الكلام أو ارتباك\nصداع مع حرارة وتيبّس في الرقبة وطفح جلدي\nصداع بعد إصابة في الرأس",
                "A sudden, very severe headache, the worst you have ever had\nA headache with weakness, numbness, slurred speech or confusion\nA headache with a fever, a stiff neck and a rash\nA headache after a head injury"),
            Urgent: new("نوبتك المعتادة أشد بكثير أو تستمر أكثر من 3 أيام\nتغيرات في الرؤية لا تزول",
                "Your usual migraine is much worse or lasts more than 3 days\nVision changes that don't go away"),
            Primary: new("صداع يتكرر عدة مرات في الشهر\nالمسكنات لم تعد تفيد، أو تتناولها معظم الأيام\nأنتِ حامل ولديكِ صداع نصفي",
                "You have headaches several times a month\nPainkillers no longer help, or you take them on most days\nYou are pregnant and have migraines"),
            Sources: [Nhs("الصداع النصفي", "Migraine", "https://www.nhs.uk/conditions/migraine/")]),

        new("kidney-stones", false,
            new("حصى الكلى", "Kidney stones"),
            new("حصوة الكلى، حصى المسالك البولية، المغص الكلوي", "renal stones, urinary stones, renal colic"),
            new("ترسبات صلبة تتكون في الكلى، وتزداد في الأجواء الحارة وقلة شرب الماء. قد تسبب ألمًا شديدًا عند انسداد مجرى البول.",
                "Hard deposits that form in the kidneys, more often in hot weather and when you don't drink enough. They can cause severe pain if they block the flow of urine."),
            ["kidneys"],
            S(
                ("overview",
                    "تتكون الحصى من مواد موجودة في البول. قد تمر الحصى الصغيرة دون أن تشعر بها، أما الكبيرة فقد تسد مجرى البول وتسبب ألمًا شديدًا.\nحصى الكلى أكثر شيوعًا في المناطق الحارة حيث يفقد الجسم الماء بالتعرق.",
                    "Stones form from substances in the urine. Small stones may pass without you noticing, but larger ones can block the flow of urine and cause severe pain.\nThey are more common in hot climates, where the body loses water through sweating."),
                ("symptoms",
                    "- ألم شديد في الجنب أو الظهر قد يمتد إلى أسفل البطن أو الفخذ، ويأتي على شكل موجات\n- دم في البول (لون وردي أو أحمر أو بني)\n- غثيان أو قيء\n- الحاجة المتكررة للتبول أو ألم عند التبول\n- حرارة ورعشة إذا صاحبها التهاب",
                    "- Severe pain in the side or back that may spread to the lower tummy or groin, coming in waves\n- Blood in the urine (pink, red or brown)\n- Feeling or being sick\n- Needing to pass urine often, or pain when passing urine\n- A high temperature and shivering if there is an infection"),
                ("causes",
                    "- قلة شرب الماء\n- الجو الحار والتعرق\n- الإكثار من الملح أو البروتين الحيواني\n- وجود تاريخ عائلي\n- بعض الأدوية والحالات الصحية",
                    "- Not drinking enough water\n- Hot weather and sweating\n- A diet high in salt or animal protein\n- A family history of kidney stones\n- Some medicines and health conditions"),
                ("diagnosis",
                    "تحليل للبول والدم، وأشعة بالموجات فوق الصوتية أو أشعة مقطعية لتحديد حجم الحصوة ومكانها.",
                    "Urine and blood tests, and an ultrasound or CT scan to find the size and position of the stone."),
                ("treatment",
                    "- الحصى الصغيرة تمر غالبًا مع شرب الماء ومسكنات الألم\n- قد تساعد بعض الأدوية على مرورها\n- الحصى الكبيرة قد تحتاج إلى تفتيت بالموجات التصادمية أو منظار أو جراحة",
                    "- Small stones usually pass with water and pain relief\n- Some medicines can help a stone pass\n- Larger stones may need shock wave treatment, a telescope procedure (ureteroscopy) or surgery"),
                ("prevention",
                    "- اشرب الماء طوال اليوم حتى يصبح لون البول فاتحًا\n- زد كمية الماء في الجو الحار ومع الرياضة\n- قلّل الملح\n- اسأل طبيبك عن النظام الغذائي المناسب لنوع الحصوة",
                    "- Drink water throughout the day so your urine is pale\n- Drink more in hot weather and when exercising\n- Cut down on salt\n- Ask your doctor about a diet that suits your type of stone")),
            Audiences: ["men", "women"],
            Emergency: new("ألم شديد في الجنب أو الظهر مع حرارة 38 درجة أو أعلى أو رعشة\nألم شديد لا تستطيع معه البقاء ساكنًا ولا تخففه المسكنات\nعدم القدرة على التبول نهائيًا",
                "Severe pain in your side or back with a temperature of 38°C or higher, or shivering\nPain so severe you can't stay still and pain relief doesn't help\nYou can't pass urine at all"),
            Urgent: new("ألم في الجنب يأتي ويذهب\nدم في البول\nحرقة عند التبول مع ألم في الجنب",
                "Pain in your side that comes and goes\nBlood in your urine\nA burning feeling when you pass urine, with pain in your side"),
            Primary: new("أصبت بحصى الكلى سابقًا وتريد نصيحة للوقاية من تكرارها",
                "You have had kidney stones before and want advice on preventing them"),
            Sources: [Nhs("حصى الكلى", "Kidney stones", "https://www.nhs.uk/conditions/kidney-stones/")]),

        new("low-back-pain", false,
            new("آلام أسفل الظهر", "Low back pain"),
            new("ألم الظهر، وجع الظهر، عرق النسا", "backache, lower back pain, sciatica"),
            new("مشكلة شائعة جدًا تتحسن غالبًا خلال أسابيع قليلة. الاستمرار في الحركة يساعد على التعافي.",
                "A very common problem that usually gets better within a few weeks. Staying active helps you recover."),
            ["bones"],
            S(
                ("overview",
                    "آلام أسفل الظهر من أكثر المشكلات الصحية شيوعًا. في أغلب الحالات لا يكون وراءها سبب خطير، وتتحسن خلال أسابيع قليلة.",
                    "Low back pain is one of the most common health problems. In most cases there is no serious cause, and it gets better within a few weeks."),
                ("symptoms",
                    "- ألم أو تيبّس في أسفل الظهر\n- ألم يمتد إلى الأرداف أو الساق\n- تشنج في العضلات",
                    "- Pain or stiffness in the lower back\n- Pain that spreads to the buttocks or leg\n- Muscle spasms"),
                ("causes",
                    "- الإجهاد عند رفع الأشياء\n- الجلوس الطويل والوضعيات غير الصحيحة\n- قلة الرياضة وزيادة الوزن\n- أحيانًا انزلاق غضروفي أو ضغط على العصب (عرق النسا)",
                    "- Strain from lifting\n- Sitting for long periods and poor posture\n- Too little exercise and being overweight\n- Sometimes a slipped disc or a trapped nerve (sciatica)"),
                ("diagnosis",
                    "يعتمد على الأعراض والفحص السريري. لا تحتاج معظم الحالات إلى أشعة إلا إذا وُجدت علامات تحذيرية.",
                    "It is based on your symptoms and an examination. Most people don't need a scan unless there are warning signs."),
                ("treatment",
                    "- استمر في الحركة ومارس أنشطتك اليومية قدر الإمكان\n- تمارين خفيفة ومنتظمة\n- مسكنات مثل الباراسيتامول أو الإيبوبروفين لفترة قصيرة\n- الكمادات الدافئة أو الباردة\n- العلاج الطبيعي إذا لم يتحسن الألم",
                    "- Keep moving and carry on with daily activities as much as you can\n- Gentle, regular exercise\n- Painkillers such as paracetamol or ibuprofen for a short time\n- Heat or cold packs\n- Physiotherapy if the pain doesn't improve"),
                ("prevention",
                    "- مارس الرياضة بانتظام، خاصة تمارين الظهر والبطن\n- ارفع الأشياء بثني الركبتين وإبقاء الظهر مستقيمًا\n- خذ فترات راحة من الجلوس\n- حافظ على وزن صحي",
                    "- Exercise regularly, especially back and core exercises\n- Lift by bending your knees and keeping your back straight\n- Take breaks from sitting\n- Keep a healthy weight")),
            Audiences: ["men", "women", "elderly"],
            Emergency: new("ألم في الظهر مع تنميل حول المقعدة أو الأعضاء التناسلية\nفقدان التحكم في البول أو البراز\nألم في الظهر بعد سقوط أو حادث شديد\nألم في الظهر مع ألم في الصدر",
                "Back pain with numbness around your bottom or genitals\nYou can't control your bladder or bowels\nBack pain after a serious fall or accident\nBack pain with chest pain"),
            Urgent: new("ألم في الظهر مع حرارة وشعور بالمرض\nتنميل أو وخز أو ضعف في إحدى الساقين أو كلتيهما\nألم يزداد ولا تخففه الراحة أو المسكنات",
                "Back pain with a fever and feeling unwell\nNumbness, tingling or weakness in one or both legs\nPain that is getting worse and isn't eased by rest or painkillers"),
            Primary: new("ألم لم يتحسن بعد 4 إلى 6 أسابيع\nألم يمنعك من ممارسة أنشطتك اليومية",
                "Pain that hasn't improved after 4 to 6 weeks\nPain that stops you from doing your daily activities"),
            Specialties: ["Orthopedics"],
            Sources: [Nhs("ألم الظهر", "Back pain", "https://www.nhs.uk/conditions/back-pain/")]),

        // ---------------- tests and procedures ----------------
        new("hba1c-test", true,
            new("فحص السكر التراكمي (HbA1c)", "HbA1c test"),
            new("السكر التراكمي، الهيموجلوبين السكري، A1c", "glycated haemoglobin, A1c, average blood sugar test"),
            new("تحليل دم يُظهر متوسط السكر في الدم خلال آخر شهرين إلى ثلاثة أشهر.",
                "A blood test that shows your average blood sugar over the past 2 to 3 months."),
            ["hormones", "blood"],
            S(
                ("about", "يقيس الفحص نسبة السكر الملتصق بالهيموجلوبين في خلايا الدم الحمراء، فيعطي صورة عن متوسط السكر لعدة أشهر بدلًا من لحظة واحدة.",
                    "It measures how much sugar is attached to the haemoglobin in your red blood cells. This gives a picture of your average blood sugar over several months rather than at one moment."),
                ("why", "- تشخيص السكري من النوع الثاني ومرحلة ما قبل السكري\n- متابعة التحكم في السكري، عادة كل 3 إلى 6 أشهر",
                    "- To diagnose type 2 diabetes and prediabetes\n- To check how well diabetes is controlled, usually every 3 to 6 months"),
                ("prepare", "لا يحتاج عادة إلى صيام. أخبر طبيبك إذا كان لديك فقر دم أو اضطراب في الدم مثل فقر الدم المنجلي، لأنه قد يؤثر على النتيجة.",
                    "You usually don't need to fast. Tell your doctor if you have anaemia or a blood disorder such as sickle cell disease, as it can affect the result."),
                ("during", "تُسحب عينة صغيرة من الدم من وريد في الذراع خلال دقائق. وتستخدم بعض العيادات جهازًا يكتفي بوخزة في الإصبع.",
                    "A small blood sample is taken from a vein in your arm, which takes a few minutes. Some clinics use a finger-prick device."),
                ("results", "تظهر النتيجة كنسبة مئوية. وبشكل عام عند التشخيص:\n- أقل من 5.7%: طبيعي\n- من 5.7% إلى 6.4%: ما قبل السكري\n- 6.5% أو أكثر: سكري، ويُؤكَّد بفحص ثانٍ\nإذا كنت مصابًا بالسكري فسيتفق معك طبيبك على هدف شخصي.",
                    "The result is a percentage. As a general guide for diagnosis:\n- below 5.7%: normal\n- 5.7% to 6.4%: prediabetes\n- 6.5% or higher: diabetes, confirmed with a second test\nIf you have diabetes, your doctor will agree a personal target with you."),
                ("risks", "المخاطر قليلة جدًا، وقد تظهر كدمة بسيطة مكان سحب الدم.",
                    "The risks are very small. You may get a slight bruise where the blood was taken.")),
            Related: ["type-2-diabetes"],
            Sources: [Medline("فحص السكر التراكمي", "Hemoglobin A1C (HbA1c) test", "https://medlineplus.gov/lab-tests/hemoglobin-a1c-hba1c-test/")]),

        new("complete-blood-count", true,
            new("تحليل صورة الدم الكاملة (CBC)", "Complete blood count (CBC)"),
            new("صورة الدم، تعداد الدم، CBC", "full blood count, FBC, CBC"),
            new("تحليل دم شائع يقيس خلايا الدم الحمراء والبيضاء والصفائح والهيموجلوبين.",
                "A common blood test that measures red cells, white cells, platelets and haemoglobin."),
            ["blood"],
            S(
                ("about", "من أكثر تحاليل الدم شيوعًا. يقيس عدد خلايا الدم الحمراء والبيضاء والصفائح الدموية، ونسبة الهيموجلوبين الذي ينقل الأكسجين.",
                    "One of the most common blood tests. It measures the number of red cells, white cells and platelets, and the haemoglobin that carries oxygen."),
                ("why", "- الاطمئنان على الصحة العامة\n- اكتشاف فقر الدم أو الالتهابات أو بعض اضطرابات الدم\n- متابعة بعض الحالات والعلاجات",
                    "- To check your general health\n- To find anaemia, infections or some blood disorders\n- To monitor some conditions and treatments"),
                ("prepare", "لا يحتاج عادة إلى تحضير خاص.", "You usually don't need to do anything to prepare."),
                ("during", "تُسحب عينة دم من وريد في الذراع خلال دقائق.", "A blood sample is taken from a vein in your arm, which takes a few minutes."),
                ("results", "تُقارن النتائج بالمعدلات الطبيعية. النتيجة خارج المعدل لا تعني دائمًا وجود مشكلة، وسيشرحها لك طبيبك. فمثلًا قد يدل انخفاض الهيموجلوبين على فقر الدم، وارتفاع خلايا الدم البيضاء على وجود التهاب.",
                    "Results are compared with normal ranges. A result outside the range doesn't always mean there is a problem, and your doctor will explain it. For example, low haemoglobin can mean anaemia, and a high white cell count can mean an infection."),
                ("risks", "المخاطر قليلة جدًا، وقد تظهر كدمة بسيطة.", "The risks are very small. You may get a slight bruise.")),
            Related: ["sickle-cell-disease", "g6pd-deficiency"],
            Sources: [Medline("تحليل صورة الدم الكاملة", "Complete blood count (CBC)", "https://medlineplus.gov/lab-tests/complete-blood-count-cbc/")]),

        new("mammogram", true,
            new("تصوير الثدي بالأشعة (الماموجرام)", "Mammogram"),
            new("الماموجرام، أشعة الثدي، فحص الكشف المبكر عن سرطان الثدي", "breast X-ray, breast screening, mammography"),
            new("تصوير الثدي بجرعة منخفضة من الأشعة السينية، يكشف التغيرات مبكرًا قبل أن يمكن لمسها.",
                "A low-dose X-ray of the breasts that can find changes early, before they can be felt."),
            ["cancer", "reproductive"],
            S(
                ("about", "صورة بالأشعة السينية بجرعة منخفضة للثدي، تساعد على اكتشاف السرطان في مراحله المبكرة عندما يكون صغيرًا لا يمكن لمسه.",
                    "A low-dose X-ray of the breast. It helps find cancer at an early stage, when it is too small to feel."),
                ("why", "- الكشف المبكر لدى النساء دون أعراض، ابتداءً من سن الأربعين (اسألي مركزك الصحي)\n- فحص كتلة أو تغير لاحظتِه في الثدي",
                    "- Screening for women without symptoms, from age 40 (ask your health centre)\n- Checking a lump or change you have noticed"),
                ("prepare", "- يُفضّل الحجز بعد الدورة الشهرية بأسبوع حين يكون الثدي أقل حساسية\n- لا تستخدمي مزيل العرق أو البودرة أو الكريم على الصدر والإبط يوم الفحص\n- ارتدي ملابس من قطعتين\n- أخبري الفريق إن كنتِ حاملًا أو مرضعًا أو لديكِ حشوات في الثدي",
                    "- Book for about a week after your period, when your breasts are less tender\n- Don't use deodorant, powder or cream on your chest and underarms on the day\n- Wear a two-piece outfit\n- Tell the staff if you are pregnant, breastfeeding or have breast implants"),
                ("during", "تضع أخصائية الأشعة كل ثدي بين لوحين وتضغط عليه لثوانٍ للحصول على صورة واضحة. قد يكون الضغط مزعجًا لكنه قصير، ويستغرق الموعد كاملًا نحو 20 إلى 30 دقيقة.",
                    "A female radiographer places each breast between two plates and presses it for a few seconds to get a clear image. The pressure can be uncomfortable but is brief. The whole appointment takes about 20 to 30 minutes."),
                ("results", "يقرأ الصور طبيب أشعة. قد يُطلب منكِ العودة لصور إضافية، وأغلب حالات الاستدعاء لا تكون سرطانًا.",
                    "A radiologist reads the images. You may be asked to come back for more images; most women who are called back don't have cancer."),
                ("risks", "- جرعة إشعاع منخفضة جدًا\n- قد يظهر تغير يحتاج إلى فحوصات إضافية ثم يتبين أنه سليم",
                    "- A very low dose of radiation\n- A change may show that needs more tests and turns out to be harmless")),
            Specialties: ["Oncology"], Related: ["breast-cancer"],
            Sources: [Medline("تصوير الثدي بالأشعة", "Mammography", "https://medlineplus.gov/mammography.html"), Nhs("الكشف المبكر عن سرطان الثدي", "Breast screening (mammogram)", "https://www.nhs.uk/conditions/breast-screening-mammogram/")]),

        new("premarital-screening", true,
            new("فحص ما قبل الزواج", "Premarital screening"),
            new("فحص الزواج الصحي، الفحص الطبي قبل الزواج", "premarital test, pre-marriage screening, healthy marriage programme"),
            new("برنامج وطني يفحص الخاطبين قبل الزواج للكشف عن أمراض الدم الوراثية الشائعة وبعض الأمراض المعدية.",
                "A national programme that tests couples before marriage for common inherited blood disorders and some infections."),
            ["blood", "infections", "reproductive"],
            S(
                ("about", "ضمن برنامج الزواج الصحي التابع لوزارة الصحة، يُفحص الخاطبان للكشف عن أمراض الدم الوراثية (فقر الدم المنجلي والثلاسيميا) وبعض الأمراض المعدية (التهاب الكبد الوبائي ب و ج، وفيروس نقص المناعة).",
                    "As part of the Ministry of Health's Healthy Marriage Programme, couples are tested for inherited blood disorders (sickle cell disease and thalassaemia) and some infections (hepatitis B, hepatitis C and HIV)."),
                ("why", "- معرفة احتمال إنجاب أطفال مصابين بأمراض الدم الوراثية\n- اكتشاف الأمراض المعدية مبكرًا لعلاجها وحماية الشريك\nالنتائج سرية، وقرار إتمام الزواج يعود للخاطبين.",
                    "- To know the chance of having children with an inherited blood disorder\n- To find infections early so they can be treated and the partner protected\nResults are confidential, and the decision to marry stays with the couple."),
                ("prepare", "يُنصح بإجراء الفحص قبل موعد الزواج بثلاثة أشهر على الأقل، لأن الشهادة صالحة لمدة محدودة. لا يحتاج إلى صيام.",
                    "Have the test at least 3 months before the wedding, as the certificate is valid for a limited time. You don't need to fast."),
                ("during", "تُسحب عينة دم من كل من الخاطبين في مركز صحي معتمد.", "A blood sample is taken from each partner at an approved health centre."),
                ("results", "يحصل الخاطبان على شهادة الفحص. وإذا تبيّن احتمال لإنجاب أطفال مصابين أو اكتُشف مرض معدٍ، تُقدَّم لهما الاستشارة الوراثية أو العلاج المناسب.",
                    "The couple receive a screening certificate. If there is a chance of having affected children, or an infection is found, they are offered genetic counselling or treatment."),
                ("risks", "المخاطر قليلة جدًا، وقد تظهر كدمة بسيطة.", "The risks are very small. You may get a slight bruise.")),
            Related: ["sickle-cell-disease"],
            Sources: [new(new("وزارة الصحة: فحص ما قبل الزواج", "Ministry of Health: Premarital screening"), "https://www.moh.gov.sa/en/HealthAwareness/Beforemarriage/Pages/default.aspx", "https://www.moh.gov.sa/HealthAwareness/Beforemarriage/Pages/default.aspx")]),

        new("spirometry", true,
            new("قياس وظائف التنفس", "Spirometry"),
            new("فحص وظائف الرئة، اختبار التنفس", "lung function test, breathing test"),
            new("فحص بسيط يقيس كمية الهواء التي تخرجها من رئتيك وسرعة إخراجها.",
                "A simple test that measures how much air you can breathe out and how fast."),
            ["lungs"],
            S(
                ("about", "يقيس الجهاز كمية الهواء التي تستطيع إخراجها بعد أخذ نفس عميق، وسرعة إخراجها.",
                    "A machine measures how much air you can blow out after a deep breath, and how quickly."),
                ("why", "- تشخيص الربو وأمراض الرئة الأخرى\n- متابعة الحالة ومدى الاستجابة للعلاج",
                    "- To diagnose asthma and other lung conditions\n- To check the condition and how well treatment is working"),
                ("prepare", "- تجنّب التدخين والوجبات الثقيلة والمجهود الشديد قبل الفحص بساعات\n- ارتدِ ملابس مريحة\n- قد يُطلب منك التوقف عن بعض البخاخات قبل الفحص، فاتبع التعليمات",
                    "- Avoid smoking, heavy meals and hard exercise in the hours before the test\n- Wear loose, comfortable clothes\n- You may be asked to stop some inhalers beforehand: follow the instructions you are given"),
                ("during", "يوضع مشبك على أنفك، ثم تأخذ نفسًا عميقًا وتنفخ بأقوى وأسرع ما يمكن في أنبوب. يُكرر ذلك عدة مرات، وقد يُعاد بعد استخدام بخاخ موسّع للشعب.",
                    "A clip is placed on your nose. You take a deep breath and blow as hard and fast as you can into a mouthpiece. This is repeated a few times, and may be repeated after using a reliever inhaler."),
                ("results", "تُقارن النتائج بالقيم المتوقعة لعمرك وطولك وجنسك، وسيشرحها لك الطبيب.",
                    "The results are compared with the values expected for your age, height and sex, and your doctor will explain them."),
                ("risks", "قد تشعر بدوخة أو تعب بسيط لفترة قصيرة.", "You may feel dizzy or a little tired for a short time.")),
            Related: ["asthma"],
            Sources: [Nhs("قياس وظائف التنفس", "Spirometry", "https://www.nhs.uk/conditions/spirometry/")]),

        new("ecg", true,
            new("تخطيط القلب الكهربائي", "Electrocardiogram (ECG)"),
            new("تخطيط القلب، رسم القلب، EKG", "ECG, EKG, heart tracing"),
            new("فحص سريع وغير مؤلم يسجل النشاط الكهربائي للقلب.",
                "A quick, painless test that records the heart's electrical activity."),
            ["heart"],
            S(
                ("about", "يسجل التخطيط الإشارات الكهربائية التي تنظم ضربات القلب، ويظهر سرعتها وانتظامها.",
                    "An ECG records the electrical signals that control your heartbeat, showing how fast and how regularly it beats."),
                ("why", "- ألم الصدر أو الخفقان أو الدوخة أو ضيق التنفس\n- متابعة أثر ارتفاع ضغط الدم على القلب\n- قبل بعض العمليات الجراحية",
                    "- Chest pain, palpitations, dizziness or shortness of breath\n- Checking the effect of high blood pressure on the heart\n- Before some operations"),
                ("prepare", "لا يحتاج إلى تحضير خاص. ارتدِ ملابس يسهل خلع الجزء العلوي منها، وتجنّب الكريمات على الصدر.",
                    "You don't need to prepare. Wear clothes that make it easy to uncover your chest, and avoid creams on your chest."),
                ("during", "توضع لصقات صغيرة على الصدر والذراعين والساقين وتوصل بالجهاز، وتبقى مستلقيًا دون حركة لدقائق. يستغرق الفحص نحو 5 إلى 10 دقائق.",
                    "Small sticky pads are placed on your chest, arms and legs and connected to the machine, and you lie still for a few minutes. The test takes about 5 to 10 minutes."),
                ("results", "يقرأ الطبيب التخطيط، وقد يطلب فحوصات أخرى مثل تخطيط صدى القلب أو اختبار الجهد.",
                    "A doctor reads the ECG and may ask for other tests, such as an echocardiogram or an exercise test."),
                ("risks", "لا توجد مخاطر، وقد تسبب اللصقات تهيجًا بسيطًا في الجلد.", "There are no risks. The sticky pads may slightly irritate the skin.")),
            Specialties: ["Cardiology"], Facilities: ["Saud Al Babtain Cardiac Center"], Related: ["high-blood-pressure"],
            Sources: [Nhs("تخطيط القلب الكهربائي", "Electrocardiogram (ECG)", "https://www.nhs.uk/conditions/electrocardiogram/")]),
    ];
}
