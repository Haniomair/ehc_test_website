/* EHC website behaviours (ported from Concept C).
   TODO: split into ES modules per component and guard every feature with a null check,
   because Umbraco pages will only contain the blocks an editor added. */

(function(){
  var html=document.documentElement;window.hooks=[];
  // ---------- theme ----------
  var userTheme=false;
  window.toggleTheme=function(){userTheme=true;html.classList.toggle('dark')};
  if(window.matchMedia){var mq=window.matchMedia('(prefers-color-scheme: dark)');var h=function(e){if(!userTheme)html.classList.toggle('dark',e.matches)};mq.addEventListener?mq.addEventListener('change',h):mq.addListener(h)}
  // ---------- marquee ----------
  var words=[["Cardiology","أمراض القلب"],["Oncology","الأورام"],["Pediatrics","طب الأطفال"],["Orthopedics","العظام"],["Ophthalmology","العيون"],["Family medicine","طب الأسرة"],["Women's health","صحة المرأة"],["Neurology","الأعصاب"],["Emergency care","الطوارئ"],["Rehabilitation","التأهيل"]];
  function buildMq(ar){var s='';for(var k=0;k<2;k++)words.forEach(function(w,i){s+='<span class="inline-flex items-center gap-12 '+(i%3===0?'text-brand-500 dark:text-brand-300':'text-stroke')+'">'+(ar?w[1]:w[0])+'<span class="text-[.6em] text-brand-500">✦</span></span>'});document.getElementById('mq').innerHTML=s}
  buildMq(false);
  // ---------- i18n ----------
  var els=[].slice.call(document.querySelectorAll('[data-ar]'));els.forEach(function(e){e._en=e.innerHTML});
  var phs=[].slice.call(document.querySelectorAll('[data-ar-ph]'));phs.forEach(function(e){e._en=e.placeholder});
  function logos(ar){document.querySelectorAll('.lg-en').forEach(function(i){i.classList.toggle('hidden',ar);i.classList.toggle('block',!ar)});document.querySelectorAll('.lg-ar').forEach(function(i){i.classList.toggle('hidden',!ar);i.classList.toggle('block',ar)})}
  window.toggleLang=function(){var ar=html.lang!=='ar';html.lang=ar?'ar':'en';html.dir=ar?'rtl':'ltr';
    els.forEach(function(e){e.innerHTML=ar?e.getAttribute('data-ar'):e._en});phs.forEach(function(e){e.placeholder=ar?e.getAttribute('data-ar-ph'):e._en});
    document.querySelector('#langBtn span').textContent=ar?'English':'العربية';buildMq(ar);logos(ar);window.hooks.forEach(function(f){f(ar)})};
  var size=16;window.fs=function(d){size=Math.max(14,Math.min(20,size+d));html.style.setProperty('--fs',size+'px')};
  window.notes=function(o){document.getElementById('notes').classList.toggle('open',o)};
  window.drawer=function(o){document.getElementById('drawer').classList.toggle('hidden',!o);document.body.style.overflow=o?'hidden':''};
  // ---------- mega menu ----------
  var trigs=[].slice.call(document.querySelectorAll('.mtrig')),dim=document.getElementById('megaDim'),hdr=document.getElementById('hdr'),openId=null,tOpen,tClose;
  function openMega(id){clearTimeout(tClose);if(openId===id)return;trigs.forEach(function(t){t.setAttribute('aria-expanded',t.dataset.mega===id)});
    document.querySelectorAll('.mega-panel').forEach(function(p){p.classList.toggle('open',p.id===id)});openId=id;dim.classList.toggle('opacity-100',!!id)}
  function closeMega(){openId=null;trigs.forEach(function(t){t.setAttribute('aria-expanded','false')});document.querySelectorAll('.mega-panel').forEach(function(p){p.classList.remove('open')});dim.classList.remove('opacity-100')}
  trigs.forEach(function(t){
    t.addEventListener('mouseenter',function(){clearTimeout(tOpen);tOpen=setTimeout(function(){openMega(t.dataset.mega)},openId?0:120)});
    t.addEventListener('mouseleave',function(){clearTimeout(tOpen)});
    t.addEventListener('click',function(){openId===t.dataset.mega?closeMega():openMega(t.dataset.mega)});
    t.addEventListener('keydown',function(e){if(e.key==='ArrowDown'){e.preventDefault();openMega(t.dataset.mega);var f=document.getElementById(t.dataset.mega).querySelector('a');f&&f.focus()}});
  });
  hdr.addEventListener('mouseleave',function(){clearTimeout(tOpen);tClose=setTimeout(closeMega,180)});
  hdr.addEventListener('mouseenter',function(){clearTimeout(tClose)});
  document.addEventListener('keydown',function(e){if(e.key==='Escape'){if(openId){var t=document.querySelector('[data-mega="'+openId+'"]');closeMega();t&&t.focus()}notes(false);drawer(false)}});
  document.addEventListener('click',function(e){if(openId&&!hdr.contains(e.target))closeMega()});
  document.querySelectorAll('.mega-panel a').forEach(function(a){a.addEventListener('click',closeMega)});
  // ---------- hero slider ----------
  var hero=document.getElementById('hero'),slides=hero.querySelectorAll('.slide'),btns=document.querySelectorAll('#heroNav button'),cur=0,timer,reduce=window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  function go(i){cur=i;slides.forEach(function(s,j){s.classList.toggle('hidden',j!==i)});
    btns.forEach(function(b,j){b.classList.remove('prog-on');void b.offsetWidth;if(j===i)b.classList.add('prog-on');b.setAttribute('aria-selected',j===i)});
    hero.classList.remove('hero-bg-0','hero-bg-1','hero-bg-2');hero.classList.add('hero-bg-'+i);
    clearTimeout(timer);if(!reduce)timer=setTimeout(function(){go((cur+1)%slides.length)},7000)}
  btns.forEach(function(b,j){b.addEventListener('click',function(){go(j)})});go(0);
  // ---------- facility filter ----------
  var chips=document.querySelectorAll('#facChips .chip');
  function filt(f){chips.forEach(function(c){c.setAttribute('aria-pressed',c.dataset.f===f)});
    document.querySelectorAll('#pins .pin').forEach(function(p){p.classList.toggle('dim',f!=='all'&&p.dataset.t.split(' ').indexOf(f)<0)});
    document.querySelectorAll('#facList > a').forEach(function(a){var ok=(f==='all'&&a.dataset.t!=='spec')||a.dataset.t.split(' ').indexOf(f)>=0;a.classList.toggle('hidden',!ok);a.classList.toggle('flex',ok)})}
  chips.forEach(function(c){c.addEventListener('click',function(){filt(c.dataset.f)})});
  // ---------- rail ----------
  window.rail=function(d){var r=document.getElementById('rail');r.scrollBy({left:d*(html.dir==='rtl'?-1:1)*340,behavior:'smooth'})};
  // ---------- reveal ----------
  if('IntersectionObserver' in window){var io=new IntersectionObserver(function(es){es.forEach(function(e){if(e.isIntersecting){e.target.classList.add('in');io.unobserve(e.target)}})},{threshold:0,rootMargin:'0px 0px -8% 0px'});document.querySelectorAll('.rv').forEach(function(e){io.observe(e)})}else document.querySelectorAll('.rv').forEach(function(e){e.classList.add('in')});
  document.querySelectorAll('a[href="#"]').forEach(function(a){a.addEventListener('click',function(e){e.preventDefault()})});

  // ================= Concept C additions =================
  var isAr=function(){return html.lang==='ar'};
  var T=function(o){return isAr()?o.ar:o.en};
  // ---------- care navigator ----------
  var NAV={
    urgent:{t:{en:'Call 997 now, or go to the nearest ER',ar:'اتصل على 997 فورًا أو توجّه لأقرب طوارئ'},d:{en:'For chest pain, breathing trouble, severe bleeding or loss of consciousness, don\'t wait.',ar:'في حالات ألم الصدر أو صعوبة التنفس أو النزيف الشديد أو فقدان الوعي، لا تنتظر.'},c:{en:'Find nearest ER',ar:'أقرب قسم طوارئ'},h:'#facilities',ic:'i-er',red:true},
    visit:{t:{en:'Book with your family doctor',ar:'احجز مع طبيب الأسرة'},d:{en:'Your primary care center is the first step, and refers you to a specialist if needed.',ar:'مركز الرعاية الأولية هو الخطوة الأولى، ويحوّلك للأخصائي عند الحاجة.'},c:{en:'Book via Sehhaty',ar:'احجز عبر صحتي'},h:'#',ic:'i-cal'},
    results:{t:{en:'Your results are in Sehhaty',ar:'نتائجك في تطبيق صحتي'},d:{en:'Lab and radiology results appear as soon as they are approved.',ar:'تظهر نتائج المختبر والأشعة فور اعتمادها.'},c:{en:'Open Sehhaty',ar:'افتح صحتي'},h:'#',ic:'i-lab'},
    vaccine:{t:{en:'Walk in to any health center',ar:'توجّه لأي مركز صحي'},d:{en:'The seasonal flu vaccine is free, with no appointment needed.',ar:'لقاح الإنفلونزا الموسمية مجاني ودون موعد مسبق.'},c:{en:'Nearest vaccine center',ar:'أقرب مركز تطعيم'},h:'#facilities',ic:'i-shield'},
    refill:{t:{en:'Request a refill online',ar:'اطلب تجديد الوصفة إلكترونيًا'},d:{en:'Repeat prescriptions for chronic conditions can be renewed through Sehhaty.',ar:'يمكن تجديد وصفات الأمراض المزمنة عبر تطبيق صحتي.'},c:{en:'Renew prescription',ar:'جدّد الوصفة'},h:'#',ic:'i-note'},
    home:{t:{en:'Request home healthcare',ar:'اطلب الرعاية المنزلية'},d:{en:'Our home care teams visit eligible patients after a referral.',ar:'تزور فرق الرعاية المنزلية المرضى المستحقين بعد التحويل.'},c:{en:'Check eligibility',ar:'تحقق من الأهلية'},h:'#',ic:'i-home'}
  };
  var WHO={me:null,child:{en:' Children are seen by pediatric teams.',ar:' يتابع الأطفالَ فريقُ طب الأطفال.'},parent:{en:' Seniors can ask about home care and chronic-care clinics.',ar:' يمكن لكبار السن السؤال عن الرعاية المنزلية وعيادات الأمراض المزمنة.'}};
  var nWho='me',nNeed='visit';
  function renderNav(){var r=NAV[nNeed],box=document.getElementById('navOut');if(!box)return;
    document.getElementById('navT').textContent=T(r.t);
    document.getElementById('navD').textContent=T(r.d)+(WHO[nWho]&&nNeed!=='urgent'?T(WHO[nWho]):'');
    var cta=document.getElementById('navCta');cta.textContent=T(r.c);cta.setAttribute('href',r.h);
    document.querySelector('#navIc use').setAttribute('href','#'+r.ic);
    var ic=document.getElementById('navIc');
    ic.className='grid h-10 w-10 shrink-0 place-items-center rounded-xl '+(r.red?'bg-emerg-50 text-emerg-600 dark:bg-emerg-500/15 dark:text-red-300':'bg-brand-50 text-brand-500 dark:bg-brand-500/15 dark:text-brand-300');
    cta.classList.toggle('!bg-emerg-600',!!r.red);cta.classList.toggle('!shadow-none',!!r.red)}
  function pick(sel,attr){document.querySelectorAll(sel).forEach(function(b){b.addEventListener('click',function(){document.querySelectorAll(sel).forEach(function(x){x.setAttribute('aria-pressed',x===b)});if(attr==='who')nWho=b.dataset.v;else nNeed=b.dataset.v;renderNav()})})}
  pick('.nv-who','who');pick('.nv-need','need');renderNav();window.hooks.push(renderNav);
  // ---------- ER wait board ----------
  var erCards=[].slice.call(document.querySelectorAll('#erGrid [data-w]')),erT=0;
  function erLvl(w){return w<30?{c:'bg-emerald-500',b:'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',l:{en:'Quiet',ar:'هادئ'}}:w<55?{c:'bg-accent-400',b:'bg-amber-50 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',l:{en:'Moderate',ar:'متوسط'}}:{c:'bg-highlight-500',b:'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',l:{en:'Busy',ar:'مزدحم'}}}
  function renderEr(){erCards.forEach(function(c){var w=+c.dataset.w,l=erLvl(w);c.querySelector('.er-min').textContent=w;var bar=c.querySelector('.er-bar');bar.className='er-bar block h-full rounded-full transition-all duration-700 '+l.c;bar.style.width=Math.min(100,w/90*100)+'%';var bd=c.querySelector('.er-badge');bd.className='er-badge whitespace-nowrap rounded-full px-2 py-0.5 text-[.7rem] font-bold '+l.b;bd.textContent=T(l.l)})}
  renderEr();window.hooks.push(renderEr);
  setInterval(function(){erT++;var u=document.getElementById('erUpd');if(u)u.textContent=(erT%15)+'s';if(erT%15===0&&!reduce){erCards.forEach(function(c){c.dataset.w=Math.max(8,Math.min(85,+c.dataset.w+Math.round((Math.random()-.5)*10)))});renderEr()}},1000);
  // ---------- journeys ----------
  document.querySelectorAll('#jTabs [role=tab]').forEach(function(t){t.addEventListener('click',function(){document.querySelectorAll('#jTabs [role=tab]').forEach(function(x){x.setAttribute('aria-selected',x===t)});document.querySelectorAll('.jp').forEach(function(p){var on=p.id===t.dataset.j;p.classList.toggle('hidden',!on);p.classList.toggle('grid',on)})})});
  // ---------- BMI ----------
  var hIn=document.getElementById('hIn'),wIn=document.getElementById('wIn');
  function bmi(){var h=+hIn.value,w=+wIn.value,b=w/Math.pow(h/100,2);document.getElementById('hV').textContent=h;document.getElementById('wV').textContent=w;document.getElementById('bmiV').textContent=b.toFixed(1);
    var cat=b<18.5?{l:{en:'Underweight',ar:'نقص في الوزن'},c:'bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300',t:{en:'A little below the healthy range. A nutrition consult can help you build up safely.',ar:'أقل قليلًا من النطاق الصحي. يمكن لاستشارة التغذية مساعدتك بأمان.'}}:b<25?{l:{en:'Healthy range',ar:'وزن صحي'},c:'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',t:{en:'You are in the healthy range. Keep it up with regular activity and a yearly check-up.',ar:'أنت في النطاق الصحي. حافظ عليه بالنشاط المنتظم والفحص السنوي.'}}:b<30?{l:{en:'Overweight',ar:'زيادة في الوزن'},c:'bg-amber-50 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',t:{en:'Slightly above the healthy range. Small changes to diet and activity make a real difference.',ar:'أعلى قليلًا من النطاق الصحي. تغييرات بسيطة في الغذاء والنشاط تصنع فرقًا.'}}:{l:{en:'Obesity range',ar:'سمنة'},c:'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',t:{en:'Talk to your family doctor about a healthy-weight program that suits you.',ar:'تحدث مع طبيب الأسرة عن برنامج وزن صحي يناسبك.'}};
    var el=document.getElementById('bmiC');el.className='mt-2 inline-block rounded-full px-3 py-1 text-sm font-bold '+cat.c;el.textContent=T(cat.l);document.getElementById('bmiTip').textContent=T(cat.t);
    document.getElementById('bmiM').style.left=Math.max(0,Math.min(100,(b-15)/25*100))+'%'}
  if(hIn){hIn.addEventListener('input',bmi);wIn.addEventListener('input',bmi);bmi();window.hooks.push(bmi)}
  // ---------- search palette ----------
  var sBox=document.getElementById('search'),sIn=document.getElementById('sIn');
  window.searchUi=function(o){sBox.classList.toggle('hidden',!o);document.body.style.overflow=o?'hidden':'';if(o){sIn.value='';sFilter();setTimeout(function(){sIn.focus()},30)}};
  function sFilter(){var q=sIn.value.trim().toLowerCase(),n=0;document.querySelectorAll('#sRes .s-i').forEach(function(a){var ok=!q||(a.dataset.k+' '+a.textContent).toLowerCase().indexOf(q)>=0;a.classList.toggle('hidden',!ok);a.classList.toggle('flex',ok);if(ok)n++});document.querySelectorAll('#sRes .s-h').forEach(function(h){h.classList.toggle('hidden',!!q)});document.getElementById('sNone').classList.toggle('hidden',n>0)}
  sIn.addEventListener('input',sFilter);
  document.querySelectorAll('#sRes .s-i').forEach(function(a){a.addEventListener('click',function(){searchUi(false)})});
  document.addEventListener('keydown',function(e){if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();searchUi(sBox.classList.contains('hidden'))}if(e.key==='Escape'){searchUi(false);chat(false)}});
  // ---------- Ask EHC (scripted demo) ----------
  var QS=[{q:{en:'Nearest ER',ar:'أقرب طوارئ'},a:{en:'The closest emergency department to you is Dammam Medical Complex (about 4 km, est. wait ~18 min). If it\'s life-threatening, call 997 right away.',ar:'أقرب قسم طوارئ لك هو مجمع الدمام الطبي (حوالي 4 كم، الانتظار المتوقع نحو 18 دقيقة). إذا كانت الحالة خطيرة اتصل على 997 فورًا.'}},
    {q:{en:'Book an appointment',ar:'حجز موعد'},a:{en:'You can book with your family doctor through the Sehhaty app, or call 937 any time. Would you like the nearest health center?',ar:'يمكنك الحجز مع طبيب الأسرة عبر تطبيق صحتي أو الاتصال على 937 في أي وقت. هل تريد أقرب مركز صحي؟'}},
    {q:{en:'Flu vaccine',ar:'لقاح الإنفلونزا'},a:{en:'The seasonal flu vaccine is free at every primary health center, no appointment needed.',ar:'لقاح الإنفلونزا الموسمية مجاني في جميع مراكز الرعاية الأولية دون موعد مسبق.'}},
    {q:{en:'Visiting hours',ar:'أوقات الزيارة'},a:{en:'Visiting hours vary by hospital and ward. Pick a facility and I\'ll show its hours.',ar:'تختلف أوقات الزيارة حسب المستشفى والقسم. اختر المنشأة وسأعرض لك أوقاتها.'}}];
  var log=document.getElementById('chatLog'),qsBox=document.getElementById('chatQs'),chatStarted=false;
  function bubble(txt,me){var d=document.createElement('div');d.className=me?'max-w-[85%] self-end rounded-2xl rounded-ee-md bg-brand-500 px-3.5 py-2.5 text-white':'max-w-[85%] self-start rounded-2xl rounded-es-md bg-soft px-3.5 py-2.5 text-deep-950 dark:bg-white/5 dark:text-slate-100';d.textContent=txt;log.appendChild(d);log.scrollTop=log.scrollHeight}
  function typing(cb){var d=document.createElement('div');d.className='self-start rounded-2xl bg-soft px-3.5 py-2.5 text-slate-400 dark:bg-white/5';d.textContent='•••';log.appendChild(d);log.scrollTop=log.scrollHeight;setTimeout(function(){d.remove();cb()},650)}
  function renderQs(){qsBox.innerHTML='';QS.forEach(function(x){var b=document.createElement('button');b.type='button';b.className='rounded-full border border-brand-200 px-3 py-1.5 text-xs font-semibold text-brand-600 hover:bg-brand-50 dark:border-brand-400/30 dark:text-brand-300 dark:hover:bg-white/5';b.textContent=T(x.q);b.onclick=function(){bubble(T(x.q),true);typing(function(){bubble(T(x.a))})};qsBox.appendChild(b)})}
  window.chat=function(o){var box=document.getElementById('chatBox');box.classList.toggle('open',o);document.getElementById('chatBtn').classList.toggle('!hidden',o);
    if(o&&!chatStarted){chatStarted=true;renderQs();typing(function(){bubble(isAr()?'مرحبًا! أنا مساعد تجمع الشرقية الصحي. كيف أساعدك اليوم؟':'Hi! I\'m the EHC assistant. How can I help you today?')})}};
  window.hooks.push(function(){if(chatStarted)renderQs()});
  document.getElementById('chatForm').addEventListener('submit',function(e){e.preventDefault();var i=document.getElementById('chatIn'),v=i.value.trim();if(!v)return;bubble(v,true);i.value='';typing(function(){bubble(isAr()?'هذه نسخة تجريبية بردود جاهزة. جرّب أحد الاقتراحات أدناه، أو اتصل على 937 للاستشارة الطبية.':'This is a demo with scripted replies. Try one of the suggestions below, or call 937 for medical advice.')})});
})();

  // ---------- preview-only: theme preset switcher + occasion greeting (Umbraco sets data-theme and the ribbon server-side) ----------
  (function(){var GREET={'national-day':['Happy Saudi National Day','يوم وطني سعيد'],'ramadan':['Ramadan Kareem. Clinic hours may change during Ramadan','رمضان كريم. قد تتغير أوقات العيادات خلال الشهر الفضيل'],'eid-al-fitr':['Eid Mubarak. See our Eid emergency and clinic hours','عيد مبارك. تعرّف على أوقات الطوارئ والعيادات خلال العيد'],'eid-al-adha':['Eid al-Adha Mubarak','عيد أضحى مبارك'],'hajj':['Hajj season: stay safe from heat. Drink water and avoid direct sun','موسم الحج: احمِ نفسك من الإجهاد الحراري. اشرب الماء وتجنّب الشمس المباشرة'],'pink-october':['Pink October: book your free screening','أكتوبر الوردي: احجزي فحصك المجاني']};
    var html=document.documentElement,cur='';function greet(){var g=document.getElementById('greet');if(!g)return;var m=GREET[cur];g.classList.toggle('hidden',!m);if(m)g.textContent=m[html.lang==='ar'?1:0]}
    document.querySelectorAll('[data-theme-preset]').forEach(function(b){b.addEventListener('click',function(){cur=b.getAttribute('data-theme-preset');if(cur)html.setAttribute('data-theme',cur);else html.removeAttribute('data-theme');
      document.querySelectorAll('[data-theme-preset]').forEach(function(x){x.setAttribute('aria-pressed',x===b)});greet()})});
    var lb=document.getElementById('langBtn');if(lb)lb.addEventListener('click',function(){setTimeout(greet,0)});})();
