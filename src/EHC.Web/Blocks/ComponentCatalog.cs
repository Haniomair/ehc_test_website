using System.Globalization;

namespace EHC.Web.Blocks;

/// <summary>
/// Editor-facing catalogue of every page section: category, icon and a short bilingual name + description.
/// Used by the "Component library" page. Add an entry whenever a new block element type is created.
/// </summary>
public static class ComponentCatalog
{
    public sealed record Category(string Key, string Ar, string En, string Icon)
    {
        public string Name(CultureInfo c) => c.TwoLetterISOLanguageName == "ar" ? Ar : En;
    }

    public sealed record Component(string Alias, string CategoryKey, string Icon, string NameAr, string NameEn, string DescriptionAr, string DescriptionEn)
    {
        public string Name(CultureInfo c) => c.TwoLetterISOLanguageName == "ar" ? NameAr : NameEn;
        public string Description(CultureInfo c) => c.TwoLetterISOLanguageName == "ar" ? DescriptionAr : DescriptionEn;
    }

    public static readonly IReadOnlyList<Category> Categories =
    [
        new("heroes", "الواجهات الرئيسية", "Heroes", "star"),
        new("services", "الخدمات والتنقل", "Services & navigation", "compass"),
        new("text", "النصوص والتخطيط", "Text & layout", "doc"),
        new("features", "المزايا والمراحل", "Features & steps", "grid"),
        new("numbers", "الأرقام والمؤشرات", "Numbers & indicators", "target"),
        new("media", "الصور والفيديو", "Images & video", "play"),
        new("people", "الأشخاص والثقة", "People & trust", "users"),
        new("data", "البيانات والأدلة", "Live data & directories", "locate"),
        new("cta", "الدعوات إلى إجراء", "Calls to action", "arrow"),
    ];

