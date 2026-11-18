// Exiled's Toolkit: the editor New Loop Plus' Talents tab and More Rewards' Rewards tab share, run in the game's
// root web document. It is sent with each tab's own script, ahead of it. A tab calls ExiledEditor.make(T) once and
// gets its own copy of the effect list, the effect and icon pickers, the level and text editors and the helpers they
// use. T is the tab's side: its state (st), its styles, names and messages, and the parts each tab draws its own way.
// The tab fills T before anything is drawn. A newer copy of this script replaces an older one; an older one never
// replaces a newer one.
(function () {
  if (window.ExiledEditor && window.ExiledEditor.version >= 1) return;

  window.ExiledEditor = { version: 1, make };

  function make(T) {
      const MAX_LEVELS = 10;

      const MAX_GIVES = 10;

      const GAME_PREFIX = 'game:';

      const REWARD_ICONS = 'rewards';

      // The icon picker's groups are the folders the game keeps its images in; these are the ones worth a friendlier name.
      const ICON_GROUPS = {
        rewards: ['Rewards', '奖励'],
        Food: ['Food', '食物'],
        Material: ['Materials', '材料'],
        Consumable: ['Consumables', '消耗品'],
        Furniture: ['Furniture', '家具'],
        Electrical: ['Electrical', '电器'],
        Structure: ['Structures', '建筑'],
        Vehicle: ['Vehicles', '载具'],
        RobotModule: ['Robot modules', '机器人模块'],
        Literature: ['Books', '书籍'],
        Renovation: ['Renovation', '装修'],
        Camp: ['Camp', '营地'],
        icon: ['Other', '其他']
      };

      const TOAST_MS = 2600;

      const INT_FIELD = /^(IntValue\d|Buff(Satiety|Morale|Stamina|Health|Vitality)Max|ItemEndBuff0\d|BuffEndBuff0\d|PonitEnd|ExposureValue|BuffAction)$/;

      const STAT_MAX = /^Buff(Stamina|Satiety|Morale|Health|Vitality)Max$/;

      const NO_SCRIPT = ['', 'Buff_Empty'];

      const VARS = '--hl:#FFC83A;--hl-glow:rgba(255,200,58,0.35);--tm:#f0e8d8;--td:#a89a82;--tmeta:#7a6e5a;--bl:rgba(220,188,120,0.16);--bd2:rgba(220,188,120,0.10);';

      function scopedCss() {
        return T.CSS.map(line => {
          const scopes = T.SCOPES[line.charAt(0)];
          const brace = line.indexOf('{');
          const selectors = line.slice(2, brace).split(',').map(s => s.trim());
          const scoped = [];

          for (const scope of scopes) {
            for (const s of selectors) scoped.push(s.charAt(0) === '&' ? scope + s.slice(1) : scope + ' ' + s);
          }

          return scoped.join(',') + line.slice(brace);
        }).join('\n');
      }

      function injectStyle() {
        if (document.getElementById(T.STYLE_ID)) return;

        const style = document.createElement('style');

        style.id = T.STYLE_ID;
        style.textContent = scopedCss();
        document.head.appendChild(style);
      }

      const isObj = v => v !== null && typeof v === 'object' && !Array.isArray(v);

      const isArr = Array.isArray;

      const isNum = v => typeof v === 'number' && isFinite(v);

      const clone = v => v === undefined ? undefined : JSON.parse(JSON.stringify(v));

      const round2 = v => Math.round(v * 100) / 100;

      const fmt = v => String(round2(isNum(v) ? v : 0));

      const range = n => Array.from({ length: Math.max(0, n) }, (_, i) => i);

      const L = (en, zh) => T.st.lang === 'zh' ? zh : en;

      const ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

      const esc = s => String(s == null ? '' : s).replace(/[&<>"']/g, c => ESCAPES[c]);

      const isEmptyVal = v => v == null || v === 0 || v === '' || v === false || (isArr(v) && v.length === 0);

      const zeroOf = v => isArr(v) ? [] : typeof v === 'string' ? '' : typeof v === 'boolean' ? false : 0;

      const allKeys = (a, b) => Array.from(new Set(Object.keys(a).concat(Object.keys(b))));

      // The file and the game leave zeros, empty text and false out, so those all count as "not set".
      function sameVal(a, b) {
        if (isEmptyVal(a) && isEmptyVal(b)) return true;

        if (isNum(a) && isNum(b)) return Math.abs(a - b) < 1e-6;

        return JSON.stringify(a) === JSON.stringify(b);
      }

      function h(html) {
        const t = document.createElement('template');

        t.innerHTML = html.trim();

        return t.content.firstElementChild;
      }

      // Text in the file is "plain" or { en, zh }; inside the tab it is always { en, zh }, or null when there is none.
      function normText(v) {
        if (typeof v === 'string') return v ? { en: v, zh: v } : null;

        if (!isObj(v)) return null;

        const en = typeof v.en === 'string' ? v.en : '';
        const zh = typeof v.zh === 'string' ? v.zh : '';

        return en || zh ? { en, zh } : null;
      }

      const textIn = (t, lang) => t ? (t[lang] || t.en || t.zh || '') : '';

      const shown = (own, fallback, lang) => textIn(own, lang) || textIn(fallback, lang);

      const CATS = [
        ['prep', 'Before the disaster', '灾前准备'],
        ['body', 'Body and needs', '身体与需求'],
        ['morale', 'Morale', '心态'],
        ['items', 'Items and supplies', '物资'],
        ['skills', 'Skills', '熟练度'],
        ['speed', 'Speed', '速度'],
        ['explore', 'Exploring', '探索'],
        ['home', 'Home and power', '居所与电力'],
        ['farm', 'Farming', '种植'],
        ['defense', 'Traps and defense', '陷阱与防御'],
        ['money', 'Money and trade', '金钱与交易'],
        ['loop', 'Loop', '轮回'],
        ['raw', 'Other game scripts', '其他游戏脚本']
      ].map(c => ({ id: c[0], en: c[1], zh: c[2] }));

      // Item groups the game's airdrop rewards use, named after those rewards.
      const GROUP_TALENTS = { 201: 3066, 202: 3067, 203: 3068, 204: 3069, 205: 3070, 206: 3071, 207: 3072, 208: 3073, 228: 3074, 222: 3077, 223: 3078, 224: 3079, 227: 3080 };

      const SKILL_TALENTS = [['Production', 4020], ['Plant', 4021], ['Cooking', 4022], ['Trap', 4023], ['Explore', 4024], ['Defense', 4025]];

      const XP_SKILLS = [['Production', 'Production'], ['Cook', 'Cooking'], ['Plant', 'Plant'], ['Trap', 'Trap'], ['Explore', 'Explore'], ['Defense', 'Defense']];

      const WORK = [['Production', 'Crafting', '制造'], ['Cook', 'Cooking', '烹饪'], ['Interact', 'Searching and using things', '搜索与交互'], ['Chore', 'Chores', '杂务']];

      const STATS = [['Stamina', 'Stamina', '精力'], ['Satiety', 'Satiety', '饱腹'], ['Morale', 'Morale', '心态'], ['Health', 'Health', '健康'], ['Vitality', 'Life', '生命']].map(s => ({ v: s[0], en: s[1], zh: s[2] }));

      const ROBOTS = [['HK', 'HK-1 sorting robot', 'HK-1 整理机器人'], ['TB', 'GR-3 farming robot', 'GR-3 种植机器人'], ['WX', 'FX-5 repair robot', 'FX-5 维修机器人']];

      const robotScript = v => 'EndlessRobotGrant_' + v;

      // "an HK-1 sorting robot" but "a GR-3 farming robot": the article follows how the letter is said.
      function robotName(v, zh, withArticle) {
        const r = ROBOTS.find(x => x[0] === v) || [v, v, v];
        const name = zh ? r[2] : r[1];

        if (!withArticle) return name;

        return (/^[AEFHILMNORSX]/.test(name) ? 'an ' : 'a ') + name;
      }

      // Filled from the game's reward names once the data arrives.
      const named = { groups: [], skills: [], xp: [], work: [], robots: [] };

      function nameOptions() {
        named.groups = Object.keys(GROUP_TALENTS).map(Number).filter(g => T.st.byId[GROUP_TALENTS[g]]).map(g => {
          const t = T.st.byId[GROUP_TALENTS[g]];

          return { v: g, en: t.name.en.replace(/^Airdrop - /, ''), zh: t.name.zh.split('\u00b7').pop() };
        });

        const skillLabel = (name, tid) => {
          const t = T.st.byId[tid];

          return t ? { en: t.name.en.replace(/ Memory$/, '') || name, zh: t.name.zh.replace(/记忆$/, '') || name } : { en: name, zh: name };
        };

        named.skills = SKILL_TALENTS.map(s => Object.assign({ v: s[0] }, skillLabel(s[0], s[1])));
        named.xp = XP_SKILLS.map(s => {
          const from = SKILL_TALENTS.find(k => k[0] === s[1]);

          return Object.assign({ v: s[0], script: 'AE_' + s[0] + 'ExpMultiplier' }, skillLabel(s[1], from ? from[1] : 0));
        });
        named.work = WORK.map(w => ({ v: w[0], en: w[1], zh: w[2], script: 'AE_' + w[0] + 'SpeedMultiplier' }));
        named.robots = ROBOTS.map(r => ({ v: r[0], en: r[1], zh: r[2], script: robotScript(r[0]) }));
      }

      // A number that can change per level. scale and add turn the file's raw value into what the player reads (0.1 -> 10%).
      function num(key, en, zh, unit, def, o) {
        return Object.assign({ type: 'num', key, en, zh, unit, def, step: 1, scale: 1, add: 0, min: 0 }, o);
      }

      const pct = (key, en, zh, def, o) => num(key, en, zh, ['%', '%'], def, Object.assign({ step: 5, scale: 100 }, o));

      // One value for the whole reward. A virtual pick is not a field: it chooses the script (a skill) or the field (a stat).
      function pick(key, en, zh, options, def, o) {
        return Object.assign({ type: 'pick', key, en, zh, options, def, virtual: false }, o);
      }

      // A field the effect needs at a fixed value; an effect row with another value is some other effect.
      const hide = (key, def) => ({ type: 'hide', key, def });

      const optionsOf = p => p.options();

      const isOptionOn = o => !o.script || T.st.scripts.has(o.script);

      const availableOptions = p => optionsOf(p).filter(isOptionOn);

      const groupUnknown = (v, zh) => (zh ? '物品组' : 'Group ') + v;

      function opt(p, v, zh) {
        const o = optionsOf(p).find(x => x.v === v);

        if (o) return zh ? o.zh : o.en;

        return p.unknown ? p.unknown(v, zh) : String(v == null ? '' : v);
      }

      const itemCount = (n, zh) => fmt(n) + (zh ? '件' : n === 1 ? ' item' : ' items');

      const plural = (n, word) => fmt(n) + ' ' + word + (n === 1 ? '' : 's');

      const numericBuffKeys = v => Object.keys(v).filter(k => /^Buff/.test(k) && isNum(v[k]) && v[k] !== 0);

      const FX = [];

      function fx(o) {
        FX.push(Object.assign({ usedBy: [], finishes: [], params: [], modes: 'both', pickable: true, raw: false }, o, { scripts: o.scripts || [o.script] }));
      }

      fx({ id: 'game:countdown', src: 'game', cat: 'prep', script: 'PreDisaster_AddCountDownTime',
        en: 'More time before the disaster', zh: '灾难前更多时间', what: ['Adds hours to the countdown before the disaster hits.', '增加灾难来临前的倒计时。'],
        params: [num('IntValue0', 'Extra time', '额外时间', ['h', '小时'], [1, 2, 3, 4, 7], { scale: 1 / 3600 })],
        line: (v, zh) => '+' + fmt(v.IntValue0) + (zh ? '小时' : 'h'),
        desc: (v, zh) => zh ? '提升灾难前醒来时间' + fmt(v.IntValue0) + '小时' : 'Gives you ' + plural(v.IntValue0, 'hour') + ' of extra time before the disaster hits.' });
      fx({ id: 'game:gold', src: 'game', cat: 'prep', script: 'PreDisaster_AddGold',
        en: 'Starting money', zh: '初始资金', what: ['Extra money before the disaster.', '灾前额外资金。'],
        params: [num('IntValue0', 'Extra money', '额外资金', ['$', '$'], [100, 200, 300, 400, 600], { step: 50 })],
        line: v => '+$' + fmt(v.IntValue0),
        desc: (v, zh) => zh ? '提升灾难前资金额度' + fmt(v.IntValue0) + '$' : 'Start with an extra $' + fmt(v.IntValue0) + ' before the disaster hits.' });
      fx({ id: 'game:weight', src: 'game', cat: 'prep', script: 'PreDisaster_AddWeight',
        en: 'Carry more when shopping', zh: '购物负重', what: ['More carry weight on pre-disaster shopping trips.', '提升灾前购物时的负重。'],
        params: [num('IntValue0', 'Extra weight', '额外负重', ['kg', 'KG'], [0.5, 1, 1.5, 2, 3], { step: 0.5, scale: 1 / 1200 })],
        line: v => '+' + fmt(v.IntValue0) + 'kg',
        desc: (v, zh) => zh ? '提升购物负重' + fmt(v.IntValue0) + 'KG' : 'Carry ' + fmt(v.IntValue0) + 'kg more weight.' });
      fx({ id: 'game:short-life-discount', src: 'game', cat: 'prep', script: 'PreDisaster_AddDiscount',
        en: 'Discount on short-life supplies', zh: '临期物资折扣', what: ['Cheaper supplies that spoil soon.', '购买即将过期的物资时打折。'],
        params: [pct('FloatValue1', 'Discount', '折扣', [10, 15, 20, 25, 30], { scale: -100, add: 100 }), num('FloatValue0', 'Shelf life under', '保质期小于', ['days', '天'], [5, 5, 5, 5, 5])],
        line: v => '-' + fmt(v.FloatValue1) + '%',
        desc: (v, zh) => zh ? '购买保质期小于' + fmt(v.FloatValue0) + '天的物资时，获得' + fmt(v.FloatValue1) + '%折扣' : 'Get a ' + fmt(v.FloatValue1) + '% discount when purchasing supplies with a shelf life of less than ' + plural(v.FloatValue0, 'day') + '.' });
      fx({ id: 'game:bulk-discount', src: 'game', cat: 'prep', script: 'BigVolume_Discount',
        en: 'Discount on big items', zh: '大件折扣', what: ['Cheaper supplies that take a lot of space.', '购买大体积物资时打折。'],
        params: [pct('FloatValue1', 'Discount', '折扣', [5, 10, 15, 20, 25], { scale: -100, add: 100 }), num('FloatValue0', 'Size at least', '体积至少', ['', ''], [6, 6, 6, 6, 6])],
        line: v => '-' + fmt(v.FloatValue1) + '%',
        desc: (v, zh) => zh ? '在购买大于等于' + fmt(v.FloatValue0) + '体积物资时，获得' + fmt(v.FloatValue1) + '%的价格折扣' : 'Get a ' + fmt(v.FloatValue1) + '% discount on supplies of size ' + fmt(v.FloatValue0) + ' or more.' });
      fx({ id: 'game:drive', src: 'game', cat: 'prep', script: 'VehicleTimeReduce',
        en: 'Drive and travel faster', zh: '载具驾驶', what: ['Unlocks buying a vehicle and cuts travel time.', '解锁购买车辆并减少行路时间。'],
        params: [pct('FloatValue0', 'Less travel time', '行路时间减少', [10, 20, 30, 30, 30])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '解锁购买车辆的能力，并减少' + fmt(v.FloatValue0) + '%的行路时间' : 'Unlocks buying a vehicle and cuts travel time by ' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:cabinet-food', src: 'game', cat: 'prep', script: 'PreDisaster_AddRandomItem',
        en: 'Food left in the cupboards', zh: '柜中遗留食物', what: ['Random food waiting in your home\'s cupboards.', '居所柜子里遗留随机食物。'],
        params: [hide('IntValue0', 301), num('IntValue1', 'Food items', '食物数量', ['items', '件'], [2, 3, 4, 5, 8])],
        line: (v, zh) => itemCount(v.IntValue1, zh),
        desc: (v, zh) => zh ? '在居所的柜子里，会遗留一些随机食物' + fmt(v.IntValue1) + '件' : 'Your home\'s cupboards hold ' + fmt(v.IntValue1) + ' random food items.' });
      fx({ id: 'game:loan', src: 'game', cat: 'money', script: 'LoanMaxBonus',
        en: 'Higher loan limit', zh: '借款上限', what: ['Borrow more before the disaster (the base limit is 500).', '提高灾前可借款上限（原500）。'],
        params: [num('IntValue0', 'Loan limit', '借款上限', ['', ''], [800, 1200, 1800], { step: 100, add: 500 })],
        line: v => fmt(v.IntValue0),
        desc: (v, zh) => zh ? '可借款上限提高至' + fmt(v.IntValue0) + '（原500）' : 'Loan limit increased to ' + fmt(v.IntValue0) + ' (from 500).' });
      fx({ id: 'game:furniture', src: 'game', cat: 'home', script: 'AE_FurnitureDiscount',
        en: 'Furniture discount', zh: '家具折扣', what: ['Cheaper furniture.', '购买家具打折。'],
        params: [pct('FloatValue0', 'Discount', '折扣', [5, 10, 15])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '购买家具时享' + fmt(v.FloatValue0) + '%折扣' : fmt(v.FloatValue0) + '% off furniture.' });
      fx({ id: 'game:sleep-stamina', src: 'game', cat: 'body', script: 'PostDisaster_AddEnergyEfficiency',
        en: 'More Stamina from sleep', zh: '睡眠恢复精力', what: ['Sleep restores Stamina faster.', '睡眠时精力恢复更快。'],
        params: [pct('FloatValue0', 'Stamina from sleep', '睡眠精力', [10, 20, 30, 40, 50])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '睡眠增加精力效率增加' + fmt(v.FloatValue0) + '%' : 'Increases Stamina recovery while sleeping by ' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:medicine', src: 'game', cat: 'body', script: 'PostDisaster_MedicalMaster',
        en: 'Stronger medicine', zh: '医疗增效', what: ['Medical items work better and treat worse illness.', '医疗物品效果提升，可治疗更严重疾病。'],
        params: [pct('FloatValue0', 'Medicine effect', '医疗效果', [10, 20, 30, 40, 50])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '医疗物品效果提升' + fmt(v.FloatValue0) + '%' : 'Medical items are ' + fmt(v.FloatValue0) + '% more effective.' });
      fx({ id: 'game:eat-satiety', src: 'game', cat: 'body', script: 'Efficient_Nutrition',
        en: 'More Satiety from food', zh: '进食饱腹', what: ['Every meal fills you more.', '进食获得更多饱腹。'],
        params: [pct('FloatValue0', 'Satiety from food', '进食饱腹', [15, 20, 25])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '进食获得的饱腹度+' + fmt(v.FloatValue0) + '%' : 'Food gives ' + fmt(v.FloatValue0) + '% more Satiety.' });
      fx({ id: 'game:staple-satiety', src: 'game', cat: 'body', script: 'Buff_304310_ComfortFood',
        en: 'More Satiety from staple food', zh: '主食饱腹', what: ['Staple foods fill you more.', '食用主食获得更多饱腹。'],
        params: [pct('FloatValue0', 'Satiety from staples', '主食饱腹', [12, 18, 24], { step: 2 })],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '食用主食类别食品时，获得的饱腹效果增加' + fmt(v.FloatValue0) + '%' : 'Eating Staple Foods grants ' + fmt(v.FloatValue0) + '% more Satiety.' });
      fx({ id: 'game:sleep-satiety', src: 'game', cat: 'body', script: 'Deep_Sleep',
        en: 'Less hunger while sleeping', zh: '睡眠饱腹下降减少', what: ['Satiety drops slower while you sleep.', '睡眠时饱腹下降更慢。'],
        params: [pct('FloatValue0', 'Slower Satiety drop', '饱腹下降减少', [15, 25, 35], { scale: -100 })],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '睡眠时饱腹度下降速度-' + fmt(v.FloatValue0) + '%' : 'Satiety drops ' + fmt(v.FloatValue0) + '% slower while sleeping.' });
      // The game heals Life through BuffVitalityStart; a reward made on BuffHealthStart keeps that field.
      fx({ id: 'game:heal-now', src: 'game', cat: 'body', script: 'Buff_Default_StartNotify',
        en: 'Restore Life at once', zh: '立即恢复生命', what: ['Restores Life the moment the reward is taken.', '获得奖励时立即恢复生命。'],
        params: [num('heal', 'Life restored', '恢复生命', ['', '点'], [45, 60, 80], { step: 5, dyn: g => g.field })],
        init: (g, tpl) => { g.field = !isEmptyVal(tpl.BuffHealthStart) && isEmptyVal(tpl.BuffVitalityStart) ? 'BuffHealthStart' : 'BuffVitalityStart'; },
        test: v => numericBuffKeys(v).every(k => k === 'BuffVitalityStart' || k === 'BuffHealthStart'),
        zeroOthers: (k, g) => /^Buff/.test(k) && k !== g.field,
        line: v => '+' + fmt(v.heal),
        desc: (v, zh) => zh ? '立即恢复' + fmt(v.heal) + '点生命值' : 'Restore ' + fmt(v.heal) + ' Life at once.' });
      fx({ id: 'game:stat-max', src: 'game', cat: 'body', script: 'Buff_Default',
        en: 'Raise a stat\'s maximum', zh: '提高属性上限', what: ['Raises the cap of Stamina, Satiety, Morale, Health or Life.', '提高精力、饱腹、心态、健康或生命的上限。'],
        params: [
          pick('stat', 'Stat', '属性', () => STATS, 'Stamina', { virtual: true, from: v => { const k = numericBuffKeys(v).find(x => STAT_MAX.test(x)); return k ? k.slice(4, -3) : 'Stamina'; } }),
          num('BuffMax', 'Extra maximum', '额外上限', ['', ''], [5, 10, 15, 20, 30], { step: 5, dyn: g => 'Buff' + g.pick.stat + 'Max' })
        ],
        test: v => { const keys = numericBuffKeys(v); return keys.length === 1 && STAT_MAX.test(keys[0]); },
        zeroOthers: (k, g) => /^Buff/.test(k) && k !== 'Buff' + g.pick.stat + 'Max',
        line: v => '+' + fmt(v.BuffMax),
        desc: (v, zh, p) => zh ? opt(p[0], v.stat, true) + '上限+' + fmt(v.BuffMax) : 'Max ' + opt(p[0], v.stat) + ' +' + fmt(v.BuffMax) + '.' });
      fx({ id: 'game:cold', src: 'game', cat: 'body', script: 'AE_ColdDebuffReduce',
        en: 'Resist the cold', zh: '耐寒', what: ['Cold snaps take less Satiety, Morale and Vitality.', '寒潮受冻时扣减的饱腹、心态、活力减少。'],
        params: [pct('FloatValue0', 'Less cold damage', '受冻减免', [25, 40, 50])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '寒潮受冻时，扣减的饱腹 / 心态 / 活力减免' + fmt(v.FloatValue0) + '%' : 'Cold snaps take ' + fmt(v.FloatValue0) + '% less Satiety, Morale and Vitality.' });
      fx({ id: 'game:morale-drop', src: 'game', cat: 'morale', script: 'PostDisaster_PsychologicalAdjustment',
        en: 'Morale drops slower', zh: '心态下降减少', what: ['Morale falls slower on its own.', '心态值自然下降更慢。'],
        params: [pct('FloatValue0', 'Slower Morale drop', '心态下降减少', [10, 20, 30, 40, 45], { scale: -100 })],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '心态值自然下降减少' + fmt(v.FloatValue0) + '%' : 'Morale drops ' + fmt(v.FloatValue0) + '% slower on its own.' });
      fx({ id: 'game:airdrop', src: 'game', cat: 'items', script: 'AddItemToBag_307510',
        en: 'Airdrop next day', zh: '次日空投', what: ['A radio call brings random items of one kind the next day.', '无线电呼救，第二天获得一类随机物品。'],
        params: [pick('IntValue0', 'What drops', '空投类别', () => named.groups, 201, { unknown: groupUnknown }), num('IntValue1', 'How many', '数量', ['items', '件'], [1, 2, 3])],
        line: (v, zh) => itemCount(v.IntValue1, zh),
        desc: (v, zh, p) => zh ? '无线电呼救请求紧急补给，第二天获得' + fmt(v.IntValue1) + '件随机' + opt(p[0], v.IntValue0, true) : 'A radio call brings ' + fmt(v.IntValue1) + ' random ' + opt(p[0], v.IntValue0) + ' the next day.' });
      fx({ id: 'game:scavenge', src: 'game', cat: 'items', script: 'Survival_Context',
        en: 'Random items each day', zh: '每日随机物品', what: ['Random items now, then more every day for a while.', '立即获得随机物品，之后每天再获得。'],
        params: [num('IntValue1', 'Items each day', '每天数量', ['items', '件'], [2, 3, 4])],
        line: (v, zh) => fmt(v.IntValue1) + (zh ? '件/天' : '/day'),
        desc: (v, zh) => zh ? '立即获得' + fmt(v.IntValue1) + '件随机物品，之后数天每天再获得' + fmt(v.IntValue1) + '件' : 'Get ' + fmt(v.IntValue1) + ' random items at once, then ' + fmt(v.IntValue1) + ' more each day for a few days.' });
      fx({ id: 'game:skill-start', src: 'game', cat: 'skills', script: 'EndlessProficiencyGrant', modes: 'endless',
        en: 'Start with a skill level', zh: '开局熟练度', what: ['A skill starts higher. Endless mode only.', '开局时某项熟练度提升。仅无尽模式。'],
        params: [pick('StrValue0', 'Skill', '熟练度', () => named.skills, 'Production'), num('IntValue0', 'Levels', '等级', ['Lv', '级'], [1, 2])],
        line: (v, zh) => '+' + fmt(v.IntValue0) + (zh ? ' 级' : ' Lv'),
        desc: (v, zh, p) => zh ? '开局时' + opt(p[0], v.StrValue0, true) + '熟练度提升 ' + fmt(v.IntValue0) + ' 级' : opt(p[0], v.StrValue0) + ' skill starts ' + plural(v.IntValue0, 'level') + ' higher.' });
      fx({ id: 'game:skill-xp', src: 'game', cat: 'skills', script: 'AE_CookExpMultiplier', scripts: XP_SKILLS.map(s => 'AE_' + s[0] + 'ExpMultiplier'),
        en: 'Learn a skill faster', zh: '熟练度经验加成', what: ['More experience for one skill.', '某项熟练度获得更多经验。'],
        params: [
          pick('skill', 'Skill', '熟练度', () => named.xp, 'Cook', { virtual: true, from: (v, script) => { const m = /^AE_(.*)ExpMultiplier$/.exec(script); return m ? m[1] : 'Cook'; } }),
          pct('FloatValue0', 'More experience', '经验加成', [25, 50, 75])
        ],
        scriptOf: g => 'AE_' + g.pick.skill + 'ExpMultiplier',
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh, p) => zh ? opt(p[0], v.skill, true) + '经验+' + fmt(v.FloatValue0) + '%' : opt(p[0], v.skill) + ' experience +' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:work-speed', src: 'game', cat: 'speed', script: 'AE_ProductionSpeedMultiplier', scripts: WORK.map(w => 'AE_' + w[0] + 'SpeedMultiplier'),
        en: 'Work faster', zh: '工作加速', what: ['One kind of work takes less time.', '某类工作耗时更短。'],
        params: [
          pick('work', 'Work', '工作', () => named.work, 'Production', { virtual: true, from: (v, script) => { const m = /^AE_(.*)SpeedMultiplier$/.exec(script); return m ? m[1] : 'Production'; } }),
          pct('FloatValue0', 'Faster by', '加速', [10, 20, 30])
        ],
        scriptOf: g => 'AE_' + g.pick.work + 'SpeedMultiplier',
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh, p) => zh ? opt(p[0], v.work, true) + '速度+' + fmt(v.FloatValue0) + '%' : opt(p[0], v.work) + ' ' + fmt(v.FloatValue0) + '% faster.' });
      fx({ id: 'game:night-move', src: 'game', cat: 'speed', script: 'DayNightMovement',
        en: 'Move faster at night', zh: '夜间移动加速', what: ['Faster walking at night.', '夜间移动速度增加。'],
        params: [pct('FloatValue0', 'Faster by', '加速', [30, 40, 50])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '夜间移动速度增加' + fmt(v.FloatValue0) + '%' : 'Move ' + fmt(v.FloatValue0) + '% faster at night.' });
      fx({ id: 'game:power-use', src: 'game', cat: 'home', script: 'AE_PowerCostReduce',
        en: 'Use less power', zh: '降低耗电', what: ['Every powered device draws less.', '用电设备总耗电降低。'],
        params: [pct('FloatValue0', 'Less power', '耗电降低', [10, 15, 20])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '用电设备总耗电降低' + fmt(v.FloatValue0) + '%' : 'Powered devices use ' + fmt(v.FloatValue0) + '% less power.' });
      fx({ id: 'game:solar', src: 'game', cat: 'home', script: 'AE_PowerSolarEfficiency',
        en: 'Better solar panels', zh: '太阳能增效', what: ['Solar panels make more power.', '太阳能板产电效率提高。'],
        params: [pct('FloatValue0', 'More power', '产电提高', [20, 30, 40])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '太阳能板产电效率提高' + fmt(v.FloatValue0) + '%' : 'Solar panel output increased by ' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:fridge', src: 'game', cat: 'home', script: 'AE_FridgeColdEnhance',
        en: 'Fridge keeps food longer', zh: '冰箱保鲜', what: ['Food in the fridge spoils slower.', '冰箱内食物变质更慢。'],
        params: [pct('FloatValue0', 'Slower spoiling', '变质减慢', [15, 30, 45])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '冰箱内食物变质速度再降低' + fmt(v.FloatValue0) + '%' : 'Food in the fridge spoils ' + fmt(v.FloatValue0) + '% slower.' });
      fx({ id: 'game:repair', src: 'game', cat: 'home', script: 'Buff_305510',
        en: 'Repair doors and windows better', zh: '修理门窗增效', what: ['Each repair restores more.', '每次修理恢复更多。'],
        params: [pct('FloatValue0', 'More per repair', '修理加成', [15, 30, 45, 60, 75])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '修理门窗效率提升' + fmt(v.FloatValue0) + '%' : 'Repairs to windows and doors restore ' + fmt(v.FloatValue0) + '% more.' });
      fx({ id: 'game:harvest', src: 'game', cat: 'farm', script: 'AE_HarvestWindowExtend',
        en: 'Longer harvest window', zh: '延长收获窗口', what: ['Ripe crops wait longer before they rot.', '作物成熟后更晚腐烂。'],
        params: [pct('FloatValue0', 'Longer by', '延长', [30, 60, 100], { step: 10 })],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '作物成熟后的收获窗口延长' + fmt(v.FloatValue0) + '%' : 'Harvest window extended by ' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:no-till', src: 'game', cat: 'farm', script: 'AE_SkipTillSoil',
        en: 'No tilling', zh: '免耕', what: ['Plant without tilling the soil first. No numbers.', '无需翻土即可种植。没有数值。'],
        params: [],
        line: (v, zh) => zh ? '免耕' : 'No tilling',
        desc: (v, zh) => zh ? '种植前无需翻土' : 'Plant without tilling the soil first.' });
      fx({ id: 'game:bite', src: 'game', cat: 'defense', script: 'AE_TowerBiteDamageReduce',
        en: 'Defenses take less bite damage', zh: '防御设施减伤', what: ['Bites hurt your defense structures less.', '防御设施受到的啃咬伤害降低。'],
        params: [pct('FloatValue0', 'Less damage', '伤害降低', [30, 40, 50])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '防御设施受到的啃咬伤害降低 ' + fmt(v.FloatValue0) + '%' : 'Bite damage taken by defense structures reduced by ' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:trap-catch', src: 'game', cat: 'defense', script: 'AE_TrapCaptureMultiplier',
        en: 'Traps catch more', zh: '陷阱捕获加成', what: ['Better odds of a catch.', '陷阱捕获几率提高。'],
        params: [pct('FloatValue0', 'Better odds', '捕获加成', [10, 20, 30])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '陷阱捕获几率+' + fmt(v.FloatValue0) + '%' : 'Traps catch ' + fmt(v.FloatValue0) + '% more often.' });
      fx({ id: 'game:trade', src: 'game', cat: 'money', script: 'AE_StrangerTradeAppraisalBonus',
        en: 'Better trades with strangers', zh: '交易估值提升', what: ['Strangers value what you offer higher.', '与陌生人交易时物品估值提升。'],
        params: [pct('FloatValue0', 'Higher value', '估值提升', [10, 20, 30])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '与陌生人交易时，给出物品的估值提升' + fmt(v.FloatValue0) + '%，更易成交' : 'Items you offer strangers are valued ' + fmt(v.FloatValue0) + '% higher.' });
      fx({ id: 'game:drone-trips', src: 'game', cat: 'money', script: 'AE_StrangerTradeDroneExtraTrip',
        en: 'More drone trades a day', zh: '无人机交易次数', what: ['The trade drone can trade more times each day.', '无人机每日可交易次数增加。'],
        params: [num('FloatValue0', 'More trades', '额外次数', ['', '次'], [1, 2], { min: 0 })],
        line: v => '+' + fmt(v.FloatValue0),
        desc: (v, zh) => zh ? '无人机每日可交易次数+' + fmt(v.FloatValue0) : 'The trade drone can make ' + plural(v.FloatValue0, 'more trade') + ' a day.' });
      fx({ id: 'game:brew-speed', src: 'game', cat: 'body', script: 'AE_BrewSpeedUp',
        en: 'Faster brewing', zh: '酿酒加速', what: ['Brewing takes less time.', '酿酒所需时间缩短。'],
        params: [pct('FloatValue0', 'Less time', '时间缩短', [10, 20, 30])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '酿酒所需时间缩短' + fmt(v.FloatValue0) + '%' : 'Brewing takes ' + fmt(v.FloatValue0) + '% less time.' });
      fx({ id: 'game:brew-yield', src: 'game', cat: 'body', script: 'AE_BrewBottleYield',
        en: 'More bottles per brew', zh: '酿酒出瓶更多', what: ['Each bottle needs less Satiety, so a brew gives more bottles.', '酿酒时每瓶所需的饱腹度降低，能酿出更多酒。'],
        params: [pct('FloatValue0', 'Less per bottle', '每瓶降低', [10, 20, 30])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '酿酒时每瓶所需的饱腹度降低' + fmt(v.FloatValue0) + '%' : 'Each bottle needs ' + fmt(v.FloatValue0) + '% less Satiety when brewing.' });
      fx({ id: 'game:settle-discount', src: 'game', cat: 'money', script: 'SettlementDiscount',
        en: 'Cheaper end-of-day rewards', zh: '结算奖励折扣', what: ['Rewards bought at settlement cost less.', '结算时购买奖励更便宜。'],
        params: [pct('FloatValue0', 'Discount', '折扣', [15, 20, 25])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '结算时购买奖励选项享受' + fmt(v.FloatValue0) + '%折扣' : fmt(v.FloatValue0) + '% off rewards bought at settlement.' });
      fx({ id: 'game:savings', src: 'game', cat: 'money', script: 'Savings_Plan',
        en: 'More Survival Points each day', zh: '每日生存点加成', what: ['Each day pays more Survival Points.', '每天获得的生存点增加。'],
        params: [pct('FloatValue0', 'More points', '生存点加成', [5, 10, 15])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '每天获得的生存点增加' + fmt(v.FloatValue0) + '%' : 'Daily Survival Points +' + fmt(v.FloatValue0) + '%.' });
      fx({ id: 'game:backpack-life', src: 'game', cat: 'items', script: 'AE_BackpackShelfLife',
        en: 'Backpack keeps food longer', zh: '背包保鲜', what: ['Items in your backpack spoil slower.', '随身背包里的物品保质期延长。'],
        params: [num('FloatValue0', 'Shelf life', '保质期', ['x', '倍'], [2, 3, 4], { add: 1, min: 1, step: 0.5 })],
        line: (v, zh) => fmt(v.FloatValue0) + (zh ? '倍' : 'x'),
        desc: (v, zh) => zh ? '随身背包里的物品保质期延长至' + fmt(v.FloatValue0) + '倍' : 'Items in your backpack keep ' + fmt(v.FloatValue0) + 'x as long.' });
      fx({ id: 'game:quick-rest', src: 'game', cat: 'body', script: 'QuickRest',
        en: 'Rest restores Stamina faster', zh: '休息恢复加快', what: ['Resting brings Stamina back faster.', '通过休息恢复精力更快。'],
        params: [pct('FloatValue0', 'Faster by', '加快', [10, 20, 30])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '通过休息恢复精力的速度加快' + fmt(v.FloatValue0) + '%' : 'Resting restores Stamina ' + fmt(v.FloatValue0) + '% faster.' });
      fx({ id: 'game:press-nutrition', src: 'game', cat: 'body', script: 'AE_CompressValueRate',
        en: 'Ration Press keeps more nutrition', zh: '压制保留更多营养', what: ['The Ration Press keeps more of the food\'s nutrition (normally 60%).', '使用口粮压制机时保留更多营养（原本为 60%）。'],
        params: [pct('FloatValue0', 'More kept', '多保留', [5, 10, 20])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '使用口粮压制机时，营养保留从 60% 提高到 ' + fmt(60 + v.FloatValue0) + '%' : 'The Ration Press keeps ' + fmt(60 + v.FloatValue0) + '% of the food\'s nutrition, up from 60%.' });
      fx({ id: 'game:exposure', src: 'game', cat: 'explore', script: 'AE_ExposureReduceMultiplier',
        en: 'Exposure builds slower', zh: '暴露值增长降低', what: ['Exposure builds up slower while exploring.', '探索时暴露值增长更慢。'],
        params: [pct('FloatValue0', 'Slower by', '降低', [10, 20, 30], { scale: -100 })],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '探索时暴露值增长-' + fmt(v.FloatValue0) + '%' : 'Exposure builds up ' + fmt(v.FloatValue0) + '% slower while exploring.' });
      fx({ id: 'game:robot', src: 'game', cat: 'home', script: robotScript('HK'), scripts: ROBOTS.map(r => robotScript(r[0])),
        en: 'Start with a robot', zh: '开局机器人', what: ['A robot and all its crafting blueprints from the start of the run. No numbers.', '开局获得一台机器人及其全部制造图纸。没有数值。'],
        params: [pick('robot', 'Robot', '机器人', () => named.robots, 'HK', { virtual: true, from: (v, script) => { const m = /^EndlessRobotGrant_(.*)$/.exec(script); return m ? m[1] : 'HK'; } })],
        scriptOf: g => robotScript(g.pick.robot),
        line: (v, zh) => robotName(v.robot, zh).split(' ')[0] + (zh ? ' 机器人' : ' robot'),
        desc: (v, zh) => zh ? '开局获得一台' + robotName(v.robot, true) + '，并掌握其全部制造图纸' : 'Start the run with ' + robotName(v.robot, false, true) + ' and all its crafting blueprints.' });
      const FX_BY_ID = {};

      const picksOf = f => f.params.filter(p => p.type === 'pick');

      const numsOf = f => f.params.filter(p => p.type === 'num');

      const hidesOf = f => f.params.filter(p => p.type === 'hide');

      const fieldName = (g, p) => p.dyn ? p.dyn(g) : p.key;

      const display = (p, raw) => (isNum(raw) ? raw : 0) * p.scale + p.add;

      function toRaw(p, field, shownValue) {
        const raw = (shownValue - p.add) / p.scale;

        return INT_FIELD.test(field) ? Math.round(raw) : Math.round(raw * 1e6) / 1e6;
      }

      // A level past the effect's own defaults keeps climbing by the last step.
      function defaultAt(p, i) {
        const def = p.def;
        const last = def[def.length - 1];
        const step = def.length > 1 ? last - def[def.length - 2] : 0;
        const v = i < def.length ? def[i] : last + step * (i - def.length + 1);

        return Math.max(p.min, round2(v));
      }

      function match(script, values) {
        const found = FX.find(f => f.pickable && T.isAvailable(f) && f.scripts.indexOf(script) >= 0 &&
          hidesOf(f).every(p => sameVal(values[p.key], p.def)) && (!f.test || f.test(values)));

        return found || rawFx(script);
      }

      const rawCache = {};

      const FIELD_RANK = k => /^(IntValue|FloatValue)\d/.test(k) ? 0 : /^Buff/.test(k) ? 1 : 2;

      function prettyScript(script) {
        return script.replace(/^(AE_|PostDisaster_|PreDisaster_|Buff_)/, '').replace(/_?\d{4,}_?/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ').trim() || script;
      }

      function rawLabel(k) {
        const m = /^(IntValue|FloatValue)(\d+)$/.exec(k);

        if (!m) return { en: k, zh: k };

        return m[1] === 'IntValue' ? { en: 'Whole number ' + m[2], zh: '整数 ' + m[2] } : { en: 'Number ' + m[2], zh: '数值 ' + m[2] };
      }

      // A game script with no friendly name: its numbers are the raw fields the game's rewards set.
      function rawFx(script) {
        if (rawCache[script]) return rawCache[script];

        const users = T.st.game.filter(t => t.levels.some(l => l.script === script));
        const levels = [].concat.apply([], users.map(t => t.levels.filter(l => l.script === script)));
        const seen = new Set();

        levels.forEach(l => Object.keys(l.values).forEach(k => { if (isNum(l.values[k])) seen.add(k); }));

        const keys = Array.from(seen).sort((a, b) => FIELD_RANK(a) - FIELD_RANK(b) || (a < b ? -1 : a > b ? 1 : 0)).slice(0, 4);
        const firstLevels = users.length ? users[0].levels.filter(l => l.script === script) : [];
        const params = keys.map(k => {
          const all = levels.map(l => l.values[k]).filter(isNum);
          const label = rawLabel(k);

          return num(k, label.en, label.zh, ['', ''], firstLevels.length ? firstLevels.map(l => isNum(l.values[k]) ? l.values[k] : 0) : [0], {
            step: all.every(v => Math.floor(v) === v) ? 1 : 0.05,
            min: -Infinity
          });
        });
        const name = prettyScript(script);
        const made = {
          id: 'raw:' + script, src: 'game', cat: 'raw', script, scripts: [script], raw: true, pickable: true, modes: 'both',
          usedBy: [], finishes: [], en: name, zh: name, what: ['The game\'s script, with its raw numbers.', '游戏脚本，使用原始数值。'],
          params,
          line: v => keys.map(k => fmt(v[k])).join(', '),
          desc: (v, zh) => params.map(p => (zh ? p.zh : p.en) + ' ' + fmt(v[p.key])).join(zh ? '，' : ', ')
        };

        rawCache[script] = made;

        return made;
      }

      const cat = { list: [], icons: [], defaultIcon: { icon: '', unityIcon: '' } };

      // The talents' own images, for a plugin that sends no icon list of its own.
      function talentIcons() {
        const seen = new Set();
        const icons = [];

        T.st.game.forEach(t => {
          if (!t.icon || seen.has(t.icon)) return;

          seen.add(t.icon);
          icons.push({ icon: t.icon, unityIcon: t.unityIcon, group: REWARD_ICONS });
        });

        return icons;
      }

      function fxIcon(f) {
        if (f.icon) return f.icon;

        const tid = f.usedBy[0] || f.finishes[0];
        const t = tid ? T.st.byId[tid] : null;

        return t && t.icon ? t.icon : cat.defaultIcon.icon;
      }

      const fxName = (f, zh) => zh ? f.zh : f.en;

      const fromRoot = path => String(path || '').replace(/^(\.\.\/)+/, '');

      // A New Loop Plus effect runs no game script, so it has no game row to start from: ''.
      const scriptOfGive = give => give.effect.indexOf(GAME_PREFIX) === 0 ? give.effect.slice(GAME_PREFIX.length) : '';

      // Fields the effect does not show but this reward sets apart from the row the plugin copies; kept as they are.
      function fixedFrom(g, v0) {
        const f = g.fx;
        const covered = new Set(numsOf(f).map(p => fieldName(g, p)).concat(hidesOf(f).map(p => p.key), picksOf(f).filter(p => !p.virtual).map(p => p.key)));
        const tpls = [T.tplValues(g.script, g.tid, 1), T.firstTpl(g.script)];
        const keys = new Set(Object.keys(v0));
        const fixed = {};

        tpls.forEach(t => Object.keys(t).forEach(k => keys.add(k)));
        keys.forEach(k => {
          if (covered.has(k)) return;

          if (f.zeroOthers && f.zeroOthers(k, g) && isEmptyVal(v0[k])) return;

          if (!tpls.some(t => !sameVal(t[k], v0[k]))) return;

          const tpl = tpls.find(t => k in t);

          fixed[k] = k in v0 ? clone(v0[k]) : zeroOf(tpl ? tpl[k] : 0);
        });

        return fixed;
      }

      // A give the tab may edit is one it would write back exactly as it found it.
      function roundTrips(g, vals, count) {
        const out = T.writeGive(g, count);

        return range(count).every(i => {
          const made = T.effectValues(out, g.tid, i);
          const want = vals[Math.min(i, vals.length - 1)];

          return allKeys(made, want).every(k => sameVal(made[k], want[k]));
        });
      }

      const isEditable = e => !e.frozen;

      const isAutoOn = e => e.autoText && e.gives.length > 0 && e.gives.every(g => !g.fx.raw) && !e.givesLocked;

      const showsAuto = e => isAutoOn(e) && e.touched;

      function valsOf(g, i) {
        const v = Object.assign({}, g.pick);

        numsOf(g.fx).forEach(p => {
          const per = g.per[p.key];

          v[p.key] = per[Math.min(i, per.length - 1)];
        });

        return v;
      }

      const autoLine = (g, i, zh) => g.fx.line(valsOf(g, i), zh);

      function autoLevelText(e, zh) {
        return range(e.levels.length).map(i => e.gives.map(g => autoLine(g, i, zh)).join(zh ? '，' : ', ')).join(' / ');
      }

      function autoDesc(e, i, zh) {
        return e.gives.map(g => g.fx.desc(valsOf(g, i), zh, g.fx.params)).join(zh ? '；' : ' ');
      }

      // A tab sets T.levelsAddUp when each level is a reward of its own that adds to the ones below it (More Rewards).
      // Then a level's total is the sum of its numbers and every level's before it. A number with an offset (a
      // discount, a multiplier) does not add that way, so a reward with one shows no totals.
      function addsUp(e) {
        if (!T.levelsAddUp || e.givesLocked || !e.gives.length) return false;

        return e.gives.every(g => !g.fx.raw && numsOf(g.fx).every(p => !p.add)) && e.gives.some(g => numsOf(g.fx).length > 0);
      }

      function totalVals(g, i) {
        const v = Object.assign({}, g.pick);

        numsOf(g.fx).forEach(p => {
          const per = g.per[p.key];

          v[p.key] = round2(range(i + 1).reduce((sum, j) => sum + per[Math.min(j, per.length - 1)], 0));
        });

        return v;
      }

      const totalText = (e, i, zh) => e.gives.map(g => g.fx.line(totalVals(g, i), zh)).join(zh ? '，' : ', ');

      const baseText = (e, key) => e.base ? e.base[key] : null;

      const nameOf = (e, lang) => shown(e.name, baseText(e, 'name'), lang) || ('#' + e.id);

      function descOf(e, i, lang) {
        const l = e.levels[i];

        if (showsAuto(e)) return autoDesc(e, i, lang === 'zh');

        if (!l) return '';

        return textIn(l.desc, lang) || textIn(e.description, lang) || textIn(l.gameDesc, lang);
      }

      const hasLevelDescs = e => e.levels.some(l => !!l.desc);

      const entryOf = id => T.st.entries.find(e => e.id === id && !e.removed) || null;

      const entryOrGhost = id => entryOf(id) || T.ghostOf(id);

      function levelCount(id) {
        const e = entryOrGhost(id);

        return e ? e.levels.length : 0;
      }

      function markChanged(e) {
        e.touched = true;
        T.st.dirty = true;
      }

      function givesChanged(e) {
        e.givesTouched = true;
        markChanged(e);

        if (!e.autoManual && !e.autoText) e.autoText = e.gives.every(g => !g.fx.raw);
      }

      // Turns a game reward that is not in the file into one that is, so it can be edited.
      function adopt(id) {
        const e = T.ghostOf(id);

        if (!e) return null;

        delete T.st.ghosts[id];
        T.st.entries.push(e);
        markChanged(e);

        return e;
      }

      // Reset drops this tab's changes to a game reward; a reward of your own is deleted. Either way it leaves the file.
      function resetOrDelete(e) {
        if (e.rawIndex >= 0) e.removed = true;
        else T.st.entries.splice(T.st.entries.indexOf(e), 1);

        T.st.dirty = true;
      }

      const nextStep = arr => {
        const n = arr.length;
        const last = arr[n - 1];

        return n > 1 ? last + (last - arr[n - 2]) : last;
      };

      function setGive(e, f, gi) {
        const g = T.makeGive(f, '', e.id, null, e.levels.length);

        if (gi >= 0 && gi < e.gives.length) e.gives[gi] = g;
        else e.gives.push(g);

        givesChanged(e);

        return g;
      }

      // A pick that chooses the script (a skill) or the field (a stat) starts the effect's row afresh.
      function setPick(e, g, key, value) {
        const p = picksOf(g.fx).find(x => x.key === key);

        if (!p) return;

        g.pick[key] = value;

        if (p.virtual) {
          g.script = g.fx.scriptOf ? g.fx.scriptOf(g) : g.script;
          g.fixed = {};

          if (g.fx.init) g.fx.init(g, T.tplValues(g.script, g.tid, 1));
        }

        givesChanged(e);
      }

      function writeText(own, fallback) {
        const fb = fallback || { en: '', zh: '' };
        const en = (own && own.en) || fb.en || (own && own.zh) || '';
        const zh = (own && own.zh) || fb.zh || (own && own.en) || '';

        return en || zh ? { en, zh } : undefined;
      }

      function saved(ok, msg) {
        if (ok && T.st.pending) {
          T.st.file = T.st.pending;
          T.buildEntries(T.st.sel);
        }

        T.st.pending = null;
        T.toast(esc(msg || ''));
        T.render();
      }

      function discard() {
        T.buildEntries(T.st.sel);
        T.render();
      }

      function send(msg) {
        try { window.vuplex.postMessage(T.PREFIX + msg); } catch (e) {}
      }

      const numText = v => String(Math.round((isNum(v) ? v : 0) * 10000) / 10000);

      const unitOf = p => p.unit ? (T.st.lang === 'zh' ? p.unit[1] : p.unit[0]) : '';

      const tag = (cls, en, zh) => '<span class="tg ' + cls + '">' + L(en, zh) + '</span>';

      const isGhost = e => !!e && !entryOf(e.id);

      // The list: every reward or only the changed ones, then only those the search finds. They come in number order,
      // or in the order the tab sets with T.compareIds, so Up, Down and Enter in the search box follow the rows
      // the way they are drawn.
      const byNumber = (a, b) => a - b;

      // The rewards the view holds before the search: every reward, or only the file's.
      const viewIds = () => T.st.view === 'all' ? T.allIds() : T.st.entries.filter(e => !e.removed).map(e => e.id);

      function listIds() {
        const ids = viewIds();
        const words = searchWords();
        const found = words.length ? ids.filter(id => matchesSearch(id, words)) : ids;

        return found.slice().sort(T.compareIds || byNumber);
      }

      // ---- Searching the list ----

      const searchWords = () => String(T.st.q || '').toLowerCase().split(/\s+/).filter(w => w);

      // A search looks through the reward's number, its name, every level's text and what it gives, in both languages.
      function searchText(id) {
        const e = entryOrGhost(id);

        if (!e) return String(id);

        const parts = [String(id), nameOf(e, 'en'), nameOf(e, 'zh')];

        range(e.levels.length).forEach(i => parts.push(descOf(e, i, 'en'), descOf(e, i, 'zh')));
        e.gives.forEach(g => parts.push(fxName(g.fx, false), fxName(g.fx, true)));

        if (T.searchExtra) parts.push(T.searchExtra(e));

        return parts.join(' ').toLowerCase();
      }

      function matchesSearch(id, words) {
        const text = searchText(id);

        return words.every(w => text.indexOf(w) >= 0);
      }

      const SEARCH_ICON = '<svg width="15" height="15" viewBox="0 0 16 16" aria-hidden="true"><circle cx="6.5" cy="6.5" r="5" fill="none" stroke="currentColor" stroke-width="2"/><path d="M10.5 10.5l4.5 4.5" stroke="currentColor" stroke-width="2"/></svg>';

      function searchHtml() {
        const q = T.st.q || '';

        return '<div class="srch">' + SEARCH_ICON +
          '<input data-a="rq" value="' + esc(q) + '" placeholder="' + esc(L('Search names and descriptions', '搜索名称和描述')) + '" spellcheck="false" autocomplete="off">' +
          '<span class="clr' + (q ? '' : ' is-hid') + '" data-a="rqx" title="' + esc(L('Clear the search', '清除搜索')) + '">&#x2715;</span></div>';
      }

      // Shown in place of the rows when the search finds nothing.
      function noMatchHtml() {
        const q = esc(T.st.q || '');
        const all = T.st.view !== 'all' ? '<br><span class="gbtn dim" data-a="view" data-v="all">' + L('SEARCH ALL REWARDS', '搜索全部奖励') + '</span>' : '';

        return '<div class="nomatch">' + L('Nothing matches "<b>' + q + '</b>".', '没有找到「<b>' + q + '</b>」。') + all + '</div>';
      }

      // Typing in the search box redraws only the list, so the box keeps its focus and what is typed in it.
      function refreshList() {
        const root = T.st.root;
        const list = root ? root.querySelector('[data-list]') : null;
        const clr = root ? root.querySelector('.srch .clr') : null;

        if (clr) clr.classList.toggle('is-hid', !T.st.q);

        if (!list) return;

        list.innerHTML = T.rowsHtml();
        list.scrollTop = 0;
      }

      function clearSearch() {
        const input = T.st.root ? T.st.root.querySelector('[data-a="rq"]') : null;

        T.st.q = '';

        if (input) input.value = '';

        refreshList();
        focusSearch();
      }

      function focusSearch() {
        const input = T.st.root ? T.st.root.querySelector('[data-a="rq"]') : null;

        if (!input) return;

        input.focus();
        input.setSelectionRange(input.value.length, input.value.length);
      }

      // Scrolls the list just enough to show the selected row below its sticky heading. Measured on screen, since the
      // row's offsetTop counts from whichever parent is positioned, which is not always the list.
      function showSelectedRow() {
        const list = T.st.root ? T.st.root.querySelector('[data-list]') : null;
        const row = list ? list.querySelector('.is-sel') : null;

        if (!row) return;

        const head = list.querySelector('.col-h');
        const listBox = list.getBoundingClientRect();
        const rowBox = row.getBoundingClientRect();
        const top = listBox.top + (head ? head.offsetHeight : 0);

        if (rowBox.top < top) list.scrollTop -= top - rowBox.top;
        else if (rowBox.bottom > listBox.bottom) list.scrollTop += rowBox.bottom - listBox.bottom;
      }

      // The list and the editor reach down to the bottom of the Mod Manager pane, whatever its size, so nothing sits
      // under its footer and the pane never scrolls on its own. Call after each redraw.
      const MIN_FIT_H = 300;

      // The attributes that tell one field from another, so the cursor can go back to the same one after a redraw.
      const FOCUS_ATTRS = ['data-a', 'data-k', 'data-c', 'data-v'];

      function fitHeight() {
        const root = T.st.root;
        const pane = root ? root.closest('.pane') : null;
        const ed = root ? root.querySelector('.ed') : null;

        if (!pane || !ed) return;

        const edTop = ed.getBoundingClientRect().top - pane.getBoundingClientRect().top + pane.scrollTop;
        const padBottom = parseFloat(getComputedStyle(root).paddingBottom) || 0;

        root.style.setProperty('--fit-h', Math.max(MIN_FIT_H, Math.floor(pane.clientHeight - edTop - padBottom)) + 'px');
      }

      // Refits when the pane changes size: the game window, or the Mod Manager showing its restart panel.
      function watchHeight() {
        const pane = T.st.root ? T.st.root.closest('.pane') : null;
        const view = pane ? pane.ownerDocument.defaultView : null;

        unwatchHeight();

        if (!view || typeof view.ResizeObserver !== 'function') return;

        T.st.fitWatch = new view.ResizeObserver(fitHeight);
        T.st.fitWatch.observe(pane);
      }

      function unwatchHeight() {
        if (T.st.fitWatch) T.st.fitWatch.disconnect();

        T.st.fitWatch = null;
      }

      // A field's change fires when the mouse goes down on something else. A redraw right then swaps out
      // what is under the mouse and the click is lost, so the redraw waits until the press is over.
      function renderAfterChange() {
        T.st.isRenderDue = true;

        if (!T.st.isPressing) setTimeout(renderIfDue, 0);
      }

      function renderIfDue() {
        if (!T.st.isRenderDue) return;

        const focus = focusedField();

        T.st.isRenderDue = false;
        T.render();
        refocus(focus);
      }

      // The field with the cursor, as a query that finds it again after a redraw.
      function focusedField() {
        const root = T.st.root;
        const el = root && root.ownerDocument ? root.ownerDocument.activeElement : null;

        if (!el || !el.hasAttribute || !el.hasAttribute('data-a') || !root.contains(el)) return null;

        const attrs = FOCUS_ATTRS.filter(n => el.hasAttribute(n)).map(n => '[' + n + '=' + JSON.stringify(el.getAttribute(n)) + ']');

        return { query: el.tagName.toLowerCase() + attrs.join(''), start: el.selectionStart, end: el.selectionEnd };
      }

      function refocus(focus) {
        const el = focus && T.st.root ? T.st.root.querySelector(focus.query) : null;

        if (!el) return;

        el.focus();

        if (typeof focus.start === 'number' && typeof el.setSelectionRange === 'function') el.setSelectionRange(focus.start, focus.end);
      }

      function watchPresses() {
        const root = T.st.root;

        if (!root || T.st.pressRoot === root) return;

        T.st.pressRoot = root;
        root.addEventListener('mousedown', () => { T.st.isPressing = true; }, true);
        root.ownerDocument.addEventListener('mouseup', endPress, true);
      }

      // The click comes right after mouseup, so the timer lets it land first.
      function endPress() {
        T.st.isPressing = false;

        if (T.st.isRenderDue) setTimeout(renderIfDue, 0);
      }

      // In the search box, Up and Down pick the row above or below and Enter picks the first one found.
      function onSearchKey(ev) {
        if (ev.target.getAttribute('data-a') !== 'rq') return;

        // The Mod Manager's Esc only takes the cursor out of the box, so a search with text in it stops the key here.
        if (ev.key === 'Escape') {
          if (!T.st.q) return;

          ev.preventDefault();
          ev.stopPropagation();
          clearSearch();

          return;
        }

        const ids = listIds();
        let next = 0;

        if (ev.key === 'Enter') next = ids[0];
        else if (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') {
          const at = ids.indexOf(T.st.sel);

          next = ids[at < 0 ? 0 : Math.max(0, Math.min(ids.length - 1, at + (ev.key === 'ArrowDown' ? 1 : -1)))];
        } else return;

        ev.preventDefault();

        if (!next) return;

        select(next);
        T.render();
        focusSearch();
        showSelectedRow();
      }

      // Unsaved edits light up SAVE. Text edits do not redraw the tab, so they call this. Typing a number lights
      // it too, since the number is only written when you leave the field.
      function refreshSaved(isTyping) {
        const btn = T.st.root ? T.st.root.querySelector('[data-a="save"]') : null;

        if (btn) btn.classList.toggle('is-due', !!T.st.dirty || !!isTyping);
      }

      // The switch above the card: it shows the reward as the game would, in English or Chinese.
      function pvBarHtml() {
        const mini = (v, label) => '<span data-a="pvl" data-v="' + v + '"' + (T.st.pvLang === v ? ' class="is-on"' : '') + '>' + label + '</span>';

        return '<div class="pv-bar">' + L('As the game shows it', '游戏内显示') + '<span class="mini">' + mini('en', 'EN') + mini('zh', '中文') + '</span></div>';
      }

      function editorHtml(e) {
        const ghost = isGhost(e);
        let html = pvBarHtml() + T.previewHtml(e) + T.whyHtml(e, ghost);

        if (ghost) return html + ghostHtml(e);

        if (e.frozen) return html + T.actionsHtml(e);

        if (e.fresh) html += T.lookHtml(e);

        return html + T.givesHtml(e) + T.levelsHtml(e) + T.textHtml(e) + T.actionsHtml(e);
      }

      function hasNoScript(e) {
        const base = e.base;

        return !e.fresh && !e.gives.length && !!base && base.levels.length > 0 && NO_SCRIPT.indexOf(base.levels[0].script) >= 0;
      }

      function ghostHtml(e) {
        const names = e.gives.map(g => esc(fxName(g.fx, T.st.lang === 'zh'))).join(', ');

        return (names ? '<div class="nnote">' + L('It gives', '效果') + ': <b>' + names + '</b></div>' : '') +
          ghostLevelsHtml(e) +
          '<div class="ed-acts">' +
          '<span class="gbtn" data-a="edit">' + L('CHANGE THIS REWARD', '修改此奖励') + '</span>' +
          '<span class="gbtn dim" data-a="copy">' + L('COPY INTO A NEW REWARD', '复制为新奖励') + '</span>' +
          '</div>';
      }

      // Each level's cost and text, so a reward you have not changed still shows what every level does. A click on a
      // row puts that level on the card above.
      function ghostLevelsHtml(e) {
        if (e.levels.length < 2) return '';

        const total = e.levels.reduce((sum, l) => sum + l.cost, 0);
        const sums = addsUp(e);
        const zh = T.st.lang === 'zh';
        const rows = e.levels.map((l, i) => '<tr data-a="lv" data-v="' + i + '"' + (i === T.st.lv ? ' class="is-on"' : '') + '>' +
          '<td class="l">Lv.' + (i + 1) + '</td>' +
          '<td class="c">' + numText(l.cost) + '</td>' +
          (sums ? '<td class="t">' + esc(totalText(e, i, zh)) + '</td>' : '') +
          '<td>' + esc(descOf(e, i, T.st.lang)) + '</td>' +
          '</tr>').join('');
        const head = '<tr><th>' + L('LEVEL', '等级') + '</th><th>' + L('COST', '消耗') + '</th>' +
          (sums ? '<th>' + L('IN TOTAL', '累计') + '</th><th>' + L('CARD TEXT', '卡面描述') + '</th>' : '<th>' + L('WHAT IT DOES', '效果') + '</th>') + '</tr>';

        return '<div class="lvs is-ghost"><b>' + L('LEVELS', '等级') + '</b>' +
          '<span class="total">' + L('To max level', '升满共需') + ' <b>' + numText(total) + '</b> ' + L('points', '点') + '</span></div>' +
          '<table class="lt glv">' + head + rows + '</table>' + (sums ? totalNoteHtml() : '');
      }

      const totalNoteHtml = () => '<div class="nnote">' + L('Each level adds to the ones below it. IN TOTAL is what the player gets from that level and every level before it.', '每一级都会叠加前面等级的效果。「累计」是该级加上前面所有等级的总效果。') + '</div>';

      function pickHtml(e, g, gi, p) {
        if (!T.canEditGives(e)) return '<span class="gv-w">' + esc(opt(p, g.pick[p.key], T.st.lang === 'zh')) + '</span>';

        const options = availableOptions(p);
        const current = g.pick[p.key];

        if (!options.some(o => o.v === current)) options.unshift({ v: current, en: opt(p, current, false), zh: opt(p, current, true) });

        return '<select class="nsel" data-a="pk" data-c="' + gi + '" data-k="' + esc(p.key) + '">' +
          options.map(o => '<option value="' + esc(JSON.stringify(o.v)) + '"' + (o.v === current ? ' selected' : '') + '>' + esc(L(o.en, o.zh)) + '</option>').join('') +
          '</select>';
      }

      function stepper(key, value, unit, isReadOnly) {
        return '<span class="stp' + (isReadOnly ? ' is-ro' : '') + '">' +
          '<b data-a="dec" data-k="' + key + '">&minus;</b>' +
          '<input data-a="val" data-k="' + key + '" value="' + numText(value) + '"' + (isReadOnly ? ' disabled' : '') + '>' +
          '<i>' + esc(unit) + '</i>' +
          '<b data-a="inc" data-k="' + key + '">+</b></span>';
      }

      // The description is per level when any level has its own; otherwise one text covers them all.
      function descTarget(e) {
        const l = e.levels[Math.min(T.st.lv, e.levels.length - 1)];

        return hasLevelDescs(e) ? { own: l.desc, game: l.gameDesc, perLevel: true } : { own: e.description, game: l ? l.gameDesc : null, perLevel: false };
      }

      // Only the preview and the footer change while the player types, so the input keeps its focus.
      function refreshPreview() {
        const pv = T.st.root ? T.st.root.querySelector('.pv') : null;
        const e = entryOrGhost(T.st.sel);

        if (pv && e) pv.replaceWith(h(T.previewHtml(e)));

        T.footer();
      }

      const talentName = id => T.st.byId[id] ? textIn(T.st.byId[id].name, T.st.lang) : '#' + id;

      const winEl = () => document.querySelector('#slmm .win') || T.st.root;

      function haystack(f) {
        const names = f.usedBy.concat(f.finishes).map(id => T.st.byId[id] ? T.st.byId[id].name.en + ' ' + T.st.byId[id].name.zh : '');

        return [f.en, f.zh, f.what[0], f.what[1], f.scripts.join(' ')].concat(names).join(' ').toLowerCase();
      }

      function matchesQuery(f, words) {
        if (!words.length) return true;

        const hay = haystack(f);

        return words.every(w => hay.indexOf(w) >= 0);
      }

      function pickerLists() {
        const p = T.st.picker;
        const words = p.q.toLowerCase().split(/\s+/).filter(Boolean);
        const byQuery = cat.list.filter(f => matchesQuery(f, words));
        const bySrc = byQuery.filter(f => p.src === 'all' || f.src === p.src);
        const shown = bySrc.filter(f => p.cat === 'all' || f.cat === p.cat);

        return { byQuery, bySrc, shown };
      }

      // An effect with no picks gives the same thing twice, so the reward may only have it once.
      function isOnOtherGive(e, f) {
        return picksOf(f).length === 0 && e.gives.some((g, gi) => g.fx === f && !(T.st.picker.mode === 'replace' && gi === T.st.picker.gi));
      }

      function closePicker() {
        if (T.st.pickerEl && T.st.pickerEl.parentNode) T.st.pickerEl.parentNode.removeChild(T.st.pickerEl);

        T.st.pickerEl = null;
        T.st.picker = null;
      }

      function renderPicker(full) {
        const el = T.st.pickerEl;
        const e = entryOf(T.st.sel);

        if (!el || !e) return;

        if (full) {
          el.innerHTML = '<div class="fxp-card">' + T.pickerHead(e) + '<div class="fxp-bd"></div><div class="fxp-ft"></div></div>';

          const input = el.querySelector('.fxp-q input');

          if (input) {
            input.focus();
            input.setSelectionRange(input.value.length, input.value.length);
          }
        }

        const lists = pickerLists();
        const listEl = el.querySelector('.fxp-list');
        const scroll = listEl ? listEl.scrollTop : 0;

        if (!lists.shown.some(f => f.id === T.st.picker.selId)) T.st.picker.selId = lists.shown.length ? lists.shown[0].id : '';

        el.querySelector('.fxp-bd').innerHTML = railHtml(lists.bySrc) + listHtml(e, lists.shown) + T.detailHtml(e, lists.shown);
        el.querySelector('.fxp-ft').innerHTML = lists.shown.length + ' ' + L('effects', '个效果') + '<span class="sp"></span>' + L('Double-click or Enter adds &middot; Esc closes', '双击或回车添加 &middot; Esc 关闭');

        const newList = el.querySelector('.fxp-list');

        if (newList) newList.scrollTop = scroll;
      }

      function railHtml(bySrc) {
        const p = T.st.picker;
        const item = (v, label, n) => '<div class="ri' + (p.cat === v ? ' is-on' : '') + (n === 0 ? ' is-zero' : '') + '" data-a="cat" data-v="' + v + '"><span>' + label + '</span><b>' + n + '</b></div>';

        return '<div class="fxp-rail"><div class="rh">' + L('KIND', '类别') + '</div>' +
          item('all', L('Everything', '全部'), bySrc.length) +
          CATS.map(c => item(c.id, L(c.en, c.zh), bySrc.filter(f => f.cat === c.id).length)).join('') +
          '</div>';
      }

      function cardMeta(e, f) {
        if (e.gives.some(g => g.fx === f)) return '<span class="tg ex">' + L('ADDED', '已添加') + '</span>';

        if (f.finishes.length) return L('Finishes', '完成') + ' ' + esc(talentName(f.finishes[0]));

        if (!f.usedBy.length) return L('No reward uses it yet', '暂无奖励使用');

        return L('Used by', '用于') + ' ' + esc(talentName(f.usedBy[0])) + (f.usedBy.length > 1 ? ' +' + (f.usedBy.length - 1) : '');
      }

      function listHtml(e, shown) {
        if (!shown.length) return '<div class="fxp-list"><div class="fxp-none">' + L('Nothing matches. Try fewer words, or another kind on the left.', '没有匹配项。试试更少的词，或左边的其他类别。') + '</div></div>';

        const groups = CATS.map(c => {
          const mine = shown.filter(f => f.cat === c.id);

          if (!mine.length) return '';

          return '<div class="ch">' + L(c.en, c.zh).toUpperCase() + '</div>' + mine.map(f => {
            const cls = ['fxc'];

            if (f.id === T.st.picker.selId) cls.push('is-sel');

            if (e.gives.some(g => g.fx === f)) cls.push('is-added');

            return '<div class="' + cls.join(' ') + '" data-a="card" data-v="' + esc(f.id) + '">' +
              '<img src="' + esc(fxIcon(f)) + '" alt="">' +
              '<div><div class="fxc-n">' + esc(fxName(f, T.st.lang === 'zh')) + '</div>' +
              '<div class="fxc-w">' + esc(L(f.what[0], f.what[1])) + '</div>' +
              '<div class="fxc-m">' + T.srcTag(f) + cardMeta(e, f) + '</div></div></div>';
          }).join('');
        }).join('');

        return '<div class="fxp-list">' + groups + '</div>';
      }

      function paramRow(p, count) {
        if (p.type === 'pick') {
          return '<tr><td>' + esc(L(p.en, p.zh)) + ' (' + L('one of', '可选') + ')</td><td>' + esc(availableOptions(p).map(o => L(o.en, o.zh)).join(', ')) + '</td></tr>';
        }

        return '<tr><td>' + esc(L(p.en, p.zh)) + '</td><td>' + esc(range(count).map(i => fmt(defaultAt(p, i))).join(' / ') + ' ' + unitOf(p)) + '</td></tr>';
      }

      function readsHtml(e, f, count) {
        try {
          const g = T.makeGive(f, '', e.id, null, count);
          const zh = T.st.lang === 'zh';
          const line = range(count).map(i => autoLine(g, i, zh)).join(' / ');
          const desc = g.fx.desc(valsOf(g, 0), zh, f.params);

          return '<div class="fxd-rd"><div class="l">' + esc(line) + '</div><div class="d">' + esc(desc) + '</div></div>';
        } catch (err) {
          return '';
        }
      }

      function chooseButton(e, f) {
        if (isOnOtherGive(e, f)) return '<span class="gbtn is-dis">' + L('ALREADY ON THIS REWARD', '已在此奖励上') + '</span>';

        if (T.st.picker.mode === 'replace') return '<span class="gbtn" data-a="choose">' + L('USE THIS INSTEAD', '改用此效果') + '</span>';

        return '<span class="gbtn" data-a="choose">' + L('ADD TO', '添加到') + ' ' + esc(nameOf(e, T.st.lang)) + '</span>';
      }

      function choose() {
        const e = entryOf(T.st.sel);
        const f = T.st.picker ? cat.list.find(x => x.id === T.st.picker.selId) : null;

        if (!e || !f || isOnOtherGive(e, f)) return;

        const isReplace = T.st.picker.mode === 'replace';

        setGive(e, f, isReplace ? T.st.picker.gi : -1);
        closePicker();
        T.render();
        T.toast((isReplace ? L('Now gives', '现在的效果：') : L('Added', '已添加')) + ' <b>' + esc(fxName(f, T.st.lang === 'zh')) + '</b>');
      }

      function onPickerDouble(ev) {
        if (ev.target.closest('[data-a="card"]')) choose();
      }

      function onPickerKey(ev) {
        if (ev.key === 'Escape') {
          ev.preventDefault();
          ev.stopPropagation();
          closePicker();
        } else if (ev.key === 'Enter') {
          ev.preventDefault();
          ev.stopPropagation();
          choose();
        }
      }

      // One group at a time: the game has some 1800 item images, too many to load at once.
      function iconsHtml() {
        const groups = [];

        cat.icons.forEach(ic => { if (groups.indexOf(ic.group) < 0) groups.push(ic.group); });

        if (groups.indexOf(T.st.iconGroup) < 0) T.st.iconGroup = groups[0] || REWARD_ICONS;

        const chips = groups.length > 1 ? '<div class="icg">' + groups.map(g => '<span data-a="icg" data-v="' + esc(g) + '"' + (g === T.st.iconGroup ? ' class="is-on"' : '') + '>' + esc(iconGroupName(g)) + '</span>').join('') + '</div>' : '';
        const imgs = cat.icons.map((ic, i) => ic.group === T.st.iconGroup ? '<img data-a="ic" data-v="' + i + '" src="' + esc(ic.icon) + '" loading="lazy" alt="" onerror="this.style.display=\'none\'">' : '').join('');

        return chips + '<div class="icl">' + imgs + '</div>';
      }

      function iconGroupName(group) {
        const known = ICON_GROUPS[group];

        return known ? L(known[0], known[1]) : group;
      }

      function showIconGroup(group) {
        if (!T.st.iconsEl) return;

        T.st.iconGroup = group;
        T.st.iconsEl.innerHTML = iconsHtml();
      }

      function closeIcons() {
        if (T.st.iconsEl && T.st.iconsEl.parentNode) T.st.iconsEl.parentNode.removeChild(T.st.iconsEl);

        T.st.iconsEl = null;
        document.removeEventListener('mousedown', onDocDown, true);
      }

      // The icon button toggles the picker itself, so a press on it is left to the click.
      function onDocDown(ev) {
        const t = ev.target;

        if (!T.st.iconsEl || T.st.iconsEl.contains(t) || (t.closest && t.closest('.look-ic'))) return;

        closeIcons();
      }

      function closeOverlays() {
        let wasOpen = !!(T.st.pickerEl || T.st.iconsEl);

        closePicker();
        closeIcons();

        if (T.closeMenus && T.closeMenus()) wasOpen = true;

        return wasOpen;
      }

      function closeToast() {
        clearTimeout(T.st.toastTimer);

        if (T.st.toastEl && T.st.toastEl.parentNode) T.st.toastEl.parentNode.removeChild(T.st.toastEl);

        T.st.toastEl = null;
      }

      function clampLevel() {
        T.st.lv = Math.max(0, Math.min(T.st.lv, levelCount(T.st.sel) - 1));
      }

      function select(id) {
        T.st.sel = id;
        T.st.lv = 0;
      }

      function setText(t, lang, value) {
        const o = t ? { en: t.en, zh: t.zh } : { en: '', zh: '' };

        o[lang] = value;

        return normText(o);
      }

      // The editor shows a reward only while the view holds it. One the search hides stays open, so typing never
      // swaps what you are editing. One the view does not hold is swapped for the first row, or for nothing when the
      // list is empty.
      function keepSelectionInView() {
        if (T.st.sel && entryOrGhost(T.st.sel) && viewIds().indexOf(T.st.sel) >= 0) return;

        select(listIds()[0] || 0);
      }

      function afterRemove() {
        keepSelectionInView();
        clampLevel();
      }

      function onInput(ev) {
        const t = ev.target;
        const a = t.getAttribute('data-a');

        if (a === 'q' && T.st.picker) {
          T.st.picker.q = t.value;
          renderPicker(false);

          return;
        }

        if (a === 'rq') {
          T.st.q = t.value;
          refreshList();

          return;
        }

        if (a === 'val') {
          refreshSaved(true);

          return;
        }

        const e = entryOf(T.st.sel);

        if (a !== 'txt' || !e) return;

        const key = t.getAttribute('data-k');

        T.editText(e, key, t.value);
        refreshPreview();
        refreshSaved();

        if (key.indexOf('name:') === 0) {
          const nm = T.st.root.querySelector('.ab.is-sel .nm');

          if (nm) nm.textContent = nameOf(e, T.st.lang);
        }
      }

      function hideTab() {
        closeOverlays();
        unwatchHeight();
      }

      // Esc closes a picker first, then clears the search.
      function escapeTab() {
        if (closeOverlays()) return true;

        if (!T.st.q) return false;

        T.st.q = '';
        T.render();

        return true;
      }


    // The styles both tabs use. {c} is the tab's class name, and the tab's own lines go in at '@extra'.
    const CSS_BASE = [
      'N &{' + VARS + 'position:relative;padding:16px 26px 26px;color:var(--tm);background:radial-gradient(ellipse at 20% 0%,rgba(120,80,30,0.28),transparent 60%),linear-gradient(180deg,#1f1a15,#16120e)}',
      'N &::before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent 10%,var(--hl) 50%,transparent 90%);opacity:.5}',
      'B input,textarea,select{user-select:text}',
      'N .{c}-top{display:flex;align-items:flex-start;gap:12px;margin-bottom:12px}',
      'N .{c}-head{flex:1;min-width:0}',
      'N .{c}-title{font-size:1.5rem;font-weight:700;letter-spacing:4px;color:#fff;text-shadow:0 2px 10px rgba(0,0,0,.6);white-space:nowrap}',
      'N .{c}-sub{color:var(--td);font-size:.9rem;line-height:1.4;margin-top:2px}',
      'N .{c}-top .key-btn.is-due{border-color:var(--hl);color:var(--hl);background:rgba(255,200,58,0.12)}',
      'N .{c}-bar{display:flex;align-items:center;flex-wrap:wrap;gap:10px 12px;margin-bottom:12px}',
      'N .srch{flex:1;min-width:200px;display:flex;align-items:center;gap:8px;border:1px solid var(--bl);background:rgba(0,0,0,0.45);padding:0 10px;color:var(--tmeta)}',
      'N .srch:focus-within{border-color:var(--hl)}',
      'N .srch svg{flex:none}',
      'N .srch input{flex:1;min-width:0;background:transparent;border:0;outline:none;color:#fff;font:400 .95rem var(--body);padding:7px 0}',
      'N .srch input::placeholder{color:#6d6253}',
      'N .srch .clr{cursor:pointer;padding:0 4px}',
      'N .srch .clr:hover{color:#fff}',
      'N .srch .clr.is-hid{visibility:hidden}',
      'N .nomatch{padding:24px 16px;text-align:center;color:var(--td);line-height:1.6}',
      'N .nomatch b{color:#fff}',
      'N .nomatch .gbtn{display:inline-block;margin-top:10px}',
      'B .sp{flex:1}',
      'N .{c}-chips{display:flex;border:1px solid var(--bl)}',
      'N .{c}-chips span{padding:6px 14px;font-size:.85rem;color:var(--td);cursor:pointer;letter-spacing:1px;white-space:nowrap}',
      'N .{c}-chips span.is-on{background:rgba(255,200,58,0.14);color:var(--hl)}',
      'N .{c}-top .key-btn{white-space:nowrap}',
      'N .{c}-probs{margin:0 0 12px;padding:8px 14px;border:1px solid rgba(255,82,82,0.4);background:rgba(255,82,82,0.08);color:#e9b;font-size:.85rem;line-height:1.5}',
      'N .{c}-bad{margin:0 0 12px;padding:10px 14px;border:1px solid rgba(255,82,82,0.5);background:rgba(255,82,82,0.12);color:#fcc;font-size:.92rem}',
      'N .{c}-wait{padding:60px;text-align:center;color:var(--td)}',
      'N .{c}-main{display:grid;grid-template-columns:minmax(280px,34%) minmax(0,1fr);gap:20px;align-items:start}',
      'N .{c}-list{position:relative;height:var(--fit-h,610px);overflow-y:auto;overflow-x:hidden;border:1px solid var(--bd2);background:rgba(0,0,0,0.22)}',
      'B ::-webkit-scrollbar{width:6px;height:6px}',
      'B ::-webkit-scrollbar-track,::-webkit-scrollbar-corner{background:transparent}',
      'B ::-webkit-scrollbar-thumb{background:rgba(255,200,58,0.3)}',
      'N .col-h{position:sticky;top:0;z-index:2;padding:9px 16px;text-align:center;color:var(--hl);font-weight:700;letter-spacing:5px;font-size:.95rem;text-shadow:0 0 12px var(--hl-glow);background:#1d1813;border-bottom:1px solid var(--bl)}',
      'N .col-h small{position:absolute;right:12px;top:11px;font-family:var(--mono);font-size:.7rem;color:var(--tmeta);letter-spacing:0;text-shadow:none}',
      'N .ab{position:relative;display:grid;grid-template-columns:52px minmax(0,1fr) auto;gap:14px;align-items:center;padding:8px 16px;min-height:66px;cursor:pointer;border-bottom:1px solid rgba(255,255,255,0.04)}',
      'N .ab::before{content:"";position:absolute;left:0;top:10px;bottom:10px;width:3px}',
      'N .ab:hover{background:linear-gradient(90deg,rgba(255,200,58,0.06),transparent)}',
      'N .ab.is-sel{background:linear-gradient(90deg,rgba(255,200,58,0.13),rgba(255,200,58,0.02) 70%,transparent)}',
      'N .ab.is-sel::before{background:var(--hl);box-shadow:0 0 10px var(--hl-glow)}',
      'N .ab img{width:52px;height:52px;object-fit:contain;filter:drop-shadow(0 2px 5px rgba(0,0,0,.5))}',
      'N .ab .nm{font-weight:700;font-size:1rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      'N .ab .ds{margin-top:2px;font-size:.8rem;color:var(--td);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      'N .ab .mt{display:flex;gap:10px;align-items:center;margin-top:3px;font-size:.8rem;color:var(--tmeta)}',
      'N .ab .lvn{font-family:var(--mono);color:var(--hl)}',
      'N .ab.is-out{opacity:.5}',
      'N .ab.is-out:hover,.ab.is-out.is-sel{opacity:.85}',
      'N .ab.is-off .nm,.ab.is-off img{opacity:.5}',
      'N .ab .setting-toggle{display:block}',
      'B .tg{white-space:nowrap;font-family:var(--mono);font-size:.66rem;letter-spacing:1px;padding:1px 6px;border:1px solid}',
      'B .tg.cut{color:#d88;border-color:rgba(255,82,82,0.4)}',
      'B .tg.fin{color:var(--hl);border-color:rgba(255,200,58,0.45)}',
      'B .tg.src-game{color:#9ab;border-color:rgba(150,180,200,0.35)}',
      'B .tg.new{color:#7dd;border-color:rgba(100,220,220,0.4)}',
      'B .tg.src-{c}{color:var(--hl);border-color:rgba(255,200,58,0.45)}',
      'B .tg.is-chg{color:#c9a0ff;border-color:rgba(180,140,255,0.45)}',
      'B .tg.ex{color:var(--tmeta);border-color:rgba(255,255,255,0.15);border-style:dashed}',
      'N .new-btn{padding:7px 14px;border:1px dashed var(--hl);color:var(--hl);cursor:pointer;letter-spacing:2px;font-size:.85rem;font-weight:700;white-space:nowrap}',
      'N .new-btn:hover{background:rgba(255,200,58,0.12);color:#fff}',
      'N .ed{min-width:0;height:var(--fit-h,610px);overflow-y:auto;overflow-x:hidden;padding-right:8px}',
      'N .pv{display:grid;grid-template-columns:110px minmax(0,1fr);gap:20px;padding:16px 20px;border:1px solid var(--bl);background:linear-gradient(180deg,rgba(40,32,24,0.95),rgba(24,20,16,0.95));box-shadow:0 8px 30px rgba(0,0,0,.5);position:relative}',
      'N .pv img{width:110px;height:110px;object-fit:contain;filter:drop-shadow(0 0 12px rgba(255,200,58,0.25))}',
      'N .pv-name{font-size:1.45rem;font-weight:700;color:#fff;display:flex;flex-wrap:wrap;align-items:baseline;gap:4px 14px;overflow-wrap:break-word}',
      'N .pv-name .lvb{font-family:var(--mono);font-size:1.1rem;color:var(--hl);text-shadow:0 0 6px var(--hl-glow);white-space:nowrap}',
      'N .pv-lt{color:var(--hl);font-size:.95rem;margin-top:4px}',
      'N .pv-lt b{color:#fff;text-decoration:underline;text-decoration-color:var(--hl);text-underline-offset:3px}',
      'N .pv-desc{color:var(--tm);font-size:1rem;margin-top:10px;line-height:1.5;min-height:48px}',
      'N .pv-desc.is-empty{color:var(--tmeta);font-style:italic}',
      'N .pv-cost{margin-top:8px;font-size:.9rem;color:var(--td)}',
      'N .pv-cost b{color:var(--hl);font-family:var(--mono);font-size:1.05rem}',
      'N .pv-sub{margin-top:4px;font-size:.85rem;color:var(--td)}',
      'N .pv-bar{display:flex;justify-content:flex-end;gap:8px;align-items:center;margin-bottom:6px;font-size:.75rem;color:var(--tmeta)}',
      'N .pv-bar .mini{display:flex;border:1px solid var(--bl)}',
      'N .pv-bar .mini span{padding:2px 8px;cursor:pointer}',
      'N .pv-bar .mini span.is-on{color:var(--hl);background:rgba(255,200,58,0.12)}',
      'N .sec-h{display:flex;align-items:center;gap:12px;margin:16px 0 8px;color:var(--hl);letter-spacing:3px;font-size:.8rem;font-weight:700}',
      'N .sec-h small{color:var(--tmeta);letter-spacing:0;font-weight:400;font-size:.8rem}',
      'N .look{display:grid;grid-template-columns:96px 1fr;gap:16px;padding:12px 14px;border:1px solid var(--bd2);background:rgba(0,0,0,0.25)}',
      'N .look-ic{position:relative;width:96px;height:96px;border:1px dashed var(--bl);cursor:pointer;display:flex;align-items:center;justify-content:center}',
      'N .look-ic img{width:80px;height:80px;object-fit:contain}',
      'N .look-ic span{position:absolute;bottom:-1px;left:-1px;right:-1px;text-align:center;font-size:.66rem;letter-spacing:1px;background:rgba(0,0,0,0.75);color:var(--hl);padding:2px 0}',
      'N .look-ic:hover{border-color:var(--hl)}',
      'N .look-f{display:grid;grid-template-columns:90px minmax(0,1fr) minmax(0,1fr);gap:8px 10px;align-items:center}',
      'N .look-f .k,.tx .k{color:var(--td);font-size:.88rem}',
      'N .look-f input,.tx input{width:100%;box-sizing:border-box;background:rgba(0,0,0,0.4);border:1px solid var(--bd2);color:#fff;padding:6px 9px;font:400 .92rem var(--body);outline:none}',
      'N .look-f input:focus,.tx input:focus{border-color:var(--hl)}',
      'N .tx input::placeholder{color:#6d6253}',
      'N .seg2{display:inline-flex;border:1px solid var(--bl)}',
      'N .seg2 span{padding:5px 14px;font-size:.82rem;letter-spacing:2px;color:var(--td);cursor:pointer}',
      'N .seg2 span.is-on{color:#1c1814;background:var(--hl);font-weight:700}',
      'N .gv{border:1px solid var(--bd2);background:rgba(0,0,0,0.22)}',
      'N .gv-row{display:grid;grid-template-columns:44px minmax(0,1fr) auto auto auto;gap:12px;align-items:center;padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.04)}',
      'N .gv-row img{width:44px;height:44px;object-fit:contain}',
      'N .gv-n{font-weight:700;color:#fff;display:flex;gap:8px;align-items:center}',
      'N .gv-w{color:var(--td);font-size:.84rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      'N .gv-picks{display:flex;flex-wrap:wrap;gap:6px}',
      'N .gv-row a{color:var(--hl);font-size:.8rem;letter-spacing:2px;cursor:pointer}',
      'N .gv-row a:hover{color:#fff}',
      'N .gv-row .x{color:var(--tmeta);cursor:pointer;padding:0 4px}',
      'N .gv-row .x:hover{color:#FF5252}',
      'N .gv-add{padding:8px 12px;color:var(--hl);font-size:.85rem;letter-spacing:2px;cursor:pointer}',
      'N .gv-add:hover{background:rgba(255,200,58,0.08)}',
      'N .gv-empty{padding:18px;text-align:center;color:var(--td)}',
      'N .gv-empty .gbtn{display:inline-block;margin-top:10px}',
      'N select.nsel{background:#1d1813;border:1px solid var(--bl);color:#fff;padding:5px 8px;font:400 .88rem var(--body);outline:none}',
      'N .lvs{display:flex;flex-wrap:wrap;gap:6px;margin:14px 0 6px;align-items:center}',
      'N .lvs span{min-width:46px;text-align:center;padding:6px 0;font-family:var(--mono);font-size:.85rem;color:var(--td);border:1px solid var(--bd2);cursor:pointer}',
      'N .lvs span.is-on{color:#1c1814;background:var(--hl);border-color:var(--hl);font-weight:700}',
      'N .lvs span.is-add{color:var(--hl);border-style:dashed;padding:6px 10px}',
      'N .lvs .total{margin-left:auto;color:var(--td);font-size:.88rem;border:0;cursor:default;font-family:var(--body)}',
      'N .lvs .total b{color:var(--hl);font-family:var(--mono)}',
      'N .lt{width:100%;border-collapse:collapse}',
      'N .lt th{text-align:left;font-weight:400;font-size:.74rem;letter-spacing:2px;color:var(--tmeta);padding:6px 8px;border-bottom:1px solid var(--bl);vertical-align:bottom}',
      'N .lt th small{display:block;letter-spacing:0;font-size:.7rem;color:#5e5546}',
      'N .lt td{padding:5px 8px;border-bottom:1px solid rgba(255,255,255,0.035)}',
      'N .lt tr.is-on td{background:rgba(255,200,58,0.06)}',
      'N .lt td.l{font-family:var(--mono);color:var(--hl);width:54px;cursor:pointer}',
      'N .lt td.x{width:30px;color:var(--tmeta);cursor:pointer}',
      'N .lvs.is-ghost>b{font-size:.74rem;letter-spacing:2px;color:var(--tmeta);font-weight:400}',
      'N .glv tr[data-a]{cursor:pointer}',
      'N .glv tr[data-a]:hover td{background:rgba(255,200,58,0.04)}',
      'N .glv td{color:var(--tm);line-height:1.45;padding:7px 8px}',
      'N .glv td.c{width:54px;font-family:var(--mono);color:#fff}',
      'N .lt td.t{white-space:nowrap;font-family:var(--mono);color:var(--hl)}',
      'N .lt td.x:hover{color:#FF5252}',
      'N .stp{display:inline-flex;align-items:center;border:1px solid var(--bl);background:rgba(0,0,0,0.35)}',
      'N .stp b{width:30px;text-align:center;color:var(--hl);cursor:pointer;font-size:1.1rem;user-select:none;line-height:32px}',
      'N .stp b:hover{background:rgba(255,200,58,0.14)}',
      'N .stp input{width:62px;background:transparent;border:0;color:#fff;text-align:center;font:700 1rem var(--mono);outline:none}',
      'N .stp i{font-style:normal;color:var(--td);font-size:.82rem;padding-right:10px}',
      'N .stp.is-ro b{visibility:hidden}',
      'N .nnote{color:var(--td);font-size:.88rem;margin:10px 0 0;line-height:1.5}',
      'N .nnote b{color:var(--tm)}',
      'N .fold{margin-top:14px;border-top:1px solid var(--bd2);padding-top:10px}',
      'N .fold-t{cursor:pointer;color:var(--hl);letter-spacing:3px;font-size:.8rem}',
      'N .tx{display:grid;grid-template-columns:130px minmax(0,1fr) minmax(0,1fr);gap:8px 10px;align-items:center;margin-top:10px}',
      '@extra',
      'N .tx .auto{color:var(--hl);font-size:.88rem;line-height:1.4}',
      'N .tx .hd2{color:var(--tmeta);font-size:.74rem;letter-spacing:2px}',
      'N .chk{display:flex;align-items:center;gap:8px;margin-top:10px;color:var(--td);font-size:.88rem;cursor:pointer}',
      'N .chk input{accent-color:#FFC83A}',
      'N .ed-acts{display:flex;flex-wrap:wrap;gap:10px;margin-top:16px;align-items:center}',
      'B .gbtn{padding:8px 16px;border:1px solid var(--hl);color:var(--hl);background:rgba(255,200,58,0.08);cursor:pointer;letter-spacing:2px;font-size:.85rem;font-weight:700;white-space:nowrap}',
      'B .gbtn:hover{background:rgba(255,200,58,0.22);color:#fff}',
      'B .gbtn.dim{border-color:var(--bl);color:var(--td);background:transparent}',
      'B .gbtn.is-dis{opacity:.45;pointer-events:none}',
      'N .add-cta{padding:18px 20px;margin-top:14px;border:1px dashed var(--bl);color:var(--td)}',
      'N .add-cta .gbtn{display:inline-block;margin:10px 10px 0 0}',
      'P &{' + VARS + 'position:absolute;inset:0;z-index:50;background:rgba(0,0,0,0.72);display:flex;align-items:center;justify-content:center;color:var(--tm);animation:slmm-pop .18s both}',
      'P .fxp-card{width:calc(100% - 40px);max-width:1520px;height:calc(100% - 40px);max-height:850px;display:grid;grid-template-rows:auto 1fr auto;border:1px solid rgba(255,200,58,0.35);background:linear-gradient(180deg,#221c16,#15110d);box-shadow:0 30px 90px rgba(0,0,0,0.9)}',
      'P .fxp-hd{display:flex;flex-wrap:wrap;align-items:center;gap:12px 18px;padding:14px 20px;border-bottom:1px solid var(--bl)}',
      'P .fxp-hd .t{font-size:1.25rem;font-weight:700;letter-spacing:4px;color:#fff;white-space:nowrap}',
      'P .fxp-hd .t small{display:block;font-size:.8rem;letter-spacing:1px;color:var(--td);font-weight:400}',
      'P .fxp-q{flex:1;min-width:240px;display:flex;align-items:center;gap:8px;border:1px solid var(--bl);background:rgba(0,0,0,0.45);padding:0 12px}',
      'P .fxp-q:focus-within{border-color:var(--hl)}',
      'P .fxp-q input{flex:1;background:transparent;border:0;outline:none;color:#fff;font:400 1.05rem var(--body);padding:9px 0}',
      'P .fxp-q span{color:var(--tmeta)}',
      'P .fxp-src{display:flex;border:1px solid var(--bl)}',
      'P .fxp-src span{padding:7px 13px;font-size:.82rem;color:var(--td);cursor:pointer;letter-spacing:1px;white-space:nowrap}',
      'P .fxp-src span b{font-family:var(--mono);font-weight:400;color:var(--tmeta);margin-left:4px}',
      'P .fxp-src span.is-on{background:rgba(255,200,58,0.14);color:var(--hl)}',
      'P .fxp-x{font-size:1.4rem;color:var(--td);cursor:pointer;padding:0 4px}',
      'P .fxp-x:hover{color:#fff}',
      'P .fxp-bd{display:grid;grid-template-columns:minmax(170px,230px) minmax(0,1fr) minmax(300px,440px);min-height:0}',
      'P .fxp-rail{overflow-y:auto;border-right:1px solid var(--bd2);padding:8px 0;background:rgba(0,0,0,0.2)}',
      'P .fxp-rail .rh{padding:12px 18px 6px;font-size:.7rem;letter-spacing:2px;color:var(--tmeta)}',
      'P .fxp-rail .ri{display:flex;justify-content:space-between;padding:7px 18px;cursor:pointer;font-size:.92rem;color:var(--td);border-left:3px solid transparent}',
      'P .fxp-rail .ri:hover{background:rgba(255,200,58,0.05);color:var(--tm)}',
      'P .fxp-rail .ri.is-on{border-left-color:var(--hl);background:rgba(255,200,58,0.1);color:#fff}',
      'P .fxp-rail .ri.is-zero{opacity:.35}',
      'P .fxp-rail .ri b{font-family:var(--mono);font-weight:400;color:var(--tmeta);font-size:.8rem}',
      'P .fxp-list{overflow-y:auto;padding:4px 14px 14px}',
      'P .fxp-list .ch{position:sticky;top:0;z-index:1;padding:10px 4px 6px;font-size:.72rem;letter-spacing:3px;color:var(--hl);background:#1d1813;border-bottom:1px solid var(--bd2)}',
      'P .fxc{display:grid;grid-template-columns:46px minmax(0,1fr);gap:12px;padding:9px 10px;cursor:pointer;border:1px solid transparent;border-bottom-color:rgba(255,255,255,0.04)}',
      'P .fxc:hover{background:rgba(255,200,58,0.05)}',
      'P .fxc.is-sel{border-color:rgba(255,200,58,0.55);background:rgba(255,200,58,0.09)}',
      'P .fxc.is-added{opacity:.55}',
      'P .fxc img{width:46px;height:46px;object-fit:contain}',
      'P .fxc-n{font-weight:700;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      'P .fxc-w{font-size:.84rem;color:var(--td);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      'P .fxc-m{display:flex;gap:8px;align-items:center;margin-top:3px;font-size:.74rem;color:var(--tmeta);white-space:nowrap;overflow:hidden}',
      'P .fxp-none{padding:40px;text-align:center;color:var(--td)}',
      'P .fxd{overflow-y:auto;border-left:1px solid var(--bd2);padding:18px 22px;background:rgba(0,0,0,0.25)}',
      'P .fxd-top{display:grid;grid-template-columns:76px 1fr;gap:14px;align-items:center}',
      'P .fxd-top img{width:76px;height:76px;object-fit:contain;filter:drop-shadow(0 0 10px rgba(255,200,58,0.2))}',
      'P .fxd-n{font-size:1.3rem;font-weight:700;color:#fff}',
      'P .fxd-s{display:flex;gap:8px;align-items:center;margin-top:4px;flex-wrap:wrap}',
      'P .fxd-s code{font:.76rem var(--mono);color:var(--tmeta)}',
      'P .fxd-w{margin-top:12px;line-height:1.5}',
      'P .fxd h4{margin:18px 0 6px;font-size:.72rem;letter-spacing:3px;color:var(--hl);font-weight:700}',
      'P .fxd table{width:100%;border-collapse:collapse;font-size:.88rem;margin:0}',
      'P .fxd td{padding:5px 0;border-bottom:1px solid rgba(255,255,255,0.04);vertical-align:top}',
      'P .fxd td:first-child{color:var(--td);width:46%}',
      'P .fxd td:last-child{font-family:var(--mono);color:var(--tm)}',
      'P .fxd-rd{padding:10px 12px;border:1px solid var(--bl);background:rgba(40,32,24,0.8)}',
      'P .fxd-rd .l{color:var(--hl);font-size:.9rem}',
      'P .fxd-rd .d{margin-top:6px;font-size:.92rem;line-height:1.5}',
      'P .fxd .ub{display:flex;flex-wrap:wrap;gap:6px}',
      'P .fxd .ub span{display:inline-flex;gap:6px;align-items:center;padding:2px 8px 2px 2px;border:1px solid var(--bd2);font-size:.8rem;color:var(--td)}',
      'P .fxd .ub img{width:22px;height:22px;object-fit:contain}',
      'P .fxd .fn{margin-top:6px;font-size:.84rem;color:var(--td);line-height:1.5}',
      'P .fxd .gbtn{display:inline-block;margin-top:18px}',
      'P .fxp-ft{display:flex;align-items:center;gap:14px;padding:12px 20px;border-top:1px solid var(--bl);color:var(--tmeta);font-size:.84rem}',
      'I &{' + VARS + 'position:absolute;z-index:60;width:720px;max-width:calc(100% - 20px);box-sizing:border-box;padding:10px;background:#1a1611;border:1px solid rgba(255,200,58,0.4);box-shadow:0 20px 60px rgba(0,0,0,0.85)}',
      'I .icg{display:flex;flex-wrap:wrap;gap:4px;margin-bottom:8px}',
      'I .icg span{padding:3px 10px;font-size:.8rem;color:var(--td);border:1px solid var(--bl);cursor:pointer;white-space:nowrap}',
      'I .icg span.is-on{background:rgba(255,200,58,0.14);color:var(--hl);border-color:var(--hl)}',
      'I ::-webkit-scrollbar{width:6px}',
      'I ::-webkit-scrollbar-thumb{background:rgba(255,200,58,0.3)}',
      'I .icl{display:grid;grid-template-columns:repeat(auto-fill,minmax(62px,1fr));gap:4px;max-height:400px;overflow-y:auto}',
      'I img{width:58px;height:58px;object-fit:contain;padding:4px;border:1px solid transparent;cursor:pointer}',
      'I img:hover{border-color:var(--hl);background:rgba(255,200,58,0.1)}',
      'T &{position:absolute;left:50%;bottom:74px;transform:translateX(-50%);background:#0d1716;border:1px solid var(--teal);color:#cfe;padding:10px 18px;font-size:.95rem;z-index:40;box-shadow:0 10px 30px rgba(0,0,0,0.7);animation:slmm-pin .25s both}',
      'T b{color:#fff}'
    ];

    function editorCss(cls, extra) {
      const at = CSS_BASE.indexOf("@extra");

      return CSS_BASE.slice(0, at).concat(extra, CSS_BASE.slice(at + 1)).map(line => line.split('{c}').join(cls));
    }

    return { MAX_LEVELS, MAX_GIVES, GAME_PREFIX, REWARD_ICONS, ICON_GROUPS, TOAST_MS, INT_FIELD, STAT_MAX, NO_SCRIPT, VARS, scopedCss, injectStyle, isObj, isArr, isNum, clone, round2, fmt, range, L, ESCAPES, esc, isEmptyVal, zeroOf, allKeys, sameVal, h, normText, textIn, shown, CATS, GROUP_TALENTS, SKILL_TALENTS, XP_SKILLS, WORK, STATS, ROBOTS, robotScript, robotName, named, nameOptions, num, pct, pick, hide, optionsOf, isOptionOn, availableOptions, groupUnknown, opt, itemCount, plural, numericBuffKeys, FX, fx, FX_BY_ID, picksOf, numsOf, hidesOf, fieldName, display, toRaw, defaultAt, match, rawCache, FIELD_RANK, prettyScript, rawLabel, rawFx, cat, talentIcons, fxIcon, fxName, fromRoot, scriptOfGive, fixedFrom, roundTrips, isEditable, isAutoOn, showsAuto, valsOf, autoLine, autoLevelText, autoDesc, addsUp, totalVals, totalText, totalNoteHtml, baseText, nameOf, descOf, hasLevelDescs, entryOf, entryOrGhost, levelCount, markChanged, givesChanged, adopt, resetOrDelete, nextStep, setGive, setPick, writeText, saved, discard, send, numText, unitOf, tag, isGhost, listIds, searchWords, searchText, matchesSearch, searchHtml, noMatchHtml, refreshList, clearSearch, focusSearch, showSelectedRow, fitHeight, watchHeight, renderAfterChange, watchPresses, onSearchKey, refreshSaved, pvBarHtml, editorHtml, hasNoScript, ghostHtml, ghostLevelsHtml, pickHtml, stepper, descTarget, refreshPreview, talentName, winEl, haystack, matchesQuery, pickerLists, isOnOtherGive, closePicker, renderPicker, railHtml, cardMeta, listHtml, paramRow, readsHtml, chooseButton, choose, onPickerDouble, onPickerKey, iconsHtml, iconGroupName, showIconGroup, closeIcons, onDocDown, closeOverlays, closeToast, clampLevel, select, setText, viewIds, keepSelectionInView, afterRemove, onInput, hideTab, escapeTab, editorCss };
  }
})();
