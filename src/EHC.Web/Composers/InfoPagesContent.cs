using static EHC.Web.Composers.BlockField;

namespace EHC.Web.Composers;

/// <summary>
/// Sections of the information pages created by InfoPagesSeeder. Texts come from the same pages on ehc.med.sa
/// (October 2026), in their Arabic and English wording, except:
/// - privacy policy: the old page was the staff intranet policy, so this is a draft that describes what this website
///   actually collects (retention periods from configuration), marked for review, with ## where EHC must fill in;
/// - patient &amp; visitor guide: the old "Learn more" links went to "coming soon" and three boxes held template filler;
///   the cards now link to pages of this site and the filler is left out;
/// - academic affairs: the old counters showed 0, so the figures are ## placeholders.
/// </summary>
internal sealed class InfoPagesContent(InfoPagesContent.Pages pages, Func<string, Guid> typeKey, InfoPagesContent.Retention retention)
{
    public sealed record Pages(Guid Privacy, Guid Terms, Guid Guide, Guid Projects, Guid Researchers, Guid Academic, Guid Rights, Guid? Contact, Guid? Facilities, Guid? EServices);

    public sealed record Retention(int ContactMonths, int FeedbackMonths, int StatsDays, int VitalsDays);

    private BlockJson New() => new(typeKey);

    private (SeedLink Ar, SeedLink En)? Contact(string ar = "تواصل معنا", string en = "Contact us") =>
        pages.Contact is { } c ? (new SeedLink(ar, Page: c), new SeedLink(en, Page: c)) : null;

    private BlockJson ContactBand(BlockJson list, string titleAr, string titleEn, string textAr, string textEn)
    {
        if (Contact() is not { } link) return list;
        return list.Add("ctaBandBlock", Text("title", titleAr, titleEn), Text("text", textAr, textEn), Link("primaryCta", link.Ar, link.En));
    }