    public static readonly IReadOnlyList<Component> All =
    [
        // heroes
        new("heroCampaignBlock", "heroes", "star", "واجهة الحملات", "Campaign hero", "شرائح حملات متحركة مع مؤقت وإيقاف مؤقت.", "Rotating campaign slides with progress and pause."),
        new("heroImageBlock", "heroes", "star", "واجهة بصورة", "Image hero", "صورة كاملة العرض بطبقة لونية من الثيم.", "Full-width photo with a theme-coloured overlay."),
        new("heroVideoBlock", "heroes", "play", "واجهة بفيديو", "Video hero", "فيديو خلفية صامت مع زر إيقاف.", "Muted background video with a pause button."),
        new("heroSplitBlock", "heroes", "grid", "واجهة مقسومة", "Split hero", "نص بجانب صورة أو بطاقات عائمة.", "Text beside a framed image or floating cards."),
        new("heroSearchBlock", "heroes", "search", "واجهة البحث", "Search hero", "بحث كبير مع تبويبات وكلمات شائعة.", "Large search with scopes and popular terms."),
        // services & navigation
        new("careNavigatorBlock", "services", "compass", "دليل الرعاية", "Care navigator", "من أنت؟ ماذا تحتاج؟ ثم وجهة مقترحة.", "Who are you, what do you need, then a suggested route."),
        new("quickActionsBlock", "services", "grid", "إجراءات سريعة", "Quick actions", "شريط أيقونات لأكثر الخدمات طلبًا.", "Icon strip for the most-used services."),
        new("linkCardsBlock", "services", "app", "بطاقات الروابط", "Link cards", "خدمات إلكترونية وروابط بشارات.", "E-services and links with badges."),
        new("journeysBlock", "services", "users", "رحلات المستفيد", "Patient journeys", "مراحل الحياة وخطوات الرعاية.", "Life stages and care steps."),
        new("careModelBlock", "services", "heart", "نموذج الرعاية الصحية السعودي", "Saudi Model of Care", "أنظمة الرعاية حول المستفيد، مع لوحة لكل نظام.", "The systems of care around the person, with a panel for each."),
        new("healthToolsBlock", "services", "scal", "أدوات صحية", "Health tools", "نظرة عامة على الأدوات الصحية حسب الفئة، مع حاسبة كتلة الجسم اختياريًا.", "Overview of the site's health tools by category, optionally with the BMI calculator."),
        new("healthToolBlock", "services", "scal", "أداة صحية", "Health tool", "إحدى الأدوات الثماني: كتلة الجسم، الوزن المثالي، السعرات، الإباضة، موعد الولادة، النظر، السكري، الربو.", "One of eight tools: BMI, ideal weight, calories, ovulation, due date, vision, prediabetes, asthma."),
        new("azIndexBlock", "services", "menu", "فهرس أبجدي", "A–Z index", "التخصصات أو المنشآت أو الأدوات أو المقالات مرتبة أبجديًا مع بحث.", "Specialties, facilities, tools or articles by letter, with a filter."),
        new("eServicesBlock", "services", "globe", "الخدمات الإلكترونية", "E-services", "خدمات إلكترونية مع تبويب لكل فئة (المرضى أولاً)، وشريط اختياري لتطبيق الجوال.", "Online services with a tab per audience (patients first), and an optional mobile app strip."),
        // text & layout
        new("sectionHeadingBlock", "text", "doc", "عنوان قسم", "Section heading", "عنوان وتمهيد وأزرار.", "Heading, intro and buttons."),
        new("richTextBlock", "text", "doc", "نص منسق", "Rich text", "محتوى نصي حر بتنسيق محدود.", "Free text with safe formatting."),
        new("mediaTextBlock", "text", "note", "صورة ونص", "Image + text", "صورة بأنماط مع نقاط وشارة عائمة.", "Styled image with bullets and a floating badge."),
        new("columnsBlock", "text", "grid", "أعمدة نصية", "Text columns", "حتى ثلاثة أعمدة بأيقونات (الرؤية، الرسالة…).", "Up to three columns with icons (vision, mission…)."),
        new("columnsLayoutBlock", "text", "grid", "تخطيط أعمدة", "Columns layout", "عمودان أو ثلاثة، ولكل عمود أقسامه الخاصة (نص، صورة، فيديو…).", "Two or three columns, each with its own sections (text, image, video…)."),
        new("checklistBlock", "text", "shield", "قائمة تحقق", "Checklist", "قائمة بعلامات صح أو أرقام.", "List with ticks, dots or numbers."),
        new("calloutBlock", "text", "bulb", "تنبيه", "Callout", "ملاحظة بخمس نغمات منها الطوارئ.", "Notice in five tones, including emergency."),
        new("quoteBlock", "text", "chat", "اقتباس", "Pull quote", "اقتباس بارز مع الاسم والمنصب.", "Large quote with name and role."),
        new("dividerBlock", "text", "menu", "فاصل", "Divider", "مسافة أو خط أو زخرفة أو موجة.", "Space, line, motif or wave."),
        new("tabsBlock", "text", "grid", "تبويبات", "Tabs", "محتوى في تبويبات بثلاثة أنماط.", "Tabbed content in three styles."),
        new("accordionBlock", "text", "menu", "أسئلة شائعة", "FAQ / accordion", "أسئلة قابلة للطي مع بيانات لمحركات البحث.", "Collapsible questions with search-engine data."),
        new("tableBlock", "text", "grid", "جدول", "Table", "جدول من نص أو من Excel.", "Table typed as text or pasted from Excel."),
        new("introSplitBlock", "text", "doc", "مقدمة مقسومة", "Intro split", "عنوان وسطر فرعي بجانب النص والأزرار.", "Heading and subheading beside text and buttons."),
        new("shareBarBlock", "text", "globe", "مشاركة وطباعة", "Share & print", "روابط مشاركة بدون متتبعات، نسخ الرابط والطباعة.", "Tracker-free share links, copy link and print."),
        new("pageNavBlock", "text", "menu", "محتويات الصفحة", "Page contents bar", "قائمة ثابتة تُبنى تلقائيًا من عناوين الأقسام.", "Sticky menu built automatically from section headings."),
        // features & steps
        new("featureGridBlock", "features", "grid", "شبكة مزايا", "Feature grid", "بطاقات بأربعة أنماط وأعمدة 2–4.", "Cards in four styles, 2–4 columns."),
        new("stepsBlock", "features", "arrow", "خطوات", "Process steps", "خطوات أفقية أو عمودية.", "Horizontal or vertical steps."),
        new("timelineBlock", "features", "clock", "خط زمني", "Timeline", "محطات بتواريخ وصور.", "Milestones with dates and images."),
        new("packagesBlock", "features", "heart", "باقات", "Packages", "باقات فحص بمزايا وسعر وزر.", "Check-up packages with features, price and button."),
        new("excellenceRailBlock", "features", "award", "مراكز التميز", "Centers of excellence", "شريط أفقي من البطاقات.", "Horizontal rail of cards."),
        new("introPanelBlock", "features", "hosp", "لوحة تعريفية", "Intro panel", "صورة أو فيديو مع بطاقة متداخلة ونقاط وأرقام.", "Photo or video with an overlapping card, points and numbers."),
        new("flipCardsBlock", "features", "grid", "بطاقات قلابة", "Flip cards", "أيقونة وعنوان، وتنقلب لعرض التفاصيل.", "Icon and title that turn over to show details."),
        new("splitPanelsBlock", "features", "grid", "لوحات مقسومة", "Split panels", "صفوف بعرض كامل: صورة ولوحة ملونة بالتناوب.", "Full-width rows: photo and colour panel, alternating."),
        new("imageCardsBlock", "features", "grid", "بطاقات مصورة", "Image cards", "بطاقات بصور ووسم ورابط.", "Photo cards with tag and link."),
        new("hotspotsBlock", "features", "pin", "نقاط على صورة", "Image hotspots", "نقاط مرقمة على صورة مرتبطة بقائمة.", "Numbered points on an image linked to a list."),
        // numbers
        new("statsBandBlock", "numbers", "target", "شريط أرقام", "Stats band", "أرقام تعدّ عند الظهور.", "Numbers that count up on scroll."),
        new("statementStatsBlock", "numbers", "flag", "بيان وأرقام", "Statement + stats", "رسالة رئيسية مع أرقام، وصف اختياري لشعارات الاعتماد.", "Key message with figures, and an optional row of accreditation logos."),
        new("progressBarsBlock", "numbers", "target", "مؤشرات", "Progress / indicators", "أشرطة أو حلقات نسب مئوية.", "Percentage bars or rings."),
        new("countdownBlock", "numbers", "clock", "عدّ تنازلي", "Countdown", "عدّ حتى موعد بتوقيت الرياض.", "Counts down to a Riyadh date and time."),
        // media
        new("galleryBlock", "media", "grid", "معرض صور", "Image gallery", "شبكة أو بناء حجري مع عارض.", "Grid or masonry with a viewer."),
        new("imageBlock", "media", "grid", "صورة", "Image", "صورة واحدة بتعليق ورابط، عريضة أو ضيقة.", "Single image with caption and link, wide or narrow."),
        new("videoBlock", "media", "play", "فيديو", "Video", "يوتيوب عند الضغط أو ملف فيديو.", "YouTube on click, or a video file."),
        new("beforeAfterBlock", "media", "eye", "قبل وبعد", "Before / after", "مقارنة صورتين بفاصل قابل للسحب.", "Compare two images with a draggable divider."),
        new("storiesBlock", "media", "video", "قصص", "Stories", "فيديو واقتباسات من المستفيدين.", "Video and patient quotes."),
        new("marqueeBlock", "media", "menu", "شريط متحرك", "Ticker", "كلمات متحركة للتخصصات.", "Scrolling specialty words."),
        new("embedBlock", "media", "app", "تضمين نموذج أو تقرير", "Embed (form / report)", "Microsoft Forms وPower BI ونماذج Frappe، تُعرض عند الطلب.", "Microsoft Forms, Power BI and Frappe web forms, loaded on request."),
        // people & trust
        new("teamBlock", "people", "users", "فريق العمل", "Team / leadership", "بطاقات أو صور دائرية.", "Cards or circle portraits."),
        new("testimonialsBlock", "people", "chat", "آراء المستفيدين", "Testimonials", "دوّار أو شبكة مع تقييم.", "Carousel or grid with ratings."),
        new("logoWallBlock", "people", "award", "شعارات الشركاء", "Logo wall", "شبكة أو شريط متحرك.", "Grid or scrolling strip."),
        new("accreditationsBlock", "people", "award", "الاعتمادات", "Accreditations", "شارات الاعتماد والجوائز.", "Accreditation badges and awards."),
        new("doctorsBlock", "people", "users", "الأطباء", "Doctors", "أطباء من دليل الأطباء.", "Doctors from the directory."),
        new("leaderMessageBlock", "people", "chat", "كلمة القيادة", "Leader message", "صورة واقتباس واسم ومنصب وتوقيع.", "Portrait, quote, name, role and signature."),
        // live data & directories
        new("erWaitBlock", "data", "er", "انتظار الطوارئ", "ER wait times", "أوقات الانتظار من الخادم.", "Wait times from the server feed."),
        new("facilityFinderBlock", "data", "locate", "دليل المنشآت", "Facility finder", "خريطة وفلترة للمنشآت، مع اختيار الشبكة الصحية وأوقات انتظار الطوارئ (اختياري).", "Map and filters for facilities, with optional health-network chips and ER wait times."),
        new("networksBlock", "data", "hosp", "الشبكات الصحية", "Health networks", "بطاقة لكل شبكة مع عدد المستشفيات والمراكز.", "A card per network with its hospital and centre counts."),
        new("visitorGuideBlock", "data", "clock", "دليل الزوار", "Visitor guide", "أوقات الزيارة والتعليمات وما يجب إحضاره.", "Visiting hours, guidelines and what to bring."),
        new("healthLibraryBlock", "data", "book", "المكتبة الصحية", "Health library", "أحدث المقالات التثقيفية، لموضوع واحد أو للجميع.", "Latest patient-education articles, one topic or all."),
        new("contactBlock", "data", "phone", "بيانات التواصل", "Contact details", "عنوان وهواتف وبريد وخريطة.", "Address, phones, email and map."),
        new("contactFormBlock", "data", "mail", "نموذج التواصل", "Contact form", "شكاوى واستفسارات واقتراحات برقم مرجعي، تُقرأ في قسم الرسائل.", "Complaints, questions and suggestions with a reference number, read in the Messages section."),
        new("openingHoursBlock", "data", "clock", "أوقات العمل", "Opening hours", "أيام وساعات مع شريط الطوارئ.", "Days and hours with emergency strip."),
        new("timetableBlock", "data", "cal", "جدول مواعيد", "Timetable", "مواعيد مجمعة حسب اليوم مع فلتر.", "Times grouped by day with a filter."),
        new("eventsBlock", "data", "cal", "فعاليات", "Events", "تواريخ ميلادية وهجرية.", "Gregorian and Hijri dates."),
        new("newsBlock", "data", "news", "الأخبار", "News", "أحدث الأخبار تلقائيًا.", "Latest news, automatically."),
        new("campaignsBlock", "data", "flag", "الحملات", "Campaigns", "حملات حالية وتقويم.", "Current campaigns and calendar."),
        new("downloadsBlock", "data", "doc", "ملفات للتنزيل", "Downloads", "النوع والحجم من الملف.", "Type and size read from the file."),
        new("emergencyNumbersBlock", "data", "er", "أرقام الطوارئ", "Emergency numbers", "أرقام بالضغط للاتصال بلون الطوارئ الثابت.", "Tap-to-call numbers in the fixed emergency style."),
        new("contactStripBlock", "data", "phone", "شريط التواصل", "Contact strip", "شريط مختصر لبيانات التواصل.", "Compact band of contact details."),
        // calls to action
        new("ctaBannerBlock", "cta", "arrow", "لافتة بصورة", "Image CTA banner", "لافتة بصورة وزرين.", "Photo banner with two buttons."),
        new("ctaBandBlock", "cta", "arrow", "شريط دعوة", "CTA band", "شريط ملون بدعوة وزر.", "Coloured band with a call to action."),
        new("appPromoBlock", "cta", "app", "ترويج التطبيق", "App promo", "روابط المتاجر ومزايا التطبيق.", "Store links and app features."),
        new("careersBlock", "cta", "brief", "الوظائف", "Careers", "بطاقات للوظائف والتدريب.", "Cards for jobs and training."),
        new("announcementBlock", "cta", "note", "إعلان", "Announcement", "شريط إعلان قصير.", "Short announcement strip."),
        new("actionTilesBlock", "cta", "arrow", "بلاطات الإجراءات", "Action tiles", "بلاطات أيقونات كبيرة فوق صورة.", "Large icon tiles over a photo."),
    ];

    private static readonly Dictionary<string, Component> ByAlias = All.ToDictionary(c => c.Alias, StringComparer.OrdinalIgnoreCase);

    public static Component? Find(string alias) => ByAlias.GetValueOrDefault(alias);

    public static Category CategoryOf(Component c) => Categories.First(x => x.Key == c.CategoryKey);
}