    public string Blocks(string id) => id switch
    {
        "privacy" => Privacy(),
        "terms" => Terms(),
        "guide" => Guide(),
        "projects" => Projects(),
        "researchers" => Researchers(),
        "academic" => Academic(),
        "rights" => Rights(),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private string Privacy()
    {
        var r = retention;
        var ar = $"""
            <h2>من نحن</h2>
            <p>يدير تجمع الشرقية الصحي هذا الموقع. توضح هذه السياسة البيانات التي نجمعها عند استخدامك للموقع، وسبب جمعها، ومدة الاحتفاظ بها، وحقوقك وفق نظام حماية البيانات الشخصية في المملكة العربية السعودية.</p>
            <h2>عند تصفحك للموقع</h2>
            <p>نُحصي زيارات الصفحات لتحسين الموقع، دون ملفات تعريف ارتباط ودون حفظ عنوان IP الخاص بك. نسجّل الصفحة واللغة والموقع الذي أتيت منه ونوع الجهاز، والدولة (والمنطقة والمدينة داخل المملكة فقط)، مع رمز زائر يتغيّر كل يوم ولا يمكن ربطه بك. تُحذف تفاصيل الزيارات بعد {r.StatsDays} يومًا ولا يبقى إلا المجاميع اليومية.</p>
            <h2>ملفات تعريف الارتباط الاختيارية</h2>
            <p>لا نستخدم ملفات تعريف الارتباط الاختيارية إلا إذا اخترت «السماح بالكل» في إشعار ملفات تعريف الارتباط. عندها نقيس:</p>
            <ul>
            <li>أماكن النقر ومدى التمرير في الصفحة، دون أي نص تكتبه ودون رمز زائر.</li>
            <li>سرعة تحميل الصفحات وأداءها، وتُحذف هذه القياسات بعد {r.VitalsDays} يومًا.</li>
            </ul>
            <p>يمكنك تغيير اختيارك في أي وقت من رابط إعدادات ملفات تعريف الارتباط أسفل الصفحة.</p>
            <h2>ما يُحفظ في متصفحك</h2>
            <p>يحفظ الموقع في متصفحك فقط: اختيارك لملفات تعريف الارتباط، وإعدادات العرض (مثل الوضع الداكن وحجم الخط والتباين)، وعمليات البحث الأخيرة، وأنك أجبت على سؤال «هل كانت هذه الصفحة مفيدة؟». لا تُرسل هذه البيانات إلينا، ويمكنك حذفها من إعدادات متصفحك.</p>
            <h2>موقعك الجغرافي</h2>
            <p>عند استخدام «موقعي» على الخريطة، يُستخدم موقعك داخل متصفحك فقط لإظهار أقرب المنشآت، ولا يُرسل إلينا ولا يُحفظ.</p>
            <h2>نموذج التواصل</h2>
            <p>عند إرسال رسالة نحفظ ما تكتبه: الاسم والبريد الإلكتروني ورقم الجوال وموضوع الرسالة ونصها، إضافة إلى لغة الصفحة التي أرسلتها منها. نستخدمها فقط للرد عليك ومتابعة رسالتك، ولا يطّلع عليها إلا الفريق المختص، وتُحذف بعد {r.ContactMonths} شهرًا.</p>
            <h2>تقييم الصفحات</h2>
            <p>إجابات «هل كانت هذه الصفحة مفيدة؟» مجهولة الهوية: لا نحفظ اسمًا أو بريدًا إلكترونيًا أو عنوان IP، ونحذف تلقائيًا أي أرقام أو عناوين بريد تُكتب في التعليق. تُحذف الإجابات بعد {r.FeedbackMonths} شهرًا.</p>
            <h2>المحتوى من جهات أخرى</h2>
            <p>قد تعرض بعض الصفحات نماذج أو تقارير من جهات أخرى (مثل Microsoft Forms أو Power BI)، ولا يُحمَّل هذا المحتوى إلا عندما تختار عرضه، وعندها تنطبق سياسة الخصوصية لتلك الجهة. كما تنقلك بعض الخدمات الإلكترونية إلى منصات وطنية مثل «صحتي» التي لها سياساتها الخاصة.</p>
            <h2>مشاركة البيانات</h2>
            <p>لا نبيع بياناتك ولا نستخدمها لأغراض تسويقية، ولا نشاركها إلا إذا طلبتها جهة مختصة وفق الأنظمة المعمول بها.</p>
            <h2>حماية البيانات</h2>
            <p>يعمل الموقع كاملًا عبر اتصال مشفّر (HTTPS)، ويقتصر الاطلاع على الرسائل على مستخدمين مصرّح لهم في لوحة الإدارة.</p>
            <h2>حقوقك</h2>
            <p>وفق نظام حماية البيانات الشخصية يحق لك:</p>
            <ul>
            <li>معرفة البيانات التي نحتفظ بها عنك والاطلاع عليها.</li>
            <li>طلب تصحيحها أو حذفها.</li>
            <li>سحب موافقتك على ملفات تعريف الارتباط الاختيارية في أي وقت.</li>
            <li>تقديم شكوى إلى الهيئة السعودية للبيانات والذكاء الاصطناعي (سدايا).</li>
            </ul>
            <p>لممارسة حقوقك تواصل مع مسؤول حماية البيانات: ##</p>
            <h2>تحديث هذه السياسة</h2>
            <p>قد نحدّث هذه السياسة عند تغيّر خدمات الموقع، وننشر النسخة الجديدة في هذه الصفحة. آخر تحديث: ##</p>
            """;
        var en = $"""
            <h2>Who we are</h2>
            <p>Eastern Health Cluster runs this website. This policy explains what data we collect when you use it, why, how long we keep it, and your rights under the Saudi Personal Data Protection Law (PDPL).</p>
            <h2>When you browse the website</h2>
            <p>We count page visits to improve the website, without cookies and without storing your IP address. We record the page, language, the site you came from and the type of device, the country (and the region and city inside Saudi Arabia only), with a visitor code that changes every day and cannot be linked to you. Visit details are deleted after {r.StatsDays} days; only daily totals remain.</p>
            <h2>Optional cookies</h2>
            <p>We use optional cookies only if you choose "Allow all" in the cookie notice. Then we measure:</p>
            <ul>
            <li>where people click and how far they scroll, without anything you type and without a visitor code;</li>
            <li>how quickly pages load and respond; these measurements are deleted after {r.VitalsDays} days.</li>
            </ul>
            <p>You can change your choice at any time with the cookie settings link at the bottom of the page.</p>
            <h2>What is stored in your browser</h2>
            <p>The website stores only these in your own browser: your cookie choice, display settings (such as dark mode, text size and contrast), your recent searches, and that you answered "Was this page helpful?". None of this is sent to us, and you can delete it in your browser settings.</p>
            <h2>Your location</h2>
            <p>When you use "My location" on the map, your position is used inside your browser only, to show the nearest facilities. It is not sent to us or stored.</p>
            <h2>Contact form</h2>
            <p>When you send a message we store what you type: your name, e-mail address, mobile number, the subject and your message, plus the language of the page you sent it from. We use it only to answer and follow up your message, only the team responsible can see it, and it is deleted after {r.ContactMonths} months.</p>
            <h2>Page feedback</h2>
            <p>Answers to "Was this page helpful?" are anonymous: we store no name, e-mail or IP address, and numbers or e-mail addresses typed in a comment are removed automatically. Answers are deleted after {r.FeedbackMonths} months.</p>
            <h2>Content from other providers</h2>
            <p>Some pages can show forms or reports from other providers (such as Microsoft Forms or Power BI). They load only when you choose to show them, and then that provider's privacy policy applies. Some e-services also take you to national platforms such as Sehhaty, which have their own policies.</p>
            <h2>Sharing data</h2>
            <p>We do not sell your data or use it for marketing, and we share it only when a competent authority requires it under the applicable regulations.</p>
            <h2>Keeping data safe</h2>
            <p>The whole website uses an encrypted connection (HTTPS), and only authorised users of the administration area can read messages.</p>
            <h2>Your rights</h2>
            <p>Under the Personal Data Protection Law you have the right to:</p>
            <ul>
            <li>know what data we hold about you and see it;</li>
            <li>ask us to correct or delete it;</li>
            <li>withdraw your consent to optional cookies at any time;</li>
            <li>complain to the Saudi Data and AI Authority (SDAIA).</li>
            </ul>
            <p>To use your rights, contact the data protection officer: ##</p>
            <h2>Changes to this policy</h2>
            <p>We may update this policy when the website's services change and will publish the new version on this page. Last updated: ##</p>
            """;
        return New()
            .Add("calloutBlock", Pick("tone", "warning"),
                Text("title", "مسودة بانتظار المراجعة", "Draft for review"),
                Text("text",
                    "تصف هذه الصفحة ما يجمعه هذا الموقع فعليًا، وتحتاج إلى مراجعة واعتماد مكتب حماية البيانات والإدارة القانونية في التجمع قبل الإطلاق. المواضع المشار إليها بـ ## تنتظر بياناتهم.",
                    "This page describes what this website actually collects. It needs review and approval by EHC's data protection office and legal department before launch; the places marked ## are waiting for their details."))
            .Add("pageNavBlock", Text("title", "في هذه الصفحة", "On this page"))
            .Add("richTextBlock", Rich("body", ar, en))
            .Build();
    }

    private string Terms()
    {
        const string ar = """
            <p>توضح هذه الوثيقة الشروط والأحكام اللازمة لاستخدام موقع تجمع الشرقية الصحي علماً أن استخدام موقع التجمع يُعد قبولاً ضمنياً على هذه الشروط والأحكام، ويحتفظ تجمع الشرقية الصحي بحق اتخاذ جميع الإجراءات القانونية عند حصول أي انتهاك لأي من أحكامها، بما في ذلك أي انتهاك لحقوق الملكية الفكرية.</p>
            <h2>قيود الاستخدام</h2>
            <p>باستخدامك لموقع التجمع، تقر بالامتناع عن الآتي:</p>
            <ul>
            <li>رفع أو تحميل ملفات تحتوي على برمجيات أو مواد أو بيانات أو معلومات أخرى ليست مملوكة لك أو لا تملك ترخيصاً بشأنها.</li>
            <li>رفع أو تحميل ملفات تحتوي على فيروسات أو بيانات تالفة أو أي برمجيات خبيثة، أو القيام بكل ما من شأنه التأثير في سلامة المعلومات في موقع التجمع أو موثوقيتها أو استمرار توفرها.</li>
            <li>نشر أو إعلان أو توزيع أو تعميم مواد أو معلومات تحتوي تشويهاً للسمعة أو انتهاكاً لأي مواد أو معلومات غير قانونية من خلال خدمات موقع التجمع.</li>
            <li>استخدام أي وسيلة أو برنامج أو إجراء لاعتراض أو محاولة اعتراض التشغيل الصحيح لموقع التجمع.</li>
            <li>القيام بأي إجراء يفرض تحميلاً غير معقول أو يكون حجم التحميل كبيراً أو يستخدم بصورة غير مناسبة على البنية التحتية لموقع التجمع.</li>
            <li>كل ما يعد مخالفة لنظام مكافحة الجرائم المعلوماتية أو الأنظمة ذوات العلاقة المعمول بها في المملكة العربية السعودية.</li>
            <li>إنشاء أي روابط إلكترونية خاصة بموقع التجمع أو عرضها أو وضعها في أي موقع، ما عدا وضع الروابط الخاصة بالخدمات في مواقع لا تتعارض في أهدافها وتوجهها العام مع أهداف وسياسات وأطر عمل الخدمات الإلكترونية التابعة للتجمع.</li>
            </ul>
            <h2>أحكام عامة</h2>
            <p>لا يتحمل التجمع أي نفقات أو خسائر أو تكاليف أو تعويضات أو مطالبات ناجمة بأي شكل من الأشكال عامةً كانت أو خاصةً أو حتى عرضية أو تسببت بأي عمل يتعلق باستخدام موقع التجمع أو المعلومات الموجودة فيه.</p>
            <ul>
            <li>لا يتحمل التجمع مسؤولية أي تبعات ناتجة عن خطأ أو عدم تحديث للمعلومات في موقع التجمع.</li>
            <li>يحتفظ التجمع بحق إضافة أو تغيير أي شرط من شروط الاستخدام، على أن يتم إشعار المستخدم بذلك.</li>
            <li>يحق للتجمع إيقاف الموقع مؤقتاً عند الحاجة إلى صيانة أو تطوير أو تعديل، وذلك حتى انتهاء العمل اللازم لذلك.</li>
            <li>يحتفظ التجمع بكامل حقوقه في إيقاف وتعطيل أي ارتباط بأي شكل من الأشكال من أي موقع غير مصرح به أو يحتوي على مواضيع غير ملائمة أو فاضحة أو متعدية أو بذيئة أو إباحية أو غير مقبولة أو غير قانونية، أو يحتوي على أسماء أو مواد أو معلومات تخالف أي نظام أو تنتهك أي حقوق عامة أو خاصة.</li>
            <li>لا يتحمل التجمع أي مسؤولية عن المحتويات المتوفرة في أي موقع آخر يتم الوصول إليه عبر هذا الموقع أو الوصول من خلال خدمات التجمع.</li>
            </ul>
            <h2>حجب المستخدم</h2>
            <p>يجوز لتجمع وحسب تقديرها المطلق إنهاء أو تقييد أو إيقاف حق المستخدم في الدخول إلى الموقع دون إشعار ولأي سبب بما في ذلك مخالفة شروط وبنود الاستخدام أو أي سلوك آخر قد يعتبرة التجمع حسب تقديرها غير قانوني أو يكون مضراً بالآخرين، وفي حال الإنهاء فإنه لن يصرح للمستخدم الدخول إلى الموقع.</p>
            <h2>حقوق الملكية</h2>
            <p>يلتزم مستخدم موقع التجمع بالحفاظ على حقوق الملكية الفكرية لموقع التجمع التي تتضمن على سبيل المثال لا الحصر ما يلي: جميع محتويات الموقع من خدمات ومعلومات تعد محمية بالكامل طبقاً لأنظمة المملكة العربية السعودية، ولا يجوز للمستخدم بيع أو ترخيص أو تأجير أو تعديل أو نسخ أو استنساخ أو تحميل أو إعلان أو نقل أو توزيع أو تحرير أو إنشاء أعمال مشتقة من أي مواد أو محتويات من موقع التجمع للجمهور أو لأغراض تجارية، دون الحصول على الموافقة الخطية المسبقة من الجهة المشرفة. ويمنع إجراء أي تعديل في محتويات موقع التجمع، كما أن الرسوم والصور في موقع التجمع محمية بموجب الأنظمة، ولا يجوز استنساخها أو استغلالها بأي طريقة كانت، دون موافقة خطية مسبقة من تجمع الشرقية الصحي.</p>
            <h2>استخدام الموقع وإخلاء المسؤولية</h2>
            <p>تجمع الشرقية الصحي غير مسؤول تحت أي ظرف من الظروف عن أي أضرار مباشرة أو غير مباشرة أو عرضية أو تبعية أو خاصة أو استثنائية تنشأ عن استخدام أو عدم القدرة على استخدام الموقع الإلكتروني الخاص بها.</p>
            <p>تعد جميع المعلومات التي تتضمنها صفحات (التجمع) على موقعها الإلكتروني معلومات عامة وإرشادية فقط ولا تقدم التجمع أي إقرارات أو ضمانات سواء بشكل صريح أو ضمني حول اكتمال أو دقة أو موثوقية أو ملاءمة أو توفر هذه البيانات أو المعلومات أو المواد ذوات الصلة الواردة في الموقع لأي غرض كان ولا يجوز استخدامها لغرض آخر غير الاستخدام العام.</p>
            <h2>أمان الموقع والأنظمة التابعة له</h2>
            <p>ويفرض موقع التجمع درجة عالية من الأمان على جميع تجهيزات وخوادم الموقع، حيث تم تجهيز أحدث أجهزة الحماية، كما تبذل التجمع كافة الجهود لفحص واختبار محتويات الموقع، وعلى المستخدم الالتزام بتشغيل برامج مضادة للفيروسات في جميع المواد التي يقوم بتنزيلها من الموقع.</p>
            <h2>تقديم الشكوى</h2>
            <p>في حال وجود شكاوى أو استفسارات يتم التواصل مع: خدمة العملاء على البريد الإلكتروني <a href="mailto:EHC@moh.gov.sa">EHC@moh.gov.sa</a></p>
            <h2>المرجعية القضائية</h2>
            <p>يخضع المستخدم لجميع الأنظمة واللوائح المعمول بها في المملكة العربية السعودية.</p>
            <h2>إدارة الموقع</h2>
            <p>اسم الجهة المشرفة على الموقع: الصحة الرقمية بتجمع الشرقية الصحي.</p>
            """;
        const string en = """
            <p>This document describes the terms and conditions required for using EHC Website taking into account that using the cluster's Website is an implicit acceptance of these terms and conditions. EHC reserves the right to take all legal measures in the event of any violation of any of its provisions, including any violation of intellectual property rights.</p>
            <h2>Usage restrictions</h2>
            <p>By using the cluster's Website, you agree to refrain from the following:</p>
            <ul>
            <li>Uploading or downloading files containing software, materials, data, or other information you do not own or do not have a license to.</li>
            <li>Uploading or downloading files that contain viruses, corrupted data, or any malicious software, or doing anything that may affect the security, reliability, or continued availability of information on the cluster's Website.</li>
            <li>Publishing, advertising, distributing, or circulating materials or information that contain defamation or violation of any illegal materials or information through the services of the cluster's Website.</li>
            <li>Using any means, program, or procedure to intercept or attempt to intercept the proper functioning of the cluster's Website.</li>
            <li>Carrying out any action that imposes an unreasonable load, large download size, or improper use of the cluster's Website infrastructure.</li>
            <li>Everything that is considered a violation of the Anti-Cyber Crime Law or the related laws in force in Saudi Arabia.</li>
            <li>Creating, displaying, or placing any links related to the cluster's Website, except for placing links related to services on sites whose objectives and general direction do not conflict with the objectives, policies, and frameworks of e-services affiliated with EHC.</li>
            </ul>
            <h2>General provisions</h2>
            <p>EHC shall not be liable for any expenses, losses, costs, compensation, or claims arising in any way whatsoever, general, special, or even incidental, or caused by any action related to the use of the cluster's Website or the information contained therein.</p>
            <ul>
            <li>EHC is not responsible for any consequences resulting from an error or failure to update the information on the cluster's Website.</li>
            <li>EHC reserves the right to add or change any of the terms of use upon user's notification.</li>
            <li>EHC has the right to temporarily stop the Website when maintenance, development, or modification is needed, until the completion of such necessary work.</li>
            <li>EHC reserves all rights to stop and disable any link in any way from any website that is not authorized or that contains inappropriate, obscene, defamatory, pornographic, objectionable or illegal topics, or contains names, materials, or information that violate any law or violate any public or private rights.</li>
            <li>EHC is not responsible for the content available on any other website accessed through this Website or accessed through the cluster's services.</li>
            </ul>
            <h2>User blocking</h2>
            <p>EHC may, at its sole discretion, terminate, restrict, or suspend the user's right to access the Website without notice and for any reason, including violation of the terms and conditions of use or any other behavior that EHC may consider, in its discretion, to be illegal or harmful to others, and in the event of termination, the user will not be authorized to Login in to the Website.</p>
            <h2>Intellectual property rights</h2>
            <p>The user of the cluster's Website is committed to preserving the intellectual property rights of the cluster's Website, which include, for example, the following: All contents of the Website, including services and information, are fully protected in accordance with the regulations of the Kingdom of Saudi Arabia. The user may not sell, license, rent, modify, copy, reproduce, upload, advertise, transfer, distribute, edit, or create works derived from any materials or contents from the cluster's Website for public or commercial purposes without obtaining prior written approval of the supervising authority. It is prohibited to make any modification to the contents of the cluster's Website, and the graphics and images on the cluster's Website are protected by regulations and may not be reproduced or exploited in any way without the prior written consent of EHC.</p>
            <h2>Website usage and disclaimer</h2>
            <p>Eastern Health cluster (EHC) is not responsible under any circumstances for any direct, indirect, incidental, consequential, special, or exceptional damages arising from the use or inability to use its Website. All information included in the Website are general and instructional information only, and EHC makes no declarations or warranties, express or implied, about the completeness, accuracy, reliability, suitability, or availability of this data, information, or related materials contained on the Website for any purpose and may not be used for a purpose other than general use.</p>
            <h2>Security of website and its related systems</h2>
            <p>EHC has a department specialized in information security that works on the security of the Website and its related systems according to strategic objectives related to compliance with international standards, security policies and procedures to protect information assets from security risks. Penetration test is performed periodically on EHC systems, and the two-factor verification feature is applied to access the cluster's Website and its related systems to enhance security in access and data protection. The cluster's Website imposes a high degree of security on all the Website's equipment and servers, as the latest protection devices are equipped, and EHC makes every effort to examine and test the contents of the Website, and the user must commit to running anti-virus programs in all materials that he/she downloads from the Website.</p>
            <h2>Complaints</h2>
            <p>In the event of complaints or inquiries, contact: Customer Service at: <a href="mailto:EHC@moh.gov.sa">EHC@moh.gov.sa</a></p>
            <h2>Judicial authority</h2>
            <p>The user is subject to all laws and regulations in force in the Kingdom of Saudi Arabia.</p>
            <h2>Website administration</h2>
            <p>The supervising authority of the Website: Digital Content – Eastern Health cluster.</p>
            """;
        return New()
            .Add("pageNavBlock", Text("title", "في هذه الصفحة", "On this page"))
            .Add("richTextBlock", Rich("body", ar, en))
            .Build();
    }

    private string Guide()
    {
        var list = New().Add("introSplitBlock",
            Text("heading", "رعايتكم تبدأ بفهم *احتياجاتكم*", "Your care begins with understanding *your needs*"),
            Rich("body",
                "<p>نقدم خدمات صحية مصممة لكل مستفيد، تجمع بين الخبرة الطبية والاهتمام الشخصي، لضمان تجربة علاجية بمعايير عالمية.</p><p>نحرص على أن تبدأ تجربتكم معنا بكل راحة واطمئنان. ومن خلال هذه الصفحة، ستجدون جميع المعلومات التي تحتاجونها للتخطيط لزيارتكم، والتعرف على الخدمات والمرافق والإرشادات التي تضمن لكم تجربة سلسة ومريحة منذ لحظة وصولكم وحتى انتهاء رحلتكم العلاجية.</p>",
                "<p>We provide personalized healthcare services tailored to every patient, combining medical expertise with compassionate, individualized care to deliver an exceptional treatment experience that meets the highest international standards.</p><p>Your comfort and peace of mind are our priority. This page provides everything you need to plan your visit, explore our services and facilities, and access helpful information designed to ensure a smooth, comfortable, and seamless experience from the moment you arrive until the completion of your care journey.</p>"));

        var cards = New().Items();
        void Card(string icon, string titleAr, string titleEn, string textAr, string textEn, Guid? page, string linkAr, string linkEn)
        {
            if (page is not { } key) return;
            cards.Add("linkCardItem", Pick("icon", icon), Text("title", titleAr, titleEn), Text("text", textAr, textEn),
                Link("link", new SeedLink(linkAr, Page: key), new SeedLink(linkEn, Page: key)));
        }
        Card("clinic", "أن تصبح مريضًا مسجلاً", "Becoming a patient",
            "للاستفادة من مجموعتنا الشاملة من خدمات الرعاية الصحية، عليك أولاً التسجيل لدينا، وتحديد طبيب رعاية أولية لك، وتحديد موعدك الأول.",
            "To benefit from our complete set of healthcare services, you will first need to be registered with us, have a Primary Care physician assigned to you and schedule your first appointment.",
            pages.Facilities, "ابحث عن مركزك الصحي", "Find your health centre");
        Card("cal", "المعلومات الخاصة بالعيادات الخارجية", "Outpatient information",
            "خدماتنا للمرضى الخارجيين تُمكّننا من تقديم علاج عالي الجودة دون الحاجة للبقاء أنت أو أحد أحبائك في المستشفى طوال الليل.",
            "Our outpatient services enable us to provide quality treatment without the need for you or your loved one to remain in hospital overnight.",
            pages.EServices, "المواعيد والخدمات الإلكترونية", "Appointments and e-services");
        Card("hosp", "معلومات المرضى المقيمين", "Hospital stay information",
            "تعرف على كل ما تحتاج إلى معرفته حول إقامتك في المستشفى، بدءًا من الخدمات التي نقدمها للمرضى المقيمين، إلى ما يجب إحضاره معك، وأوقات الزيارة.",
            "Find out all you need to know about your hospital stay, from the services we offer for inpatients, to what to bring with you, visiting times.",
            pages.Facilities, "مستشفياتنا", "Our hospitals");
        Card("users", "الزوار", "Visitor information",
            "خطط لزيارتك مع معلومات حول أوقات الزيارة ومرافقنا وخدماتنا وما يمكنك وما لا يمكنك إحضاره إلى مستشفياتنا.",
            "Plan your visit with information about visiting times, our facilities and services, and what you can and cannot bring to our hospitals.",
            pages.Facilities, "ابحث عن منشأة", "Find a facility");
        list.Add("linkCardsBlock", Text("heading", "خطط لزيارتك", "Plan your visit"), Pick("columns", "2"), Pick("layout", "grid"), List("items", cards));

        list.Add("checklistBlock", Text("heading", "ما يجب إحضاره إلى موعدك", "What to bring to your appointment"), Pick("columns", "2"), Pick("marker", "check"),
            Text("items",
                "الهوية الوطنية أو الإقامة\nقائمة الأدوية الحالية\nالتقارير الطبية السابقة\nرقم الجوال المسجل",
                "National ID or iqama\nA list of your current medicines\nPrevious medical reports\nYour registered mobile number"));

        var emergency = pages.Facilities is { } f
            ? Link("link", new SeedLink("ابحث عن أقرب طوارئ", Page: f), new SeedLink("Find the nearest emergency department", Page: f))
            : new BlockField([]);
        list.Add("calloutBlock", Pick("tone", "emergency"),
            Text("title", "في الحالات الطارئة", "In an emergency"),
            Text("text", "توجّه إلى أقرب قسم طوارئ أو اتصل بالإسعاف على 997.", "Go to the nearest emergency department or call an ambulance on 997."),
            emergency);

        return ContactBand(list, "هل لديك استفسار أو ملاحظة؟", "Have a question or feedback?",
            "يسرّنا تواصلك معنا، وسيتواصل معك فريقنا في أقرب وقت ممكن.", "We are glad to hear from you, and our team will contact you as soon as possible.").Build();
    }

    private string Projects()
    {
        var list = New().Add("introSplitBlock",
            Text("heading", "الارتقاء بالرعاية الصحية من خلال *البحث العلمي*", "Advancing healthcare through *research*"),
            Rich("body",
                "<p>في تجمع الشرقية الصحي، لا نرى البحث مجرد عملية علمية، بل قوة دافعة للتغيير والابتكار. نعرض لكم أهم المشاريع والدراسات التي تعكس شغفنا بالتميز، والتزامنا برعاية صحية قائمة على الأدلة، ومستندة على المعرفة.</p><p>استكشف معنا مجموعة مختارة من الأبحاث المنشورة، مدعومة بملخصات موجزة، تفتح لك أبواب المعرفة، وتلهمك لتكون جزءًا من مستقبل أكثر صحة.</p>",
                "<p>At Eastern Health Cluster, we view research as the engine of progress and excellence in healthcare. This page highlights key scientific contributions that reflect our commitment to evidence-based innovation.</p><p>Explore a curated selection of published studies, each with a brief summary. Through this platform, we aim to empower talent, share knowledge, and drive impactful research partnerships locally and globally.</p>"));

        var items = New().Items();
        void Study(string icon, string titleAr, string titleEn, string textAr, string textEn, string? url = null, string? sourceAr = null, string? sourceEn = null)
        {
            var fields = new List<BlockField> { Pick("icon", icon), Text("title", titleAr, titleEn), Text("text", textAr, textEn) };
            if (url is not null) fields.Add(Link("link", new SeedLink(sourceAr!, url, NewWindow: true), new SeedLink(sourceEn!, url, NewWindow: true)));
            items.Add("featureItem", [.. fields]);
        }
        Study("onc", "اكتشاف تراجع جين UBA7 في الأورام النخاعية المرتبطة بطفرة SF3B1", "Identification of UBA7 expression downregulation in myelodysplastic neoplasm with SF3B1 mutations",
            "يبين هذا البحث أن جين UBA7 يعد لاعبًا محوريًا جديدًا في الأورام النخاعية ذات طفرة SF3B1، حيث كشف عن تضفير غير طبيعي أدى إلى انخفاض تعبيره الجيني، مما يجعله مؤشرًا حيويًا واعدًا للتشخيص وتوجيه العلاجات المستقبلية.",
            "This study highlights UBA7 as a novel key player in SF3B1-mutant myelodysplastic neoplasms, where aberrant splicing leads to reduced gene expression, positioning it as a promising biomarker for diagnosis and a potential target for future therapeutic strategies.",
            "https://pubmed.ncbi.nlm.nih.gov/40158006/", "اقرأ البحث على PubMed", "Read on PubMed");
        Study("lab", "اكتشاف مركب طبيعي لمواجهة البكتيريا المقاومة", "Antibacterial peptide discovery in Staphylococcus epidermidis A487",
            "تمكّن الباحثون من اكتشاف مركب بروتيني جديد من بكتيريا الجلد المفيدة Staphylococcus epidermidis (السلالة A487)، يتمتع بقدرة على مقاومة البكتيريا الضارة، خاصة MRSA المقاومة للمضادات. وقد تم التعرف عليه باستخدام تقنيات تحليل الحمض النووي، ويُعتقد أن الجين hlp هو المسؤول عن إنتاجه.",
            "This study identified a novel haemolysin-like peptide from Staphylococcus epidermidis strain A487 with antibacterial activity, especially against MRSA. Using genome sequencing and mass spectrometry, researchers discovered a unique peptide encoded by the hlp gene. The findings demonstrate the value of genomic data in uncovering new antimicrobial agents.",
            "https://www.researchgate.net/publication/51095511_Identification_of_a_haemolysin-like_peptide_with_antibacterial_activity_using_the_draft_genome_sequence_of_Staphylococcus_epidermidis_strain_A487",
            "اقرأ البحث على ResearchGate", "Read on ResearchGate");
        Study("lab", "اكتشاف مضاد بكتيري جديد من بكتيريا نافعة", "Purification and characterization of a novel δ-lysin variant from Staphylococcus epidermidis",
            "نجح فريق من الباحثين في اكتشاف مركب طبيعي جديد يُعرف باسم δ-ليسين، تم استخلاصه من بكتيريا مفيدة تعيش على الجلد تُسمى Staphylococcus epidermidis (السلالة A87). أظهر المركب قدرة على مقاومة بعض أنواع البكتيريا الضارة، ما قد يمهّد الطريق لاستخدامه مستقبلًا في تطوير مضادات حيوية طبيعية.",
            "Researchers successfully identified and purified a novel δ-lysin peptide from S. epidermidis strain A87, demonstrating antibacterial activity against Gram-positive bacteria. Structural analysis revealed similarities to known cytolytic peptides, indicating a potential role in microbial competition and skin colonization.");
        Study("shield", "ابتكار جديد لتسريع كشف بكتيريا MRSA", "Rapid and cost-effective subtyping of MRSA using denaturing HPLC",
            "ابتكر الباحثون طريقة فعالة وسريعة لتحديد أنواع بكتيريا MRSA المقاومة للمضادات، باستخدام تقنية DHPLC دون الحاجة لتحليل الحمض النووي الكامل. نجحت الطريقة في تصنيف 99 عينة خلال أقل من 5 ساعات، مما يجعلها خيارًا عمليًا لمراقبة العدوى والحد من انتشارها في المستشفيات.",
            "This study presents a novel method for rapidly and affordably subtyping methicillin-resistant Staphylococcus aureus (MRSA) using denaturing high-performance liquid chromatography (DHPLC). The method differentiated 99 S. aureus isolates, identified outbreak-related strains within 5 hours, and offers a faster, cheaper alternative to conventional spa typing for controlling hospital-acquired infections.");
        Study("shield", "سلامة التعامل مع العينات المعدية", "Safety and decontamination procedures for infectious sample handling",
            "تتناول هذه الدراسة طرقًا حديثة لضمان سلامة المختبرات عند التعامل مع العينات المعدية، وتشمل تقنيات تطهير متقدمة مثل البلازما الباردة والأشعة فوق البنفسجية والمذيبات البيئية الآمنة، وتؤكد على أهمية تقييم المخاطر والتدريب المستمر والالتزام بالمعايير الدولية في المختبرات عالية الخطورة (BSL-3 و BSL-4).",
            "This review explores modern strategies for the safe handling of infectious samples in laboratories, including cold atmospheric plasma, UV-C light and deep eutectic solvents, alongside structured risk assessment, continuous training and international regulatory compliance in high-containment labs (BSL-3 and BSL-4).");
        list.Add("featureGridBlock", Text("heading", "قائمة المشاريع", "Research list"), Pick("style", "cards"), Pick("columns", "2"), List("items", items));

        list.Add("ctaBandBlock", Text("title", "انضم إلى أبحاثنا", "Join our research"),
            Text("text", "تعرّف على خدمات الدعم البحثي وكيف تبدأ رحلتك البحثية معنا.", "Find out about our research support services and how to start your research journey with us."),
            Link("primaryCta", new SeedLink("للباحثين", Page: pages.Researchers), new SeedLink("For researchers", Page: pages.Researchers)));
        return list.Build();
    }

    private string Researchers()
    {
        var list = New().Add("introSplitBlock",
            Text("eyebrow", "الباحثون", "Researchers"),
            Text("heading", "كن شريكاً في *صناعة التغيير*", "Be a partner in *change*"),
            Rich("body",
                "<p>في تجمع الشرقية الصحي، نؤمن أن البحث ليس مجرد أداة بل هو ركيزة للتحول. نوفّر بيئة محفّزة للكفاءات الصحية للمشاركة في أبحاث تُسهم في تحسين جودة الحياة، وتُعزز من فاعلية النظام الصحي.</p><p>نحن نحول التحديات إلى فرص وندمج الابتكار في القرارات السريرية وتصميم الأنظمة المستقبلية.</p>",
                "<p>We provide an environment that inspires healthcare professionals to engage in impactful research that transforms lives.</p><p>We transform challenges into opportunities and embed innovation into clinical decisions and future system design.</p>"));

        var why = New().Items()
            .Add("featureItem", Pick("icon", "users"), Text("title", "بيئة بحثية محفزة مبنية على التعاون", "Collaborative and stimulating ecosystem"))
            .Add("featureItem", Pick("icon", "shield"), Text("title", "دعم متكامل يشمل الحوكمة، التدريب، المختبرات، والتمويل", "Full-spectrum support: governance, training, labs, and funding"))
            .Add("featureItem", Pick("icon", "drop"), Text("title", "إمكانية الوصول إلى منصات البيانات والسجلات الحيوية", "Access to data platforms and biobanks"))
            .Add("featureItem", Pick("icon", "award"), Text("title", "إشراف علمي ومهني من فرق متخصصة", "Scientific mentorship from top experts"));
        list.Add("featureGridBlock", Text("heading", "مميزات الانضمام إلى مجتمع الباحثين", "Why join our research community?"), Pick("style", "boxed"), Pick("columns", "4"), List("items", why));

        var steps = New().Items()
            .Add("stepItem", Pick("icon", "bulb"), Text("title", "قدّم فكرتك", "Submit your idea"), Text("text", "قدّم فكرتك البحثية من خلال المنصة الموحدة للأبحاث.", "Submit your research idea through our unified platform."))
            .Add("stepItem", Pick("icon", "search"), Text("title", "التقييم", "Review"), Text("text", "تخضع الفكرة للتقييم من قبل اللجنة العلمية والجهات المختصة.", "Undergo scientific and regulatory review."))
            .Add("stepItem", Pick("icon", "target"), Text("title", "التنفيذ", "Implementation"), Text("text", "بعد اعتمادها، تحصل على الدعم الكامل لتنفيذ دراستك وفق أعلى المعايير.", "Access full implementation support."));
        list.Add("stepsBlock", Text("heading", "كيف تبدأ رحلتك البحثية؟", "Start your journey"), Pick("layout", "horizontal"), List("items", steps));

        var services = New().Items();
        void Service(string icon, string titleAr, string titleEn, string textAr, string textEn) =>
            services.Add("featureItem", Pick("icon", icon), Text("title", titleAr, titleEn), Text("text", textAr, textEn));
        Service("shield", "المراجعة الأخلاقية والحوكمة", "Ethics oversight", "عبر مجلس المراجعة المؤسسية (IRB) المعتمد، والذي يشرف على كافة الدراسات، ويغطي منشآت التجمع من خلال لجان محلية نشطة.", "Central IRB with local site committees.");
        Service("hosp", "دعم التجارب السريرية", "Clinical trials support", "يشمل تنسيق الدراسات مع الرعاة، وتوفير مواقع بحثية نشطة مثل مستشفى القطيف، مركز سعود البابطين، وتجمع الدمام.", "Coordination with sponsors; active sites include Qatif Hospital, Saud Al-Babtain Center, and Dammam Medical Complex.");
        Service("lab", "الدعم المختبري والتقني", "Laboratory & technical services", "عبر مختبرات متقدمة توفر تحاليل جينية وتشخيصات دقيقة تدعم التجارب السريرية والبحوث الحيوية.", "Advanced genomic testing and diagnostics.");
        Service("grid", "تحليل البيانات والإحصاء", "Data & statistical analysis", "تصميم أدوات جمع البيانات، تحليلها، وتقديم تقارير موثوقة تدعم القرارات البحثية.", "Tool design, result analysis, and evidence-based reporting.");
        Service("chat", "الاستشارات المنهجية", "Methodological consultation", "دعم صياغة المقترحات ومراجعة التصاميم البحثية وتسجيلها في المنصات الوطنية.", "Proposal development and registration.");
        Service("book", "التدريب والتأهيل", "Training programs", "تنظيم ورش عمل وبرامج تدريبية في أخلاقيات البحث والكتابة العلمية.", "Research ethics, GCP, and scientific writing.");
        Service("target", "ضبط الجودة والمتابعة", "Quality monitoring", "تطبيق آليات تدقيق داخلية ومراقبة التزام الدراسات بالأنظمة المحلية والدولية.", "Internal audits to ensure full compliance.");
        list.Add("featureGridBlock", Text("heading", "خدمات الدعم البحثي", "Comprehensive research support services"),
            Text("intro", "ندعم الباحثين في جميع مراحل مشاريعهم.", "We empower researchers across all project phases."), Pick("style", "list"), Pick("columns", "2"), List("items", services));

        list.Add("introSplitBlock",
            Text("heading", "منشوراتنا وإنجازاتنا", "Publications & achievements"),
            Rich("body",
                "<p>يفخر مركز الأبحاث في تجمع الشرقية الصحي بإنتاجه العلمي المتميز، حيث ساهم باحثوه في مئات المنشورات المحكمة في مجلات دولية مرموقة، تغطي مجالات حيوية مثل الأورام، الأمراض الوراثية، الطب الدقيق، والصحة العامة.</p><ul><li>مقالات علمية في مجلات محكمة</li><li>مشاركات في مؤتمرات وطنية وعالمية</li><li>براءات اختراع وتقنيات مبتكرة</li><li>جوائز وتكريمات بحثية</li></ul><p>ساهمت الجهود البحثية في تحسين الخدمات الصحية، وزيادة الإيرادات، وتقليل المصاريف.</p>",
                "<p>EHC researchers have contributed to hundreds of peer-reviewed articles in high-impact journals across oncology, public health, precision medicine, and genetics.</p><ul><li>Peer-reviewed scientific publications</li><li>National and international conference participation</li><li>Patents and innovative technologies</li><li>Research awards and recognitions</li></ul><p>Research contributions have enhanced healthcare service quality, revenue generation and cost-efficiency.</p>"),
            Links("ctas", [new SeedLink("استعرض المنشورات", Page: pages.Projects)], [new SeedLink("View publications", Page: pages.Projects)]));

        list.Add("checklistBlock", Text("heading", "فريقنا البحثي", "Meet our research team"), Pick("columns", "2"), Pick("marker", "dot"),
            Text("items",
                "مجموعة من الاستشاريين والعلماء الإكلينيكيين والباحثين المشاركين\nقادة من المتخصصين في مجال الإدارة والأعمال والشراكات الإقليمية والدولية\nأخصائيو البيانات والتحليل الإحصائي لدعم المخرجات العلمية الدقيقة\nمختصو وفنيو المختبرات الطبية الحيوية البحثية في مجالات الجزيئات والوراثة والتقنيات المتقدمة\nخبراء أخلاقيات البحث وحماية المشاركين لضمان أعلى مستويات الامتثال والسلامة\nمختصون في علم الأدوية والتطبيقات السريرية للصيدلة",
                "Senior consultants and clinical scientists\nProject leaders and research coordinators\nData analysts and statisticians\nBiomedical lab experts\nEthics and compliance specialists\nClinical pharmacists and regulatory officers"));

        return ContactBand(list, "لديك فكرة بحثية؟", "Have a research idea?",
            "تواصل معنا وسنرشدك إلى الخطوة التالية.", "Get in touch and we will guide you to the next step.").Build();
    }

    /// <summary>
    /// Patient rights as published by the Ministry of Health (overview page and "Patient rights" PDF, Arabic only; the
    /// English list is a translation), how to raise a concern, and e-participation channels. The official documents
    /// are linked as the reference.
    /// </summary>
    private string Rights()
    {
        const string moh = "https://www.moh.gov.sa";
        var list = New().Add("introSplitBlock",
            Text("eyebrow", "حقوق المرضى", "Patient rights"),
            Text("heading", "حقوقك *محفوظة*", "Your rights are *protected*"),
            Rich("body",
                "<p>تولي المنظومة الصحية في المملكة العربية السعودية اهتمامًا بالغًا بحقوق المرضى، إدراكًا لأهمية تعزيز العلاقة بين المريض ومقدّم الخدمة الصحية على أسس من الكرامة الإنسانية، والعدالة، والشفافية، والاحترام المتبادل.</p><p>ولا تقتصر حقوق المرضى على الجوانب العلاجية المباشرة فحسب، بل تشمل إطارًا أوسع يتناول مختلف أوجه العلاقة بين المريض والمنظومة الصحية.</p>",
                "<p>The healthcare system in the Kingdom of Saudi Arabia places great importance on patients' rights, recognizing that safeguarding these rights is a fundamental cornerstone in the delivery of humane, safe, and comprehensive healthcare.</p><p>Patients' rights go far beyond receiving medical treatment. They encompass a broad spectrum of protections and entitlements designed to uphold the patient's autonomy, respect their privacy, and ensure fair and dignified treatment throughout their healthcare journey.</p>"));

        var cover = New().Items();
        void Right(string icon, string titleAr, string titleEn, string textAr, string textEn) =>
            cover.Add("featureItem", Pick("icon", icon), Text("title", titleAr, titleEn), Text("text", textAr, textEn));
        Right("hosp", "الوصول إلى الرعاية", "Access to care", "حقك في الوصول إلى الخدمة الصحية بيسر.", "Your right to access healthcare easily.");
        Right("doc", "معلومات واضحة", "Clear information", "معلومات دقيقة وواضحة حول حالتك الصحية وتشخيصك وخيارات العلاج المتاحة.", "Clear and accurate information about your condition, your diagnosis and the treatment options available.");
        Right("hand", "القرار لك", "Your decision", "اتخاذ قرارات مستنيرة بشأن رعايتك، بالموافقة أو الرفض، دون أي ضغط أو تمييز.", "Making informed decisions about your care, to consent or refuse, without pressure or discrimination.");
        Right("shield", "الخصوصية", "Privacy", "ضمان خصوصية معلوماتك الصحية.", "Confidentiality of your health information.");
        Right("users", "الاحترام والعدالة", "Respect and fairness", "الاحترام الكامل لثقافتك ومعتقداتك، وتقديم الرعاية بعدالة وشفافية.", "Full respect for your culture and beliefs, and care given fairly and transparently.");
        Right("chat", "الشكوى والرد", "Complaints and answers", "تقديم الشكاوى والاعتراضات وتلقي الردود المناسبة عليها ضمن آليات واضحة.", "Making complaints and objections and receiving appropriate answers through clear procedures.");
        list.Add("featureGridBlock", Text("heading", "ما تشمله حقوقك", "What your rights cover"),
            Text("intro", "كما توضحها وزارة الصحة.", "As set out by the Ministry of Health."), Pick("style", "cards"), Pick("columns", "3"), List("items", cover));

        list.Add("checklistBlock", Text("heading", "أثناء تلقيك الرعاية يحق لك", "During your care, you have the right to"), Pick("columns", "1"), Pick("marker", "check"),
            Text("items",
                "معرفة أسماء وتخصصات الفريق الطبي القائم على العلاج وإبلاغك بوجود متدربين أو باحثين مرخصين ضمن الفريق الطبي.\nعدم وجود من ليس له علاقة أثناء الكشف الطبي عليك.\nقيام الطبيب المعالج بشرح حالتك الطبية باللغة والطريقة التي تفهمها، وتوفير مترجم في حال كان الطبيب غير ناطق بلغتك.\nسؤال الممارس الصحي «هل غسلت يديك؟» تجنبًا لاحتمالية العدوى.\nالحصول على الوقت الكافي مع الطاقم الطبي أثناء العلاج.\nرفض أسلوب أو وسيلة العلاج المقترحة في حدود ما يسمح به النظام، مع تحمل العواقب بعد توضيحها لك.\nطلب رأي طبيب آخر حول تشخيص حالتك.",
                "Know the names and specialties of the medical team treating you, and be told if licensed trainees or researchers are part of the team.\nHave no one present during your examination who is not involved in your care.\nHave your doctor explain your condition in a language and a way you understand, with an interpreter if the doctor does not speak your language.\nAsk a healthcare worker \"Did you wash your hands?\" to prevent infection.\nHave enough time with the medical team during your treatment.\nRefuse a proposed treatment, within what the regulations allow, after its consequences have been explained to you.\nAsk another doctor for a second opinion on your diagnosis."));

        var docs = New().Items();
        void Doc(string icon, string titleAr, string titleEn, string textAr, string textEn, string badgeAr, string badgeEn, string urlAr, string urlEn) =>
            docs.Add("linkCardItem", Pick("icon", icon), Text("title", titleAr, titleEn), Text("text", textAr, textEn), Text("badge", badgeAr, badgeEn),
                Link("link", new SeedLink(titleAr, urlAr, NewWindow: true), new SeedLink(titleEn, urlEn, NewWindow: true)));
        Doc("doc", "وثيقة حقوق المرضى", "Patient rights document", "القائمة الكاملة لحقوق المرضى.", "The full list of patient rights.", "PDF", "PDF · Arabic",
            moh + "/awarenessplateform/Patientsrights/Documents/%D8%AD%D9%82%D9%88%D9%82%20%D8%A7%D9%84%D9%85%D8%B1%D8%B6%D9%89-.pdf",
            moh + "/awarenessplateform/Patientsrights/Documents/%D8%AD%D9%82%D9%88%D9%82%20%D8%A7%D9%84%D9%85%D8%B1%D8%B6%D9%89-.pdf");
        Doc("hosp", "حقوق المرضى في المستشفيات الحكومية", "Patient rights at public hospitals", "الحقوق في المستشفيات والمراكز الحكومية.", "Rights at public hospitals and centres.", "PDF", "PDF · Arabic",
            moh + "/awarenessplateform/Patientsrights/Documents/%D8%AD%D9%82%D9%88%D9%82%20%D8%A7%D9%84%D9%85%D8%B1%D8%B6%D9%89%20%D9%81%D9%8A%20%D8%A7%D9%84%D9%85%D8%B3%D8%AA%D8%B4%D9%81%D9%8A%D8%A7%D8%AA%20%D8%A7%D9%84%D8%AD%D9%83%D9%88%D9%85%D9%8A%D8%A9.pdf",
            moh + "/awarenessplateform/Patientsrights/Documents/%D8%AD%D9%82%D9%88%D9%82%20%D8%A7%D9%84%D9%85%D8%B1%D8%B6%D9%89%20%D9%81%D9%8A%20%D8%A7%D9%84%D9%85%D8%B3%D8%AA%D8%B4%D9%81%D9%8A%D8%A7%D8%AA%20%D8%A7%D9%84%D8%AD%D9%83%D9%88%D9%85%D9%8A%D8%A9.pdf");
        Doc("heart", "حملة حقوق المرضى", "Patient rights campaign", "صفحة حقوق المرضى في وزارة الصحة.", "The Ministry of Health's patient rights pages.", "وزارة الصحة", "Ministry of Health",
            moh + "/awarenessplateform/patientsrights/pages/default.aspx", moh + "/en/awarenessplateform/patientsrights/pages/default.aspx");
        list.Add("linkCardsBlock", Text("heading", "الوثائق الرسمية", "Official documents"),
            Text("intro", "تلخّص هذه الصفحة حقوق المرضى كما تنشرها وزارة الصحة، والوثائق الرسمية هي المرجع.", "This page summarises patient rights as published by the Ministry of Health; the official documents are the reference."),
            Pick("columns", "3"), Pick("layout", "grid"), List("items", docs));

        var steps = New().Items()
            .Add("stepItem", Pick("icon", "chat"), Text("title", "تحدّث إلينا في المنشأة", "Talk to us at the facility"),
                Text("text", "تحدّث مع الطاقم أو مكتب تجربة المريض في المستشفى أو المركز الصحي.", "Speak to the staff or the patient experience office at the hospital or health centre."))
            .Add("stepItem", Pick("icon", "note"), Text("title", "أرسل ملاحظتك إلكترونيًا", "Send it online"),
                Text("text", "استخدم نموذج تواصل معنا، وستحصل على رقم مرجعي لمتابعتها.", "Use the contact form and you will get a reference number to follow it up."))
            .Add("stepItem", Pick("icon", "phone"), Text("title", "اتصل على 937", "Call 937"),
                Text("text", "مركز الاتصال الموحد لوزارة الصحة يستقبل الاستفسارات والشكاوى.", "The Ministry of Health's unified call centre takes questions and complaints."));
        list.Add("stepsBlock", Text("heading", "إذا كانت لديك ملاحظة أو شكوى", "If you have a concern or complaint"), Pick("layout", "horizontal"), List("items", steps));

        var channels = New().Items();
        if (pages.Contact is { } contact)
            channels.Add("linkCardItem", Pick("icon", "bulb"), Text("title", "شاركنا اقتراحك", "Share a suggestion"),
                Text("text", "أفكارك تساعدنا على تحسين خدماتنا.", "Your ideas help us improve our services."),
                Link("link", new SeedLink("تواصل معنا", Page: contact), new SeedLink("Contact us", Page: contact)));
        channels.Add("linkCardItem", Pick("icon", "globe"), Text("title", "المشاركة الإلكترونية", "E-participation"),
                Text("text", "الاستشارات والاستطلاعات والمشاركة في وزارة الصحة.", "Consultations, surveys and participation at the Ministry of Health."), Text("badge", "وزارة الصحة", "Ministry of Health"),
                Link("link", new SeedLink("المشاركة الإلكترونية", moh + "/e-participation/pages/default.aspx", NewWindow: true), new SeedLink("E-participation", moh + "/en/e-participation/pages/default.aspx", NewWindow: true)))
            .Add("linkCardItem", Pick("icon", "grid"), Text("title", "البيانات المفتوحة", "Open data"),
                Text("text", "البيانات الحكومية المفتوحة على المنصة الوطنية.", "Government open data on the national platform."), Text("badge", "المنصة الوطنية", "National platform"),
                Link("link", new SeedLink("البيانات المفتوحة", "https://open.data.gov.sa/", NewWindow: true), new SeedLink("Open data", "https://open.data.gov.sa/", NewWindow: true)));
        list.Add("linkCardsBlock", Text("eyebrow", "المشاركة الإلكترونية", "E-participation"), Text("heading", "صوتك يصنع *الفرق*", "Your voice makes *a difference*"),
            Text("intro", "شاركنا آراءك وأفكارك لنطوّر خدماتنا معًا.", "Share your views and ideas so we can improve our services together."),
            Pick("columns", "3"), Pick("layout", "grid"), List("items", channels));

        return ContactBand(list, "لديك ملاحظة أو شكوى؟", "Have a concern or complaint?",
            "أرسلها إلينا وستحصل على رقم مرجعي لمتابعتها.", "Send it to us and you will get a reference number to follow it up.").Build();
    }

    private string Academic()
    {
        var list = New().Add("introSplitBlock",
            Text("eyebrow", "الشؤون الأكاديمية والتدريب", "Academic affairs & training"),
            Text("heading", "تعزيز التميز في *التعليم والتدريب*", "Advancing excellence in *education & training*"),
            Text("subheading", "في تجمع الشرقية الصحي، نُدرك أن بناء مستقبل صحي متكامل يبدأ بتأهيل الإنسان.", "At EHC, we believe that building a sustainable and integrated healthcare future begins with investing in people."),
            Rich("body",
                "<p>تعمل إدارة الشؤون الأكاديمية والتدريب على ترسيخ مكانة التجمع كمركز رائد في التعليم والتدريب الصحي، والمساهمة بفاعلية في مسيرة التحول الوطني عبر تطوير المهارات والقدرات القيادية والمهنية لمنسوبي القطاع الصحي، معتمدين على معايير الابتكار، والكفاءة، والتمكين، لبناء كوادر قادرة على قيادة التحول الصحي في المملكة.</p>",
                "<p>The Academic and Training Affairs Department is dedicated to positioning the Cluster as a leading hub for healthcare education and training. Our mission is to contribute actively to the national transformation journey by developing the leadership, professional, and technical competencies of healthcare professionals. Guided by the principles of innovation, efficiency, and empowerment, we are committed to preparing a capable workforce that can lead the healthcare transformation across the Kingdom.</p>"));

        var stats = New().Items()
            .Add("statItem", Pick("icon", "users"), BlockField.Shared("value", "##"), BlockField.Shared("suffix", "+"), Text("label", "متدرب", "Trainees"))
            .Add("statItem", Pick("icon", "hosp"), BlockField.Shared("value", "##"), Text("label", "منشأة تدريبية", "Training facilities"))
            .Add("statItem", Pick("icon", "award"), BlockField.Shared("value", "##"), BlockField.Shared("suffix", "+"), Text("label", "برنامج تدريبي طبي معتمد", "Accredited medical training programs"));
        list.Add("statsBandBlock", Pick("tone", "deep"), List("stats", stats));

        list.Add("richTextBlock", Rich("body",
            "<h2>منظومة تعليمية متكاملة لتأهيل الكفاءات الصحية</h2><p>نُشرف على منظومة متكاملة من البرامج التدريبية والتعليمية تشمل الدراسات العليا، التعليم المستمر، التدريب الجامعي، وبرامج الابتعاث، جميعها مصممة لتواكب احتياجات السوق الصحي وتُبنى على معايير الجودة والاعتماد المؤسسي.</p>",
            "<h2>A comprehensive educational ecosystem for healthcare excellence</h2><p>We manage an integrated system of educational and training programs—including postgraduate studies, continuing education, undergraduate internships, and scholarship opportunities. Each program is designed to meet the evolving needs of the healthcare market and is built upon standards of quality and institutional accreditation.</p>"));

        list.Add("checklistBlock", Text("heading", "مواقع التدريب", "Training sites"), Pick("columns", "2"), Pick("marker", "check"),
            Text("items",
                "مستشفى الملك فهد التخصصي\nمجمع الدمام الطبي\nمستشفى القطيف العام\nمستشفى الولادة والأطفال\nمركز إرادة\nمركز سعود البابطين\nمستشفى العيون التخصصي\nأكاديمية طب الأسرة (31 مركز أولي)",
                "King Fahad Specialist Hospital\nDammam Medical Complex\nQatif Central Hospital\nMaternity and Children's Hospital\nEradah Mental Health Complex\nSaud Al Babtain Cardiac Center\nDhahran Eye Specialist Hospital\nFamily Medicine Academy (31 Primary Healthcare Centers)"));

        var departments = New().Items();
        void Department(string questionAr, string questionEn, string answerAr, string answerEn) =>
            departments.Add("accordionItem", Text("question", questionAr, questionEn), Rich("answer", answerAr, answerEn));
        Department("إدارة التدريب والتطوير", "Training and Development Department",
            "<p>في رحلة التحول الصحي، تبرز إدارة التدريب والتطوير كمنصة ريادية لبناء القدرات، وصقل المهارات، وتمكين الكفاءات. نعمل على تقديم برامج تعليمية وتطويرية عالية الجودة ترتكز على التخصص، والتمكين، والتأثير:</p><ul><li>تطوير المهارات الإكلينيكية والقيادية.</li><li>تنفيذ خطط تدريب مهنية شاملة.</li><li>توفير بيئة تعليمية محفزة على الإبداع والتميّز.</li></ul>",
            "<p>As part of the healthcare transformation journey, the Training and Development Department serves as a strategic platform for capacity building, skill enhancement, and workforce empowerment. We deliver high-quality educational and professional development programs focused on specialization, empowerment, and impact:</p><ul><li>Developing clinical and leadership competencies</li><li>Implementing comprehensive professional training plans</li><li>Providing a stimulating educational environment that fosters creativity and excellence</li></ul>");
        Department("وحدة ضمان جودة التدريب", "Training Quality Assurance Unit (TDQA)",
            "<p>انطلاقًا من رؤيتنا للريادة في مجال التدريب الصحي، تضع وحدة ضمان جودة التدريب معايير دقيقة وآليات فعالة لرفع كفاءة البرامج التعليمية والأكاديمية في التجمع.</p><h3>أهداف الوحدة</h3><ul><li>التأكد من تحقيق أهداف التدريب بكفاءة وفعالية.</li><li>مراقبة الأنشطة ومراجعتها باستمرار لتحسين الأداء.</li><li>تعزيز الاتساق المؤسسي وخفض التكاليف وتقليل الهدر.</li><li>ضمان دقة وحداثة البيانات والمعلومات التشغيلية.</li><li>نشر ثقافة التحسين المستمر على جميع المستويات.</li></ul><p>تُعد الوحدة المرجعية المركزية لضمان مواءمة جميع المبادرات التدريبية مع أهداف التحول الوطني، ودعم مكانة تجمع الشرقية الصحي كمنظومة تعليمية متقدمة ومسؤولة.</p>",
            "<p>Driven by our vision to lead in healthcare training, the Training Quality Assurance Unit (TDQA) establishes rigorous standards and effective mechanisms to enhance the efficiency and quality of educational programs across the Cluster.</p><h3>Unit objectives</h3><ul><li>Ensure training goals are achieved effectively and efficiently</li><li>Continuously monitor and review activities to improve performance</li><li>Promote institutional consistency while reducing costs and minimizing waste</li><li>Ensure the accuracy and timeliness of operational data</li><li>Foster a culture of continuous improvement at all levels</li></ul><p>As the central reference body for aligning training initiatives, the unit plays a key role in supporting the Eastern Health Cluster's position as a leading academic and training entity.</p>");
        Department("أكاديمية طب الأسرة", "Family Medicine Academy",
            "<p>نقود التميز في الرعاية الأولية من خلال أكبر برنامج تدريبي في طب الأسرة على مستوى المملكة، حيث نُشرف على برامج دراسات عليا متخصصة تؤهل الأطباء لتقديم رعاية وقائية ومجتمعية شاملة.</p><h3>البرامج الأكاديمية</h3><ul><li>برنامج شهادة الاختصاص السعودية في طب الأسرة.</li><li>برنامج الرعاية العاجلة لطب الأسرة.</li></ul><h3>مميزات البرامج</h3><ul><li>22 مركز رعاية صحية أولية معتمد.</li><li>شراكة فعالة مع الهيئة السعودية للتخصصات الصحية.</li><li>مخرجات أكاديمية متميزة على المستوى الوطني.</li></ul>",
            "<p>We lead excellence in primary care through the largest family medicine training program in the Kingdom. The Academy supervises specialized postgraduate programs that prepare physicians to deliver comprehensive preventive and community-based healthcare.</p><h3>Academic programs</h3><ul><li>Saudi Board Certification Program in Family Medicine</li><li>Urgent Care Program for Family Medicine</li></ul><h3>Program highlights</h3><ul><li>22 accredited primary healthcare centers</li><li>Strong partnership with the Saudi Commission for Health Specialties</li><li>Outstanding academic outcomes recognized at the national level</li></ul>");
        Department("إدارة الدراسات العليا", "Postgraduate Education Department",
            "<p><strong>بوابتك نحو التخصص والتميّز.</strong> نُشرف على برامج دراسات عليا بشراكة استراتيجية مع الهيئة السعودية للتخصصات الصحية، عبر مسارات أكاديمية متكاملة تُعد الأطباء لمراحل متقدمة من التخصص.</p><h3>آلية القبول</h3><ol><li>التقديم عبر <a href=\"https://www.scfhs.org.sa\">www.scfhs.org.sa</a></li><li>إنشاء حساب واستكمال البيانات.</li><li>اختيار البرنامج والمركز.</li><li>حضور المقابلات.</li><li>استكمال إجراءات المطابقة.</li></ol><h3>شروط القبول</h3><ul><li>شهادة بكالوريوس صحية.</li><li>اجتياز سنة الامتياز.</li><li>تصنيف مهني.</li><li>لياقة طبية.</li><li>رعاية مؤسسية لغير السعوديين.</li></ul>",
            "<p><strong>Your gateway to specialization and excellence.</strong> We supervise postgraduate training programs in strategic partnership with the Saudi Commission for Health Specialties (SCFHS), offering comprehensive academic pathways that prepare physicians for advanced specialization.</p><h3>Admission process</h3><ol><li>Apply via <a href=\"https://www.scfhs.org.sa\">www.scfhs.org.sa</a></li><li>Create an account and complete your profile</li><li>Select your desired program and training center</li><li>Attend interviews</li><li>Complete the matching process</li></ol><h3>Eligibility requirements</h3><ul><li>Bachelor's degree in a healthcare-related field</li><li>Completion of internship year</li><li>Professional classification</li><li>Medical fitness</li><li>Institutional sponsorship (for non-Saudis)</li></ul>");
        Department("تدريب طلاب الجامعات واكتساب الخبرة", "University Student Training and Experience Program",
            "<p>نُمكّن طلاب البكالوريوس والامتياز من الانطلاق بثقة نحو مستقبلهم المهني من خلال فرص تدريب جامعي عالية الجودة، تشمل:</p><ul><li>توجيه وتخطيط مسار تدريبي فعّال.</li><li>إشراف ميداني متخصص لضمان تجربة عملية مثمرة.</li><li>برامج تدريبية مصممة لتواكب احتياجات سوق العمل الصحية.</li></ul><h3>خطوات الانضمام</h3><ol><li>إنشاء حساب عبر المنصة (منصة تام للمتدربين).</li><li>اختيار الفرصة التدريبية المناسبة.</li><li>تقديم الوثائق واستكمال المتطلبات.</li></ol><h3>الشروط</h3><ul><li>التقديم قبل شهر من تاريخ المباشرة.</li><li>تقديم الهوية، كشف الدرجات، خطاب الجامعة، والتقارير الطبية.</li></ul>",
            "<p>We empower undergraduate and internship students to confidently pursue their professional future by offering high-quality training opportunities, including:</p><ul><li>Structured guidance and planning for an effective training journey</li><li>Field supervision by qualified professionals to ensure a valuable hands-on experience</li><li>Training programs aligned with the evolving needs of the healthcare market</li></ul><h3>Steps to join</h3><ol><li>Create an account via the platform (TAM Platform for Trainees)</li><li>Select the appropriate training opportunity</li><li>Submit required documents and complete application requirements</li></ol><h3>Requirements</h3><ul><li>Apply at least one month before the intended start date</li><li>Submit national ID, academic transcript, university letter, and medical reports</li></ul>");
        Department("برنامج الابتعاث والإيفاد", "Scholarship and Sponsorship Program",
            "<p><strong>شراكات معرفية لتمكين مستقبل الرعاية الصحية.</strong> تقديم فرص ابتعاث للموظفين داخليًا وخارجيًا، كجزء من استراتيجية بناء القدرات والكفاءات داخل التجمع الصحي. نُمكّن كوادرنا من التعلّم عالميًا ومحليًا من خلال:</p><ul><li>منح دراسية وبرامج تخصصية داخل وخارج المملكة.</li><li>شراكات استراتيجية مع جامعات وهيئات دولية.</li><li>دعم التخصصات النادرة ذات الأولوية.</li></ul>",
            "<p><strong>Knowledge partnerships to empower the future of healthcare.</strong> We offer local and international scholarship opportunities for employees as part of our strategy to build capabilities and enhance workforce competencies within the Health Cluster. We empower our staff to learn locally and globally through:</p><ul><li>Academic scholarships and specialized training programs inside and outside the Kingdom</li><li>Strategic partnerships with international universities and institutions</li><li>Support for rare and high-priority specialties</li></ul>");
        list.Add("accordionBlock", Text("heading", "أقسام الشؤون الأكاديمية والتدريب", "Departments of academic and training affairs"), List("items", departments));

        return ContactBand(list, "للتواصل مع الشؤون الأكاديمية والتدريب", "Contact academic and training affairs",
            "لاستفسارات الدراسات العليا والتدريب الجامعي والابتعاث، أرسل رسالتك وسنحوّلها إلى الفريق المختص.", "For postgraduate, university training and scholarship enquiries, send us a message and we will pass it to the team responsible.").Build();
    }
}
