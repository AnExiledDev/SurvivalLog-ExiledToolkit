// Exiled's Toolkit: the editor New Loop Plus' Talents tab and More Rewards' Rewards tab share, run in the game's
// root web document. It is sent with each tab's own script, ahead of it. A tab calls ExiledEditor.make(T) once and
// gets its own copy of the effect list, the effect and icon pickers, the level and text editors and the helpers they
// use. T is the tab's side: its state (st), its styles, names and messages, and the parts each tab draws its own way.
// The tab fills T before anything is drawn, and sets T.levelsAddUp before it calls make, since a few effects are
// worded differently there. A newer copy of this script replaces an older one; an older one never replaces a newer one.
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
        ['food', 'Food and cooking', '食物与烹饪'],
        ['events', 'Wishes and events', '愿望与事件'],
        ['camps', 'Camps and aid', '营地与求援'],
        ['status', 'Statuses', '状态'],
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

      // The game's food types, by the number in a food field's name (BuffSatietyFood4Add_Ratio is fish).
      const FOOD_TYPES = [[1, 'Staple food', '主食'], [2, 'Meat', '肉类'], [3, 'Eggs and dairy', '蛋奶'], [4, 'Fish', '鱼类'], [5, 'Vegetables', '蔬菜'], [6, 'Fruit', '水果'],
        [7, 'Snacks', '零食'], [8, 'Seasoning', '调味品'], [9, 'Soft drinks', '软饮料'], [10, 'Liquor', '酒类'], [11, 'Mushrooms', '菌菇']].map(f => ({ v: f[0], en: f[1], zh: f[2] }));

      const FOOD_STATS = STATS.filter(s => s.v === 'Satiety' || s.v === 'Morale');

      const FOOD_FIELD = /^(EF_BuffMorale|BuffSatiety)Food(\d+)Add_Ratio$/;

      const foodField = (stat, food) => (stat === 'Morale' ? 'EF_BuffMoraleFood' : 'BuffSatietyFood') + food + 'Add_Ratio';

      const foodKeys = v => Object.keys(v).filter(k => FOOD_FIELD.test(k) && isNum(v[k]) && v[k] !== 0);

      const POTS = [['Small', 'Small Flowerpot', '小型花盆'], ['Medium', 'Medium Flowerpot', '中型花盆'], ['Large', 'Large Flowerpot', '大型花盆']].map(p => ({ v: p[0], en: p[1], zh: p[2], script: 'AE_SkipTillSoil' + p[0] + 'Pot' }));

      const DISH_TIERS = [{ v: 2, en: 'Medium', zh: '中档' }, { v: 3, en: 'High', zh: '高档' }];

      // The device groups AE_PowerCostByGroup can raise, by its IntValue0.
      const POWER_GROUPS = [{ v: 2, en: 'Defense turrets (each shot)', zh: '塔防装置（每次开火）' }, { v: 1, en: 'Fridges and freezers', zh: '冰箱冷柜' }];

      const recipeUnknown =(v, zh) => (zh ? '配方 ' : 'Recipes ') + v;

      // Filled from the game's reward names once the data arrives.
      const named = { groups: [], skills: [], xp: [], work: [], robots: [], crafts: [], cooks: [] };

      // The recipes the game's rewards teach, named after the reward's card ("Unlocks the Oven crafting recipe.").
      function recipeOptions(skill) {
        const seen = new Set();
        const out = [];

        T.st.game.forEach(t => t.levels.forEach(l => {
          const v = l.values || {};
          const recipes = v.StrValue1;

          if (l.script !== 'UnlockCraftRecipe' || v.StrValue0 !== skill || typeof recipes !== 'string' || seen.has(recipes)) return;

          const desc = normText(l.desc) || { en: '', zh: '' };
          const name = normText(t.name) || { en: '', zh: '' };
          const en = desc.en.replace(/^Unlocks (the )?/, '').replace(/ (crafting|cooking) recipes?\.?$/, '');
          const zh = (desc.zh.match(/【[^】]+】/g) || []).map(s => s.slice(1, -1)).join('、');

          seen.add(recipes);
          out.push({ v: recipes, en: en && en !== desc.en ? en : name.en || 'Recipes ' + recipes, zh: zh || name.zh || '配方 ' + recipes });
        }));

        return out;
      }

      // New Loop Plus keys a reward by its talent id (3066), More Rewards by its first level's row (30661).
      const rewardOf = tid => T.st.byId[tid] || T.st.byId[tid * 10 + 1];

      function nameOptions() {
        named.groups = Object.keys(GROUP_TALENTS).map(Number).filter(g => rewardOf(GROUP_TALENTS[g])).map(g => {
          const t = rewardOf(GROUP_TALENTS[g]);

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
        named.crafts = recipeOptions('Production');
        named.cooks = recipeOptions('Cooking');
      }

      // A number that can change per level. scale and add turn the file's raw value into what the player reads (0.1 -> 10%).
      // stack is how the levels of a tab whose levels add up (More Rewards) combine: 'sum' adds the raw values, 'times'
      // multiplies them (what is left to pay after a discount), 'max' keeps the highest (a level a skill is raised to),
      // and 'each' is a limit every level checks on its own, so it has no total.
      function num(key, en, zh, unit, def, o) {
        return Object.assign({ type: 'num', key, en, zh, unit, def, step: 1, scale: 1, add: 0, min: 0, stack: 'sum' }, o);
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

      // An effect whose number is a result (a limit of 800, 2x as long) has a sum version for a tab whose levels add
      // up, where each level gives its own raise. An effect is a game one named game:<script> unless it says otherwise,
      // and a tab that adds an effect with an id already in the list replaces it.
      function fx(o) {
        const own = T.levelsAddUp && o.sum ? Object.assign({}, o, o.sum) : o;
        const made = Object.assign({ id: GAME_PREFIX + o.script, src: 'game', usedBy: [], finishes: [], params: [], modes: 'both', pickable: true, raw: false }, own, { scripts: o.scripts || [o.script] });
        const at = FX.findIndex(f => f.id === made.id);

        made.line = fromTemplate(made.line, made.params);
        made.desc = fromTemplate(made.desc, made.params);

        if (at >= 0) FX[at] = made;
        else FX.push(made);
      }

      // A line or desc can be text instead of a function: one string for both languages, or [en, zh]. {0} is the first
      // number or pick the player sees, {1} the second, and {0|hour} writes "1 hour" or "2 hours" in English
      // (the s goes on the last word, so {0|more time} writes "2 more times").
      const fromTemplate = (t, params) => typeof t === 'string' || isArr(t) ? (v, zh) => fill(t, v, zh, params) : t;

      function fill(text, v, zh, params) {
        const shownParams = params.filter(p => p.type !== 'hide');
        const t = isArr(text) ? text[zh ? 1 : 0] : text;

        return t.replace(/\{(\d)(?:\|([^}]+))?\}/g, (_, i, word) => {
          const p = shownParams[+i];

          if (!p) return '';

          if (p.type === 'pick') return opt(p, v[p.key], zh);

          return word && !zh ? plural(v[p.key], word) : fmt(v[p.key]);
        });
      }

      // A drop or a cut stored as a negative fraction (-0.25 reads as 25%).
      const neg = (key, en, zh, def, o) => pct(key, en, zh, def, Object.assign({ scale: -100 }, o));

      // A whole number the game keeps in seconds, shown in hours.
      const hours = (key, en, zh, def, o) => num(key, en, zh, ['h', '小时'], def, Object.assign({ scale: 1 / 3600 }, o));

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
        params: [pct('FloatValue1', 'Discount', '折扣', [10, 15, 20, 25, 30], { scale: -100, add: 100, stack: 'times' }), num('FloatValue0', 'Shelf life under', '保质期小于', ['days', '天'], [5, 5, 5, 5, 5], { stack: 'each' })],
        line: v => '-' + fmt(v.FloatValue1) + '%',
        desc: (v, zh) => zh ? '购买保质期小于' + fmt(v.FloatValue0) + '天的物资时，获得' + fmt(v.FloatValue1) + '%折扣' : 'Get a ' + fmt(v.FloatValue1) + '% discount when purchasing supplies with a shelf life of less than ' + plural(v.FloatValue0, 'day') + '.' });
      fx({ id: 'game:bulk-discount', src: 'game', cat: 'prep', script: 'BigVolume_Discount',
        en: 'Discount on big items', zh: '大件折扣', what: ['Cheaper supplies that take a lot of space.', '购买大体积物资时打折。'],
        params: [pct('FloatValue1', 'Discount', '折扣', [5, 10, 15, 20, 25], { scale: -100, add: 100, stack: 'times' }), num('FloatValue0', 'Size at least', '体积至少', ['', ''], [6, 6, 6, 6, 6], { stack: 'each' })],
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
        desc: (v, zh) => zh ? '可借款上限提高至' + fmt(v.IntValue0) + '（原500）' : 'Loan limit increased to ' + fmt(v.IntValue0) + ' (from 500).',
        sum: {
          params: [num('IntValue0', 'Loan limit raise', '借款上限提高', ['', ''], [300], { step: 100 })],
          line: v => '+' + fmt(v.IntValue0),
          desc: (v, zh) => zh ? '可借款上限提高' + fmt(v.IntValue0) + '（原500）' : 'Raises the loan limit by ' + fmt(v.IntValue0) + ' (from 500).'
        } });
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
        params: [pick('StrValue0', 'Skill', '熟练度', () => named.skills, 'Production'), num('IntValue0', 'Levels', '等级', ['Lv', '级'], [1, 2], { stack: 'max' })],
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
      fx({ id: 'game:brew-speed', src: 'game', cat: 'food', script: 'AE_BrewSpeedUp',
        en: 'Faster brewing', zh: '酿酒加速', what: ['Brewing takes less time.', '酿酒所需时间缩短。'],
        params: [pct('FloatValue0', 'Less time', '时间缩短', [10, 20, 30])],
        line: v => '-' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '酿酒所需时间缩短' + fmt(v.FloatValue0) + '%' : 'Brewing takes ' + fmt(v.FloatValue0) + '% less time.' });
      fx({ id: 'game:brew-yield', src: 'game', cat: 'food', script: 'AE_BrewBottleYield',
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
        desc: (v, zh) => zh ? '随身背包里的物品保质期延长至' + fmt(v.FloatValue0) + '倍' : 'Items in your backpack keep ' + fmt(v.FloatValue0) + 'x as long.',
        sum: {
          params: [pct('FloatValue0', 'Longer shelf life', '保质期延长', [100], { step: 25 })],
          line: v => '+' + fmt(v.FloatValue0) + '%',
          desc: (v, zh) => zh ? '随身背包里的物品保质期延长' + fmt(v.FloatValue0) + '%' : 'Items in your backpack keep ' + fmt(v.FloatValue0) + '% longer.'
        } });
      fx({ id: 'game:quick-rest', src: 'game', cat: 'body', script: 'QuickRest',
        en: 'Rest restores Stamina faster', zh: '休息恢复加快', what: ['Resting brings Stamina back faster.', '通过休息恢复精力更快。'],
        params: [pct('FloatValue0', 'Faster by', '加快', [10, 20, 30])],
        line: v => '+' + fmt(v.FloatValue0) + '%',
        desc: (v, zh) => zh ? '通过休息恢复精力的速度加快' + fmt(v.FloatValue0) + '%' : 'Resting restores Stamina ' + fmt(v.FloatValue0) + '% faster.' });
      fx({ id: 'game:press-nutrition', src: 'game', cat: 'food', script: 'AE_CompressValueRate',
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

      // Food and cooking.
      fx({ cat: 'food', script: 'AE_BrewTierEase', en: 'Better liquor from the same brew', zh: '更易酿出好酒',
        what: ['Liquor quality lines drop, so the same ingredients make better liquor.', '酿酒品质的分界线下移，同样的原料更容易酿出好酒。'],
        params: [num('FloatValue0', 'Lines drop by', '分界线下移', ['', ''], [0.05, 0.1, 0.15], { step: 0.05 })],
        line: '-{0}',
        desc: ['Liquor quality lines drop by {0}, so the same ingredients make better liquor.', '酿酒品质的分界线下移 {0}，同样的原料更容易酿出好酒'] });
      fx({ cat: 'food', script: 'AE_ColdChainKeeper', en: 'Cold storage keeps food longer', zh: '冷库保鲜',
        what: ['Food in cold storage spoils slower.', '冷库中存放的食物变质更慢。'],
        params: [pct('FloatValue0', 'Slower spoiling', '变质减慢', [25, 50])],
        line: '-{0}%',
        desc: ['Food in cold storage spoils {0}% slower.', '冷库中存放的食物变质速度降低{0}%'] });
      fx({ cat: 'food', script: 'AE_CookDishNourish', en: 'Your dishes restore more', zh: '料理更养人',
        what: ['Dishes you cook restore more when you eat them.', '你做的菜食用时恢复效果更好。'],
        params: [pct('FloatValue0', 'More recovery', '恢复加成', [5, 10, 15])],
        line: '+{0}%',
        desc: ['Dishes you cook restore {0}% more when you eat them.', '你做的菜更养人，食用时恢复效果+{0}%'] });
      fx({ cat: 'food', script: 'AE_CookEatPerfectMorale', en: 'Morale from Perfect dishes', zh: '完美料理回复心态',
        what: ['Eating a Perfect quality dish restores extra Morale.', '吃下完美品质的料理时额外回复心态。'],
        params: [num('FloatValue0', 'Extra Morale', '额外心态', ['', '点'], [3, 6, 9])],
        line: '+{0}',
        desc: ['Eating a Perfect quality dish restores {0} extra Morale.', '吃下完美品质的料理时，额外回复{0}点心态'] });
      fx({ cat: 'food', script: 'AE_CookRottenPenaltyReduce', en: 'Smaller penalty for spoiled ingredients', zh: '变质食材惩罚减免',
        what: ['Cooking with spoiled ingredients costs less dish quality.', '用变质食材烹饪时，品质惩罚减少。'],
        params: [pct('FloatValue0', 'Smaller by', '减免', [30, 60, 100], { step: 10 })],
        line: '-{0}%',
        desc: ['The quality penalty for cooking with spoiled ingredients is {0}% smaller.', '用变质食材烹饪时，品质惩罚减免{0}%'] });
      fx({ cat: 'food', script: 'AE_CookTagQualityBonus', en: 'Better creative dishes', zh: '创意料理品质提升',
        what: ['Creative dishes (cooked from a mix of ingredients instead of a recipe) come out better.', '创意料理（凭食材搭配、非固定菜谱）品质更高。'],
        params: [num('FloatValue0', 'More quality', '品质提升', ['', ''], [5, 10, 15], { scale: 100 })],
        line: '+{0}',
        desc: ['Creative dishes (cooked from a mix of ingredients instead of a recipe) get +{0} quality.', '创意料理（凭食材搭配、非固定菜谱）品质+{0}'] });
      fx({ cat: 'food', script: 'AE_CookTagTierFloor', en: 'Lowest tier for creative dishes', zh: '创意料理保底',
        what: ['Creative dishes never come out below the tier you pick.', '创意料理的成品档次有保底。'],
        params: [pick('FloatValue0', 'At least', '至少', () => DISH_TIERS, 2)],
        line: ['{0} or better', '至少{0}'],
        desc: ['Creative dishes always come out at {0} quality or better.', '创意料理的成品档次至少为{0}'] });
      fx({ cat: 'food', script: 'AE_PerfectQualityMultiplier', en: 'More Perfect dishes', zh: '完美料理概率',
        what: ['Better odds of cooking a Perfect dish.', '做菜时做出完美料理的概率提高。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [15, 30, 45])],
        line: '+{0}%',
        desc: ['+{0}% chance to cook a Perfect dish.', '做菜时，做出完美料理的概率+{0}%'] });
      fx({ cat: 'food', script: 'AE_FuelSavingMultiplier', en: 'Stoves burn less fuel', zh: '燃料炉省燃料',
        what: ['Cooking on a fuel stove burns less fuel.', '用燃料炉做菜时燃料消耗降低。'],
        params: [pct('FloatValue0', 'Less fuel', '燃料减少', [20, 40, 60])],
        line: '-{0}%',
        desc: ['Cooking on a fuel stove burns {0}% less fuel.', '用燃料炉做菜时，燃料消耗-{0}%'] });
      fx({ cat: 'food', script: 'AE_TiredOfFoodExtraEats', en: 'Eat a food more before tiring of it', zh: '同种食物多吃几次',
        what: ['You can eat the same food more times before you get tired of it.', '同种食物可以多吃几次才会腻。'],
        params: [num('FloatValue0', 'Extra times', '额外次数', ['', '次'], [1, 2, 3])],
        line: '+{0}',
        desc: ['You can eat the same food {0|more time} before you get tired of it.', '同种食物可以多吃{0}次才会腻'] });
      fx({ cat: 'food', script: 'AE_ExpiredFoodEase', en: 'Shorter sickness from expired food', zh: '过期食物病程缩短',
        what: ['Sickness from eating expired food ends sooner.', '吃过期食物的生病症状时长缩短。'],
        params: [pct('FloatValue0', 'Shorter by', '缩短', [20, 40, 60])],
        line: '-{0}%',
        desc: ['Sickness from eating expired food is {0}% shorter.', '吃过期食物的生病症状时长-{0}%'] });
      fx({ cat: 'food', script: 'AE_StorageShelfLife', en: 'Food keeps longer', zh: '食物保质期延长',
        what: ['All food keeps longer before it goes bad.', '食物保质期延长。'],
        params: [pct('FloatValue0', 'Longer by', '延长', [10, 20, 30])],
        line: '+{0}%',
        desc: ['Food shelf life +{0}%.', '食物保质期+{0}%'] });
      fx({ cat: 'food', script: 'Buff_EnergyFood_304410', en: 'Stamina from non-staple food', zh: '非主食回复精力',
        what: ['Eating anything that is not a staple food gives a few points of Stamina.', '食用非主食类食品时获得随机精力。'],
        params: [num('FloatValue0', 'At least', '至少', ['', '点'], [2, 4, 6]), num('FloatValue1', 'At most', '至多', ['', '点'], [4, 8, 12])],
        line: ['+{0} to {1}', '+{0}到{1}'],
        desc: ['Eating non-staple food gives {0} to {1} Stamina.', '食用非主食类食品时，获得随机{0}到{1}点精力'] });
      fx({ cat: 'food', script: 'Buff_FlavorFood_304510', en: 'Morale from Delicacy food', zh: '特色风味回复心态',
        what: ['Eating food with the Delicacy tag gives a few points of Morale.', '食用「特色风味」食品时获得随机心态。'],
        params: [num('FloatValue0', 'At least', '至少', ['', '点'], [2, 4, 6]), num('FloatValue1', 'At most', '至多', ['', '点'], [4, 8, 12])],
        line: ['+{0} to {1}', '+{0}到{1}'],
        desc: ['Eating food with the Delicacy tag gives {0} to {1} extra Morale.', '食用「特色风味」食品时，获得额外随机{0}到{1}点心态'] });
      fx({ cat: 'food', script: 'Buff_HealthFood_304610', en: 'Health from All-Natural food', zh: '绿色食品回复健康',
        what: ['Eating food with the All-Natural tag gives a few points of Health.', '食用「绿色食品」时获得随机健康。'],
        params: [num('FloatValue0', 'At least', '至少', ['', '点'], [1, 2, 3]), num('FloatValue1', 'At most', '至多', ['', '点'], [3, 6, 9])],
        line: ['+{0} to {1}', '+{0}到{1}'],
        desc: ['Eating food with the All-Natural tag gives {0} to {1} extra Health.', '食用「绿色食品」时，获得额外随机{0}到{1}点健康'] });
      fx({ cat: 'food', script: 'Buff_NutritionFood_304710', en: 'Life from fruit, vegetables and fish', zh: '蔬果鱼类回复生命',
        what: ['Eating fruit, vegetables or fish gives a few points of Life.', '食用水果、蔬菜、鱼类时恢复随机生命。'],
        params: [num('FloatValue0', 'At least', '至少', ['', '点'], [2, 4, 6]), num('FloatValue1', 'At most', '至多', ['', '点'], [3, 6, 9])],
        line: ['+{0} to {1}', '+{0}到{1}'],
        desc: ['Eating fruit, vegetables or fish gives {0} to {1} Life.', '食用水果、蔬菜、鱼类时，可恢复{0}到{1}点生命'] });
      fx({ cat: 'food', script: 'Eats_Anything', en: 'Shorter Appetite Loss from disliked food', zh: '厌食时间缩短',
        what: ['Appetite Loss from eating food you dislike wears off sooner.', '食用不喜爱食物时产生的「厌食」时间缩短。'],
        params: [neg('FloatValue0', 'Shorter by', '缩短', [70, 85, 100])],
        line: '-{0}%',
        desc: ['Appetite Loss from food you dislike is {0}% shorter.', '食用不喜爱食物时产生的「厌食」时间缩短{0}%'] });
      fx({ cat: 'food', script: 'Buff_306110', en: 'More Satiety for 3 days', zh: '三天进食饱腹加成',
        what: ['For 3 days, food fills you more.', '持续3天，进食获得的饱腹效果增加。'],
        params: [pct('FloatValue0', 'More Satiety', '饱腹加成', [100, 200, 300], { step: 25 })],
        line: '+{0}%',
        desc: ['For 3 days, food gives {0}% more Satiety.', '持续3天，进食获得的饱腹效果增加{0}%'] });
      // One food type and one stat. Carnivore and Vegetarian change eight fields at once and stay raw.
      fx({ cat: 'food', script: 'AE_FoodTypeBonus', en: 'More from one food type', zh: '某类食物加成',
        what: ['One kind of food gives more Satiety or Morale.', '食用某类食物时获得更多饱腹或心态。'],
        params: [
          pick('food', 'Food', '食物', () => FOOD_TYPES, 1, { virtual: true, from: v => { const k = foodKeys(v)[0]; return k ? Number(FOOD_FIELD.exec(k)[2]) : 1; } }),
          pick('stat', 'Stat', '属性', () => FOOD_STATS, 'Satiety', { virtual: true, from: v => { const k = foodKeys(v)[0]; return k && /^EF_/.test(k) ? 'Morale' : 'Satiety'; } }),
          pct('ratio', 'More', '提升', [25, 50, 75], { dyn: g => foodField(g.pick.stat, g.pick.food) })
        ],
        test: v => { const keys = foodKeys(v); return keys.length === 1 && v[keys[0]] > 0; },
        zeroOthers: (k, g) => FOOD_FIELD.test(k) && k !== foodField(g.pick.stat, g.pick.food),
        line: '+{2}%',
        desc: ['Eating {0} gives {2}% more {1}.', '食用{0}时，获得的{1}效果增加{2}%'] });
      fx({ id: 'game:cook-recipe', cat: 'food', script: 'UnlockCraftRecipe', en: 'Learn a cooking recipe', zh: '解锁烹饪配方',
        what: ['Learn to cook a dish.', '解锁一道菜的烹饪配方。'],
        params: [hide('StrValue0', 'Cooking'), pick('StrValue1', 'Recipe', '配方', () => named.cooks, '2016', { unknown: recipeUnknown })],
        line: '{0}',
        desc: ['Learn to cook: {0}.', '解锁烹饪配方：{0}'] });

      // Items and carrying.
      fx({ cat: 'items', script: 'UnlockCraftRecipe', en: 'Learn crafting recipes', zh: '解锁制作配方',
        what: ['Learn to craft one or more items.', '解锁一件或几件物品的制作配方。'],
        params: [hide('StrValue0', 'Production'), pick('StrValue1', 'Recipes', '配方', () => named.crafts, '305;337', { unknown: recipeUnknown })],
        line: '{0}',
        desc: ['Learn to craft: {0}.', '解锁制作配方：{0}'] });
      fx({ cat: 'items', script: 'AE_BurdenFlat', en: 'Carry more weight', zh: '负重上限提升',
        what: ['Your backpack holds more weight.', '背包负重上限提升。'],
        params: [num('FloatValue0', 'Extra weight', '额外负重', ['kg', '千克'], [0.5, 0.75, 1], { step: 0.25, scale: 1 / 1200 })],
        line: '+{0}kg',
        desc: ['Your backpack holds {0}kg more.', '背包负重上限提升{0}千克'] });
      fx({ cat: 'items', script: 'AE_HeavyLoad', en: 'Carry more weight (percent)', zh: '负重上限按比例提升',
        what: ['Your backpack holds more weight, by a percent of what it held.', '背包负重上限按比例提升。'],
        params: [pct('FloatValue0', 'More weight', '负重提升', [15, 25])],
        line: '+{0}%',
        desc: ['Your backpack holds {0}% more weight.', '背包负重上限提升{0}%'] });
      fx({ cat: 'items', script: 'AE_HeavyCarryEase', en: 'Move faster when overloaded', zh: '超重减速减少',
        what: ['Carrying too much slows you down less.', '超重时移动速度惩罚减少。'],
        params: [pct('FloatValue0', 'Smaller slowdown', '惩罚减少', [50, 75, 100])],
        line: '-{0}%',
        desc: ['Carrying too much slows you down {0}% less.', '超重时移动速度惩罚减少{0}%'] });

      // Farming.
      fx({ cat: 'farm', script: 'AE_FertilizerMultiplier', en: 'Stronger fertilizer', zh: '肥料增效',
        what: ['Fertilizer works better.', '肥料效果更好。'],
        params: [pct('FloatValue0', 'Stronger by', '增效', [20, 40, 60])],
        line: '+{0}%',
        desc: ['Fertilizer works {0}% better.', '肥料效果+{0}%'] });
      fx({ cat: 'farm', script: 'AE_FlowerGrowthSpeed', en: 'Flowers grow faster', zh: '花卉生长加速',
        what: ['Flowers you plant grow faster.', '种植的花卉生长更快。'],
        params: [pct('FloatValue0', 'Faster by', '加速', [15, 30, 45])],
        line: '+{0}%',
        desc: ['Flowers you plant grow {0}% faster.', '种植花卉的生长速度+{0}%'] });
      fx({ cat: 'farm', script: 'AE_FlowerSeedDiscoverBonus', en: 'Find flower seeds', zh: '发现花朵种子',
        what: ['Better odds of finding flower seeds when you harvest plants.', '收获植物时更容易发现花朵种子。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [5, 10, 15])],
        line: '+{0}%',
        desc: ['+{0}% chance to find flower seeds when you harvest plants.', '收获植物时发现花朵种子的概率+{0}%'] });
      fx({ cat: 'farm', script: 'AE_RareSeedDiscoverBonus', en: 'Find Boston ivy seeds', zh: '发现爬山虎种子',
        what: ['Better odds of finding Boston ivy seeds when you harvest plants.', '收获植物时更容易发现爬山虎种子。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [5, 10, 15])],
        line: '+{0}%',
        desc: ['+{0}% chance to find Boston ivy seeds when you harvest plants.', '收获植物时发现爬山虎种子的概率+{0}%'] });
      fx({ cat: 'farm', script: 'AE_PlantColdResistanceBonus', en: 'Plants handle cold better', zh: '植物更耐寒',
        what: ['Plants hold up better in frost.', '植物更耐霜冻。'],
        params: [num('FloatValue0', 'Cold resistance', '抗寒能力', ['', ''], [1, 2, 3])],
        line: '+{0}',
        desc: ['Plant cold resistance +{0}.', '植物抗寒能力 +{0}，更耐霜冻'] });
      fx({ cat: 'farm', script: 'AE_PlantLightNeedReduce', en: 'Plants need less light', zh: '植物更耐弱光',
        what: ['Plants grow fine with less light.', '植物所需光照降低。'],
        params: [num('FloatValue0', 'Less light needed', '光照需求降低', ['', ''], [1, 2, 3])],
        line: '-{0}',
        desc: ['Plant light requirement -{0}.', '植物光照需求 -{0}，更耐弱光'] });
      fx({ cat: 'farm', script: 'AE_PlantDroughtRateReduce', en: 'Fewer dry crops', zh: '作物少干旱',
        what: ['Crops are less likely to dry out.', '作物出现干旱的概率降低。'],
        params: [pct('FloatValue0', 'Less likely', '概率降低', [20, 40, 60])],
        line: '-{0}%',
        desc: ['Crops are {0}% less likely to dry out.', '作物出现干旱的概率降低{0}%'] });
      fx({ cat: 'farm', script: 'AE_PlantPestRateReduce', en: 'Fewer pests', zh: '作物少虫害',
        what: ['Crops are less likely to get pests.', '作物出现虫害的概率降低。'],
        params: [pct('FloatValue0', 'Less likely', '概率降低', [20, 40, 60])],
        line: '-{0}%',
        desc: ['Crops are {0}% less likely to get pests.', '作物出现虫害的概率降低{0}%'] });
      fx({ cat: 'farm', script: 'AE_PlantWeedRateReduce', en: 'Fewer weeds', zh: '作物少长草',
        what: ['Crops are less likely to grow weeds.', '作物长草的概率降低。'],
        params: [pct('FloatValue0', 'Less likely', '概率降低', [20, 40, 60])],
        line: '-{0}%',
        desc: ['Crops are {0}% less likely to grow weeds.', '作物长草的概率降低{0}%'] });
      fx({ cat: 'farm', script: 'AE_PlantExtraSeed', en: 'Extra seed from harvests', zh: '收获额外种子',
        what: ['A harvest can give a seed of a random crop you can already plant.', '收获时有概率额外获得一颗随机已解锁作物的种子。'],
        params: [pct('FloatValue0', 'Chance', '概率', [10, 20, 30])],
        line: '{0}%',
        desc: ['Each harvest has a {0}% chance to give 1 extra seed of a random crop you can already plant.', '每次收获有{0}%概率额外获得1颗随机已解锁作物的种子'] });
      fx({ cat: 'farm', script: 'AE_PlantPerfectRateBonus', en: 'Bigger harvests', zh: '丰收概率',
        what: ['Better odds of extra yield when you harvest plants.', '收获植物时更容易获得额外产出。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [15, 30, 45])],
        line: '+{0}%',
        desc: ['+{0}% chance of extra yield when you harvest plants.', '植物收获时有额外 +{0}% 的概率获得额外产出'] });
      fx({ cat: 'farm', script: 'AE_PlantSeedDropMultiplier', en: 'More seeds', zh: '种子更多',
        what: ['Better odds of getting seeds.', '获得种子的概率提高。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [10, 20, 30])],
        line: '+{0}%',
        desc: ['+{0}% chance to get seeds.', '提高种子获得概率 {0}%'] });
      fx({ cat: 'farm', script: 'AE_PlantSlowGrowHighYield', en: 'Slow growth, big harvest', zh: '慢长多收',
        what: ['Crops take longer to grow, then give more at harvest and more planting experience.', '作物生长更慢，收获时产出更多、种植经验更多。'],
        params: [
          pct('FloatValue0', 'More yield', '产出增加', [60, 120, 180]),
          pct('FloatValue1', 'Longer growth', '生长延长', [50, 100, 150]),
          pct('FloatValue2', 'More experience', '经验增加', [50, 100, 150])
        ],
        line: '+{0}%',
        desc: ['Crops take {1}% longer to grow, give {0}% more at harvest and {2}% more planting experience.', '作物生长时间延长 {1}%，收获时常规产出增加 {0}%、种植经验增加 {2}%'] });
      fx({ id: 'game:pot-no-till', cat: 'farm', script: 'AE_SkipTillSoilSmallPot', scripts: POTS.map(p => p.script),
        en: 'No tilling in flowerpots', zh: '花盆免翻土',
        what: ['One size of flowerpot needs no tilling after a harvest, so you can replant right away. No numbers.', '某种花盆收获后免翻土，可直接复种。没有数值。'],
        params: [pick('pot', 'Flowerpot', '花盆', () => POTS, 'Small', { virtual: true, from: (v, script) => { const m = /^AE_SkipTillSoil(.*)Pot$/.exec(script); return m ? m[1] : 'Small'; } })],
        scriptOf: g => 'AE_SkipTillSoil' + g.pick.pot + 'Pot',
        line: '{0}',
        desc: ['{0}s need no tilling after a harvest and can be replanted right away.', '{0}收获后免翻土，可直接复种'] });
      fx({ cat: 'farm', script: 'AE_WitheredCompostBonus', en: 'Fertilizer from withered plants', zh: '枯萎堆肥',
        what: ['Harvesting a withered plant gives extra fertilizer.', '收获枯萎植物时额外产出肥料。'],
        params: [hide('IntValue0', 15501), num('IntValue1', 'Extra fertilizer', '额外肥料', ['', '个'], [1, 2])],
        line: '+{0}',
        desc: ['Harvesting a withered plant gives {0} extra fertilizer.', '枯萎收获额外产出 {0} 个肥料'] });
      fx({ cat: 'farm', script: 'AE_VaseLifeExtend', en: 'Vase flowers last longer', zh: '花瓶保鲜',
        what: ['Flowers in vases stay fresh longer.', '花瓶里的花保鲜更久。'],
        params: [pct('FloatValue0', 'Longer by', '延长', [25, 50, 75])],
        line: '+{0}%',
        desc: ['Flowers in vases stay fresh {0}% longer.', '花瓶里的花保鲜时长+{0}%'] });
      fx({ cat: 'farm', script: 'AE_VaseMoraleBonus', en: 'More Morale from vase flowers', zh: '插花心态加成',
        what: ['Floral Fragrance from flower arranging gives more Morale.', '插花带来的花香心态加成更高。'],
        params: [pct('FloatValue0', 'More Morale', '心态加成', [10, 20, 30])],
        line: '+{0}%',
        desc: ['Floral Fragrance from flower arranging gives {0}% more Morale.', '插花带来的花香心态加成+{0}%'] });

      // Home and power.
      fx({ cat: 'home', script: 'AE_DeviceHeatBonus', en: 'Heaters warm more', zh: '暖气增温',
        what: ['Each running air conditioner or electric heater raises the temperature more.', '每台运行中的空调或电暖器提升更多温度。'],
        params: [num('FloatValue0', 'Extra warmth', '额外温度', ['', ''], [1, 2, 3])],
        line: '+{0}',
        desc: ['Each running Air Conditioning or Electric Heater raises the temperature by {0} more.', '每台运行中的空调或电暖器提升的温度+{0}'] });
      fx({ cat: 'home', script: 'AE_GeneratorFuelSaving', en: 'Generators burn less fuel', zh: '发电机省燃料',
        what: ['Generators burn less fuel.', '发电机燃料消耗降低。'],
        params: [pct('FloatValue0', 'Less fuel', '燃料减少', [15, 30, 45])],
        line: '-{0}%',
        desc: ['Generators burn {0}% less fuel.', '发电机燃料消耗-{0}%'] });
      fx({ cat: 'home', script: 'AE_GeneratorFuelSlotBonus', en: 'More generator fuel slots', zh: '发电机燃料槽',
        what: ['Diesel generators hold more fuel.', '柴油发电机的燃料槽增加。'],
        params: [num('FloatValue0', 'Extra slots', '额外格数', ['', '格'], [1, 2, 3])],
        line: '+{0}',
        desc: ['Diesel generators get {0|more fuel slot}.', '柴油发电机的燃料槽 +{0}'] });
      fx({ cat: 'home', script: 'AE_PowerConsumerSaving', en: 'Furniture uses less power while running', zh: '家具用电降低',
        what: ['Furniture draws less power while you use it.', '使用时家具用电降低。'],
        params: [pct('FloatValue0', 'Less power', '用电降低', [15, 30, 45])],
        line: '-{0}%',
        desc: ['Furniture uses {0}% less power while in use.', '使用时家具用电 -{0}%'] });
      fx({ cat: 'home', script: 'AE_PowerGeneratorEfficiency', en: 'Diesel generators make more power', zh: '柴油发电增效',
        what: ['Diesel generators make more power.', '柴油发电机产电效率提高。'],
        params: [pct('FloatValue0', 'More power', '产电提高', [15, 30, 45])],
        line: '+{0}%',
        desc: ['Diesel generators make {0}% more power.', '柴油发电机产电效率 +{0}%'] });
      fx({ cat: 'home', script: 'AE_PowerStorageMultiplier', en: 'Store more power', zh: '储电上限提升',
        what: ['Your batteries hold more power.', '储电量上限提高。'],
        params: [pct('FloatValue0', 'More storage', '储电提高', [50, 100, 150], { step: 25 })],
        line: '+{0}%',
        desc: ['Max power capacity +{0}%.', '储电量上限+{0}%'] });
      fx({ cat: 'home', script: 'AE_PowerCostByGroup', en: 'Some devices use more power', zh: '部分设备耗电增加',
        what: ['One kind of device uses more power. The game uses this for the Power Hog and Costly Cold statuses.', '某类设备耗电增加。游戏用于「耗电大户」和「冷藏昂贵」状态。'],
        params: [pick('IntValue0', 'Devices', '设备', () => POWER_GROUPS, 2), neg('FloatValue0', 'More power use', '耗电增加', [60])],
        line: '+{1}%',
        desc: ['{0} use {1}% more power.', '{0}耗电增加{1}%'] });
      fx({ cat: 'home', script: 'AE_HomePackageMax', en: 'More furniture packages at the entryway', zh: '玄关包裹上限',
        what: ['The entryway can hold more furniture packages.', '玄关可囤的家具包裹更多。'],
        params: [num('FloatValue0', 'Extra packages', '额外包裹', ['', '个'], [1, 2, 3])],
        line: '+{0}',
        desc: ['The Entryway holds {0|more furniture package}.', '玄关可囤的家具包裹上限 +{0}'] });
      fx({ cat: 'home', script: 'AE_PersonalColdResist', en: 'Feel warmer', zh: '体感温度提升',
        what: ['Thermal Comfort moves up a step (Cold, Chilly, Warming, Comfortable), up to Comfortable.', '体感温度提升一档（受冷、微寒、取暖、舒适），最高至舒适。'],
        params: [num('FloatValue0', 'Steps warmer', '提升档数', ['', '档'], [1, 2])],
        line: '+{0}',
        desc: ['Thermal Comfort is {0|step} warmer, up to Comfortable.', '体感温度提升{0}档（最高至舒适）'] });
      fx({ cat: 'home', script: 'AE_EndlessBasementTemp', modes: 'endless', en: 'Warmer basement', zh: '地下室升温',
        what: ['The basement is warmer.', '地下室的温度提高。'],
        params: [num('FloatValue0', 'Warmer by', '升温', ['', '度'], [1, 2, 3])],
        line: '+{0}',
        desc: ['The basement is {0|degree} warmer.', '地下室的温度提高 {0} 度'] });
      fx({ cat: 'home', script: 'AE_EndlessIndoorTemp', modes: 'endless', en: 'Warmer indoors', zh: '室内升温',
        what: ['Every indoor area is warmer.', '全部室内区域的温度提高。'],
        params: [num('FloatValue0', 'Warmer by', '升温', ['', '度'], [1, 2, 3])],
        line: '+{0}',
        desc: ['Every indoor area is {0|degree} warmer.', '全部室内区域的温度提高 {0} 度'] });
      fx({ cat: 'home', script: 'AE_EndlessCookHeat', modes: 'endless', en: 'Stoves warm the room', zh: '灶台供暖',
        what: ['A stove in use warms the area it stands in.', '烹饪时灶台为所在区域供暖。'],
        params: [num('FloatValue0', 'Warmer by', '升温', ['', '度'], [2, 4, 6])],
        line: '+{0}',
        desc: ['A stove in use warms the area it stands in by {0|degree}.', '烹饪时灶台为所在区域供暖 {0} 度'] });

      // Traps and defense.
      fx({ cat: 'defense', script: 'AE_IvyShieldBonus', en: 'Stronger vine shields', zh: '藤蔓护盾增强',
        what: ['Vines give your defense structures a stronger shield.', '藤蔓为防御设施提供的护盾更强。'],
        params: [pct('FloatValue0', 'Stronger by', '增强', [15, 30, 45])],
        line: '+{0}%',
        desc: ['Vines give defense structures a {0}% stronger shield.', '藤蔓为防御设施提供的护盾+{0}%'] });
      fx({ cat: 'defense', script: 'AE_MolotovDamageBonus', en: 'Molotovs burn harder', zh: '燃烧瓶伤害提升',
        what: ['Molotov Cocktails deal more burn damage.', '燃烧瓶的爆燃伤害提高。'],
        params: [pct('FloatValue0', 'More damage', '伤害提升', [25, 50, 75])],
        line: '+{0}%',
        desc: ['Molotov Cocktail burn damage +{0}%.', '燃烧瓶的爆燃伤害提升{0}%'] });
      fx({ cat: 'defense', script: 'AE_MolotovRadiusBonus', en: 'Wider Molotov fire', zh: '燃烧瓶范围扩大',
        what: ['Molotov Cocktails burn a wider area.', '燃烧瓶的爆燃范围更大。'],
        params: [pct('FloatValue0', 'Wider by', '扩大', [20, 40, 60])],
        line: '+{0}%',
        desc: ['Molotov Cocktail burn range +{0}%.', '燃烧瓶的爆燃范围扩大{0}%'] });
      fx({ cat: 'defense', script: 'AE_TowerBackupPower', en: 'Defenses run during outages', zh: '停电备用电池',
        what: ['Electric traps and chainsaws keep running at half speed when the power is out. No numbers.', '停电时电网和电锯仍能以半速运转。没有数值。'],
        params: [],
        line: ['Half speed in outages', '停电半速运转'],
        desc: ['Electric traps and chainsaws keep running at half speed during power outages.', '停电时电网和电锯仍能以半速继续运转'] });
      fx({ cat: 'defense', script: 'AE_TowerDeviceDamageBonus', en: 'Defenses hit harder', zh: '防御装置攻击力',
        what: ['Spikes, electric traps, chainsaws and other defense structures deal more damage.', '尖刺、电网、电锯等防御装置攻击力提高。'],
        params: [pct('FloatValue0', 'More damage', '攻击力提升', [15, 30, 45])],
        line: '+{0}%',
        desc: ['Spikes, electric traps, chainsaws and other defense structures deal {0}% more damage.', '尖刺、电网、电锯等防御装置的攻击力提升{0}%'] });
      fx({ cat: 'defense', script: 'AE_TowerDeviceMaxDurability', en: 'Tougher defenses', zh: '防御装置耐久提升',
        what: ['Defense structures have more max Durability.', '防御装置的耐久上限提高。'],
        params: [pct('FloatValue0', 'More Durability', '耐久提升', [30, 60, 90])],
        line: '+{0}%',
        desc: ['Defense structures get {0}% more max Durability.', '防御装置的耐久上限提升{0}%'] });
      fx({ cat: 'defense', script: 'AE_TowerDeviceSpeedBonus', en: 'Defenses attack faster', zh: '防御装置攻速',
        what: ['Defense structures wait less between attacks.', '防御装置的攻击间隔缩短。'],
        params: [pct('FloatValue0', 'Shorter wait', '间隔缩短', [15, 30, 45])],
        line: '-{0}%',
        desc: ['Defense structures wait {0}% less between attacks.', '防御装置的攻击间隔缩短{0}%'] });
      fx({ cat: 'defense', script: 'AE_TowerHitSlowEnhance', en: 'Stronger stun and slow', zh: '麻痹减速增强',
        what: ['When a defense hits a zombie, its stun and slow work better.', '装置命中丧尸时的麻痹减速效果更强。'],
        params: [pct('FloatValue0', 'Stronger by', '增强', [15, 30, 45])],
        line: '+{0}%',
        desc: ['Stun and slow from defense hits are {0}% stronger.', '装置命中丧尸时的麻痹减速效果增强{0}%'] });
      fx({ cat: 'defense', script: 'AE_TowerWaveKillMorale', en: 'Morale from kills', zh: '击杀回复心态',
        what: ['After each wave of defense, recover Morale for every 5 kills, up to 10 a wave.', '每波防守结束后按击杀数恢复心态，单波最多10点。'],
        params: [num('FloatValue0', 'Morale per 5 kills', '每5杀心态', ['', '点'], [1, 2, 3], { scale: 5 })],
        line: '+{0}',
        desc: ['After each wave, recover {0} Morale for every 5 kills, up to 10 a wave.', '每波防守结束后按击杀数恢复心态，每5杀恢复{0}点，单波最多10点'] });
      fx({ cat: 'defense', script: 'AE_TrapBaitCapacityBonus', en: 'Bigger bait bay', zh: '诱饵仓扩容',
        what: ['The Auto Trap Cage holds more bait (normally 8 slots).', '自动诱捕笼的诱饵仓更大（原本8格）。'],
        params: [num('FloatValue0', 'Extra slots', '额外格数', ['', '格'], [1, 2])],
        line: '+{0}',
        desc: ['The Auto Trap Cage bait bay gets {0|more slot} (normally 8).', '自动诱捕笼的诱饵仓增加 {0} 格（原本 8 格）'] });
      fx({ cat: 'defense', script: 'AE_TrapBaitMultiplier', en: 'Better bait', zh: '诱饵增效',
        what: ['Bait works better in every trap.', '所有陷阱的诱饵效果更好。'],
        params: [pct('FloatValue0', 'Better by', '提升', [30, 60, 90])],
        line: '+{0}%',
        desc: ['Bait works {0}% better in every trap.', '提升所有陷阱的诱饵系数+{0}%'] });
      fx({ cat: 'defense', script: 'AE_TrapDurabilityBonus', en: 'Tougher traps', zh: '陷阱耐久提升',
        what: ['Traps have more max Durability.', '陷阱最大耐久提高。'],
        params: [pct('FloatValue0', 'More Durability', '耐久提升', [20, 40, 60])],
        line: '+{0}%',
        desc: ['Trap max Durability +{0}%.', '陷阱最大耐久 +{0}%'] });
      fx({ cat: 'defense', script: 'AE_TrapIntervalReduce', en: 'Traps catch sooner', zh: '陷阱捕获更快',
        what: ['Traps wait less between catches.', '陷阱捕获间隔缩短。'],
        params: [pct('FloatValue0', 'Shorter wait', '间隔缩短', [5, 10, 15])],
        line: '-{0}%',
        desc: ['Traps wait {0}% less between catches.', '陷阱捕获间隔 -{0}%'] });
      fx({ cat: 'defense', script: 'AE_TrapNightCapture', en: 'Better catches at night', zh: '夜间捕获加成',
        what: ['From 7 PM to 7 AM, traps are more likely to catch prey.', '夜间（19点至次日7点）陷阱更容易捕获猎物。'],
        params: [pct('FloatValue0', 'More likely', '权重提高', [15, 30])],
        line: '+{0}%',
        desc: ['From 7 PM to 7 AM, traps are {0}% more likely to catch prey.', '夜间（19点至次日7点）陷阱捕获猎物的权重提高{0}%'] });
      fx({ cat: 'defense', script: 'AE_TrapPreySeedBonus', en: 'Seeds from trapped prey', zh: '猎物带种子',
        what: ['Better odds of finding seeds when you collect trapped prey.', '取走陷阱猎物时更容易获得种子。'],
        params: [pct('FloatValue0', 'Better odds', '概率提高', [5, 10, 15])],
        line: '+{0}%',
        desc: ['+{0}% chance to find seeds when you collect trapped prey.', '取走陷阱猎物时获得种子的概率+{0}%'] });
      // The game starts a rat's chance to bolt at 70%, and each 0.1 also keeps rats in storage 0.5 seconds longer.
      fx({ cat: 'defense', script: 'AE_RatCatchEase', en: 'Catch rats by hand', zh: '徒手抓鼠',
        what: ['Rats are less likely to bolt when you grab them, and rats in storage stay longer.', '徒手抓鼠更容易得手，储物家具里的老鼠停留更久。'],
        params: [pct('FloatValue0', 'Less likely to bolt', '逃跑概率降低', [10, 20, 30])],
        line: '-{0}%',
        desc: (v, zh) => zh
          ? '徒手抓鼠更容易得手：老鼠受惊逃跑的概率由 70% 降至 ' + fmt(Math.max(0, 70 - v.FloatValue0)) + '%，储物家具里的老鼠多停留 ' + fmt(v.FloatValue0 / 20) + ' 秒'
          : 'Rats are easier to catch by hand: the chance a rat bolts drops from 70% to ' + fmt(Math.max(0, 70 - v.FloatValue0)) + '%, and rats in storage stay ' + fmt(v.FloatValue0 / 20) + 's longer.' });
      fx({ cat: 'defense', script: 'AE_EndlessTrapCost', modes: 'endless', en: 'Traps wear less', zh: '陷阱损耗降低',
        what: ['Each catch uses less trap Durability.', '陷阱每次捕获消耗的耐久降低。'],
        params: [pct('FloatValue0', 'Less wear', '损耗降低', [30, 60, 90])],
        line: '-{0}%',
        desc: ['Each catch uses {0}% less trap Durability.', '陷阱每次捕获消耗的耐久降低 {0}%'] });
      fx({ cat: 'defense', script: 'AE_EndlessEarlyWaveCut', modes: 'endless', en: 'Weaker early hordes', zh: '早期尸潮削弱',
        what: ['For the first 10 days, zombies in hordes have less health.', '前10天的尸潮丧尸血量降低。'],
        params: [pct('FloatValue0', 'Less health', '血量降低', [50, 75, 90])],
        line: '-{0}%',
        desc: ['For the first 10 days, zombies in hordes have {0}% less health.', '前 10 天的尸潮丧尸血量降低 {0}%'] });

      // Money and trade.
      const WISH_BONUSES = [[303311, 'Wish Reward I'], [303321, 'Wish Reward II'], [303331, 'Wish Reward III']].map((w, i) => ({ v: w[0], en: w[1], zh: '愿望奖励 Lv' + (i + 1) }));

      fx({ cat: 'money', script: 'AE_DroneForagingExtraTrip', en: 'More drone foraging trips', zh: '无人机拾荒次数',
        what: ['The drone can go foraging more times each day.', '无人机每日可拾荒派遣次数增加。'],
        params: [num('FloatValue0', 'Extra trips', '额外次数', ['', '次'], [1, 2])],
        line: '+{0}',
        desc: ['The drone can go foraging {0|more time} each day.', '无人机每日可拾荒派遣次数 +{0}'] });
      fx({ cat: 'money', script: 'AE_StrangerTradeBonusGift', en: 'Gifts from trades', zh: '交易附赠礼物',
        what: ['A trade can come with a small free gift.', '成交时有概率额外获赠小礼物。'],
        params: [pct('FloatValue0', 'Chance', '概率', [15, 30, 45])],
        line: '{0}%',
        desc: ['Each trade has a {0}% chance to come with a small gift.', '每次成交有{0}%概率额外获赠1件小礼物'] });
      fx({ cat: 'money', script: 'AE_StrangerTradeDealMorale', en: 'Morale from trades', zh: '成交回复心态',
        what: ['Each trade restores some Morale.', '每次成交额外回复心态。'],
        params: [num('FloatValue0', 'Extra Morale', '额外心态', ['', '点'], [2, 4, 6])],
        line: '+{0}',
        desc: ['Each trade restores {0} extra Morale.', '每次成交额外回 {0} 点心态'] });
      fx({ cat: 'money', script: 'AE_MoraleToPointBoost', en: 'Better Morale to points rate', zh: '心态生金',
        what: ['At the end of each day, Morale turns into Survival Points at a better rate (normally 5 Morale for 1 point).', '每日结算时，心态转化生存点的效率提升（原本5:1）。'],
        params: [num('FloatValue0', 'Less Morale per point', '每点少用心态', ['', ''], [1, 2, 3])],
        line: (v, zh) => fmt(Math.max(1, 5 - v.FloatValue0)) + ':1',
        desc: (v, zh) => zh
          ? '每日结算时，心态转化生存点效率提升（5:1 到 ' + fmt(Math.max(1, 5 - v.FloatValue0)) + ':1）'
          : 'At the end of each day, ' + fmt(Math.max(1, 5 - v.FloatValue0)) + ' Morale turns into 1 Survival Point, down from 5.' });
      fx({ cat: 'money', script: 'AE_StockpileDailyPoints', en: 'Survival Points from stored food', zh: '储粮返生存点',
        what: ['Each day, food stored at home pays Survival Points, up to 3 a day.', '每日结算按家中食物储备返还生存点，每日最多3点。'],
        params: [num('FloatValue0', 'Points per 500 Satiety', '每500饱腹返点', ['', '点'], [1, 2, 3], { scale: 500 })],
        line: '+{0}',
        desc: ['Each day, get {0} Survival Points for every 500 Satiety of food stored at home, up to 3 a day.', '每日结算按家中食物储备返还生存点：每500点储备饱腹值返{0}点，每日最多3点'] });
      fx({ cat: 'money', script: 'AE_PioneerCrisisBelief', en: 'Planning Points from crises', zh: '危机获得信念点',
        what: ['Each crisis you get through gives Planning Points.', '每克服一次危机获得信念点。'],
        params: [num('IntValue0', 'Planning Points', '信念点', ['', '点'], [5, 10, 15])],
        line: '+{0}',
        desc: ['Get {0} Planning Points for each crisis you get through.', '每成功克服一次危机，获得 {0} 点信念点'] });
      fx({ cat: 'money', script: 'Annual_Bonus_0', en: 'Points now, fewer later', zh: '短期激励',
        what: ['Extra Survival Points each day for 5 days, then 1 fewer each day for the next 5.', '此后5天每日多得生存点，之后5天每日少得1点。'],
        params: [hide('IntValue1', 302611), num('IntValue0', 'Extra points a day', '每日多得', ['', '点'], [3, 6, 9])],
        line: '+{0}',
        desc: ['Get {0} extra Survival Points each day for the next 5 days, then 1 fewer each day for the 5 days after.', '此后5天每日结算多得{0}点生存点，之后5天每日少得1点'] });
      fx({ cat: 'money', script: 'CrisisSettlementBonus', en: 'Points during a crisis', zh: '危机红利',
        what: ['During a crisis, each day pays extra Survival Points.', '危机期间每日结算额外获得生存点。'],
        params: [num('IntValue0', 'Extra points', '额外生存点', ['', '点'], [3, 6, 9])],
        line: '+{0}',
        desc: ['During a crisis, get {0} extra Survival Points at the end of each day.', '危机期间，每日结算额外获得{0}点生存点'] });
      fx({ cat: 'money', script: 'Daily_Closing_Plan', en: 'Flat Survival Points each day', zh: '每日固定生存点',
        what: ['Each day pays a few more Survival Points.', '每日结算时多得生存点。'],
        params: [num('IntValue0', 'Extra points', '额外生存点', ['', '点'], [1, 2])],
        line: '+{0}',
        desc: ['Get {0} more Survival Points at the end of each day.', '每日结算时获得的生存点增加{0}点'] });
      fx({ cat: 'money', script: 'Health_Reward', en: 'Points for a healthy day', zh: '健康奖励',
        what: ['A day that ends with no bad statuses pays extra Survival Points.', '结算时若没有任何不良状态，额外获得生存点。'],
        params: [num('IntValue0', 'Extra points', '额外生存点', ['', '点'], [2, 4, 6])],
        line: '+{0}',
        desc: ['Get {0} extra Survival Points at the end of a day with no bad statuses.', '结算时若没有任何不良状态，则额外获得生存点（{0}点）'] });
      fx({ cat: 'money', script: 'Task_Bonus', en: 'Points for missions', zh: '任务奖金',
        what: ['Each mission you finish pays extra Survival Points.', '每次完成任务额外获得生存点。'],
        params: [num('IntValue0', 'Extra points', '额外生存点', ['', '点'], [1, 2, 3])],
        line: '+{0}',
        desc: ['Get {0} extra Survival Points each time you finish a mission.', '每次完成任务后获得额外生存点 {0}点'] });
      // Each status in WISH_BONUSES gives 1 point for 24 hours. The game gives one per level of Wish Reward.
      fx({ cat: 'money', script: 'PostDisaster_AddWishRewardBuff', en: 'A point for each wish', zh: '愿望奖励生存点',
        what: ['Fulfilling a wish gives 1 extra Survival Point. The game has one of these per level of Wish Reward.', '完成愿望后额外获得1点生存点。游戏中愿望奖励的每一级各有一个。'],
        params: [pick('IntValue0', 'Bonus', '加成', () => WISH_BONUSES, 303311)],
        line: '+1',
        desc: ['Fulfilling a wish gives 1 extra Survival Point ({0}).', '完成愿望后额外获得1点生存点（{0}）'] });
      fx({ cat: 'money', script: 'Buff_305810', en: 'Trade Life for points', zh: '生命换生存点',
        what: ['Lose some Life right away to get Survival Points.', '立即减少生命值，换取生存点。'],
        params: [hide('FloatValue0', -20), num('IntValue0', 'Survival Points', '生存点', ['', '点'], [5, 10, 15]), num('FloatValue1', 'Life lost', '失去生命', ['', '点'], [20], { scale: -1 })],
        line: '+{0}',
        desc: ['Lose {1} Life to get {0} Survival Points.', '主动减少{1}点生命值，以换取{0}点生存点'] });
      fx({ cat: 'money', script: 'Buff_305910', en: 'Trade supplies for points', zh: '物资换生存点',
        what: ['Lose some random supplies right away to get Survival Points.', '随机失去物资，换取生存点。'],
        params: [num('IntValue0', 'Survival Points', '生存点', ['', '点'], [8, 16, 24]), num('IntValue1', 'Items lost', '失去物资', ['', '件'], [3, 6, 9])],
        line: '+{0}',
        desc: ['Lose {1} random supply items to get {0} Survival Points.', '随机失去{1}件物资，以换取{0}点生存点'] });
      fx({ cat: 'money', script: 'Buff_305410', en: 'Double crisis rewards', zh: '危机奖励翻倍',
        what: ['Every reward for getting through a crisis is doubled. No numbers.', '成功度过危机后获得的所有奖励翻倍。没有数值。'],
        params: [],
        line: ['x2', '翻倍'],
        desc: ['Rewards for getting through a crisis are doubled.', '成功度过危机后，获得的所有奖励翻倍'] });

      // Camps and aid, all Endless mode.
      fx({ cat: 'camps', script: 'AE_EndlessAidCooldown', modes: 'endless', en: 'Ask for aid more often', zh: '求援冷却缩短',
        what: ['Asking for aid has a shorter cooldown (normally 12 days).', '主动求援的冷却时间缩短（原本12天）。'],
        params: [num('FloatValue0', 'Days shorter', '缩短天数', ['', '天'], [1, 2, 3])],
        line: '-{0}',
        desc: (v, zh) => zh ? '主动求援的冷却时间缩短至 ' + fmt(Math.max(0, 12 - v.FloatValue0)) + ' 天' : 'Asking for aid has a ' + plural(Math.max(0, 12 - v.FloatValue0), 'day') + ' cooldown, down from 12.' });
      fx({ cat: 'camps', script: 'AE_EndlessAidForecast', modes: 'endless', en: 'Aid requests announced early', zh: '求援提前预告',
        what: ['Camp aid requests show up a day early, with whether they need supplies or defense. No numbers.', '营地求援提前一天预告，并标明需要物资还是协防。没有数值。'],
        params: [],
        line: ['1 day early', '提前1天'],
        desc: ['Camp aid requests are announced a day early, with whether they need supplies or defense.', '营地求援提前一天预告，并标明对方需要物资还是协防'] });
      fx({ cat: 'camps', script: 'AE_EndlessAidRetreat', modes: 'endless', en: 'Reinforcements draw more zombies', zh: '援军引走更多丧尸',
        what: ['Reinforcements you call for draw away more zombies.', '主动求援时，援军能引走更多丧尸。'],
        params: [pct('FloatValue0', 'More zombies', '引走更多', [50, 100, 150], { step: 25 })],
        line: '+{0}%',
        desc: ['Reinforcements you call for draw away {0}% more zombies.', '主动求援时，援军引走的丧尸增加{0}%'] });
      fx({ cat: 'camps', script: 'AE_EndlessCampAidRound', modes: 'endless', en: 'Camps can call for aid more', zh: '营地求援次数',
        what: ['The Recycling Yard, East Camp and Rooftop Nursery can each call for aid more times.', '回收站、城东营地、育苗棚的求援次数上限提高。'],
        params: [num('FloatValue0', 'Extra calls', '额外次数', ['', '次'], [1, 2, 3])],
        line: '+{0}',
        desc: ['The Recycling Yard, East Camp and Rooftop Nursery can each call for aid {0|more time}.', '回收站、城东营地、育苗棚的求援次数上限提高 {0} 次'] });
      fx({ cat: 'camps', script: 'AE_EndlessCampDailyCap', modes: 'endless', en: 'Higher daily camp rewards', zh: '营地回馈上限',
        what: ['Camps can pay more each day (normally up to 3 points).', '每日营地回馈上限提高（原本3点）。'],
        params: [num('FloatValue0', 'Higher cap', '上限提高', ['', '点'], [1, 2, 3])],
        line: '+{0}',
        desc: (v, zh) => zh ? '每日营地回馈上限提高至 ' + fmt(3 + v.FloatValue0) + ' 点' : 'Daily camp rewards are capped at ' + plural(3 + v.FloatValue0, 'point') + ', up from 3.' });
      fx({ cat: 'camps', script: 'AE_EndlessCampDailyDouble', modes: 'endless', en: 'Double camp rewards', zh: '营地回馈翻倍',
        what: ['Daily camp rewards double while 4 or more camps you helped are still standing. No numbers.', '同时有4个及以上你帮助过的营地存活时，每日营地回馈翻倍。没有数值。'],
        params: [],
        line: ['x2', '翻倍'],
        desc: ['Daily camp rewards are doubled while 4 or more camps you have helped remain.', '同时有 4 个及以上你帮助过的营地存活时，每日营地回馈翻倍'] });
      fx({ cat: 'camps', script: 'AE_EndlessCampEvacPoint', modes: 'endless', en: 'More points for camp evacuations', zh: '协助撤离生存点',
        what: ['Helping a camp evacuate pays more Survival Points (normally 10).', '协助营地撤离的一次性生存点提高（原本10点）。'],
        params: [num('FloatValue0', 'Extra points', '额外生存点', ['', '点'], [10, 20, 30])],
        line: '+{0}',
        desc: (v, zh) => zh ? '协助营地撤离的一次性生存点提高至 ' + fmt(10 + v.FloatValue0) + ' 点' : 'Helping a camp evacuate pays ' + plural(10 + v.FloatValue0, 'Survival Point') + ', up from 10.' });
      fx({ cat: 'camps', script: 'AE_EndlessCampRestock', modes: 'endless', en: 'Camps restock more', zh: '营地补货增加',
        what: ['Stable camps restock more items each day.', '状态稳定的营地每天多补货。'],
        params: [num('FloatValue0', 'Extra items', '额外件数', ['', '件'], [1, 2, 3])],
        line: '+{0}',
        desc: ['Stable camps restock {0|more item} each day.', '状态稳定的营地每天多补 {0} 件货'] });
      fx({ cat: 'camps', script: 'AE_EndlessCrisisPoint', modes: 'endless', en: 'Points for each crisis', zh: '危机生存点',
        what: ['Each crisis you get through pays extra Survival Points.', '每度过一次危机额外获得生存点。'],
        params: [num('FloatValue0', 'Extra points', '额外生存点', ['', '点'], [5, 10, 15])],
        line: '+{0}',
        desc: ['Get {0} extra Survival Points each time you get through a crisis.', '每度过一次危机，额外获得 {0} 点生存点'] });
      fx({ cat: 'camps', script: 'AE_EndlessEarlyDailyPoint', modes: 'endless', en: 'Points in the first 10 days', zh: '前10天生存点',
        what: ['For the first 10 days, each day pays extra Survival Points.', '前10天每日结算额外获得生存点。'],
        params: [num('FloatValue0', 'Extra points', '额外生存点', ['', '点'], [3, 5, 8])],
        line: '+{0}',
        desc: ['For the first 10 days, get {0} extra Survival Points at the end of each day.', '前 10 天每日结算时额外获得 {0} 点生存点'] });
      fx({ cat: 'camps', script: 'AE_EndlessFallDefer', modes: 'endless', en: 'Sites fall later', zh: '探索点推迟沦陷',
        what: ['Every exploration site falls 5 days later, and shows a countdown 5 days before it falls. No numbers.', '所有探索点的沦陷推迟5天，并在沦陷前5天显示倒计时。没有数值。'],
        params: [],
        line: ['5 days later', '推迟5天'],
        desc: ['All exploration sites fall 5 days later, with a countdown shown 5 days before they fall.', '所有探索点的沦陷推迟 5 天，并在沦陷前 5 天显示倒计时'] });
      fx({ cat: 'camps', script: 'AE_EndlessPledgeReduce', modes: 'endless', en: 'Smaller swarms from defense pledges', zh: '协防尸群缩小',
        what: ['Zombie swarms drawn in by a defense pledge are smaller.', '因协防承诺引来的尸群规模降低。'],
        params: [pct('FloatValue0', 'Smaller by', '降低', [25, 50, 75])],
        line: '-{0}%',
        desc: ['Zombie swarms drawn in by a defense pledge are {0}% smaller.', '因协防承诺引来的尸群规模降低 {0}%'] });

      // Wishes and events.
      fx({ cat: 'events', script: 'Clear_Requirements', en: 'More general wishes', zh: '大类愿望更多',
        what: ['Wishes more often ask for a general kind of thing, like food or tools.', '许愿时更容易出现大类愿望（如食物、工具）。'],
        params: [pct('FloatValue0', 'More likely', '概率增加', [20, 40, 60])],
        line: '+{0}%',
        desc: ['+{0}% chance that a wish asks for a general kind of thing, like food or tools.', '许愿时出现大类愿望（如需要食物、工具等）的概率增加 {0}%'] });
      fx({ cat: 'events', script: 'Focused_Wish', en: 'More wishes', zh: '许愿更频繁',
        what: ['Wish events show up more often.', '许愿事件更容易触发。'],
        params: [neg('FloatValue0', 'More likely', '概率提高', [20, 40, 60])],
        line: '+{0}%',
        desc: ['Wish events are {0}% more likely.', '许愿事件触发的概率提高{0}%'] });
      fx({ cat: 'events', script: 'Fortune_Favors_The_Bold', en: 'More random events', zh: '随机事件更多',
        what: ['Random events show up more often.', '随机事件更容易触发。'],
        params: [pct('FloatValue0', 'More likely', '概率提高', [30, 60, 90])],
        line: '+{0}%',
        desc: ['Random events are {0}% more likely.', '随机事件触发概率+{0}%'] });
      fx({ cat: 'events', script: 'Sustained_Action', en: 'Longer missions', zh: '任务时间延长',
        what: ['Mission events last longer.', '任务类事件持续更久。'],
        params: [pct('FloatValue0', 'Longer by', '延长', [50, 100, 150], { step: 25 })],
        line: '+{0}%',
        desc: ['Mission events last {0}% longer.', '任务类事件的持续时间增加 {0}%'] });
      fx({ cat: 'events', script: 'Efficient_Action', en: 'More from events', zh: '事件奖励增加',
        what: ['Finishing an event gives bigger stat rewards.', '完成事件获得的属性奖励更多。'],
        params: [pct('FloatValue0', 'More reward', '奖励增加', [50, 100, 150], { step: 25 })],
        line: '+{0}%',
        desc: ['Finishing an event gives {0}% more stat rewards.', '完成事件获得的属性奖励增加 {0}%'] });
      fx({ cat: 'events', script: 'Excess_Return', en: 'Stats from fulfilled wishes', zh: '愿望超额回报',
        what: ['Fulfilling a wish gives extra Satiety, Morale, Stamina, Health and Life.', '成功完成愿望后额外获得饱腹、心态、精力、健康和生命。'],
        params: [
          num('FloatValue0', 'Satiety', '饱腹', ['', '点'], [10, 20, 30]),
          num('FloatValue1', 'Morale', '心态', ['', '点'], [10, 20, 30]),
          num('FloatValue2', 'Stamina', '精力', ['', '点'], [10, 20, 30]),
          num('FloatValue3', 'Health', '健康', ['', '点'], [10, 20, 30]),
          num('FloatValue4', 'Life', '生命', ['', '点'], [10, 20, 30])
        ],
        line: '+{0}',
        desc: ['After you fulfill a wish: Satiety +{0}, Morale +{1}, Stamina +{2}, Health +{3}, Life +{4}.', '成功完成愿望后：饱腹+{0}、心态+{1}、精力+{2}、健康+{3}、生命+{4}'] });
      fx({ cat: 'events', script: 'Buff_306210_WishBoost', en: 'A wish every day for 3 days', zh: '三天每天许愿',
        what: ['For 3 days, a wish event shows up every day. No numbers.', '持续3天，每天必定触发一个许愿事件。没有数值。'],
        params: [],
        line: ['3 days', '3天'],
        desc: ['For 3 days, a wish event shows up every day.', '持续3天，每天必定触发一个许愿事件'] });
      fx({ cat: 'events', script: 'Buff_306310_AntiAnorexia', en: 'No Appetite Loss for 3 days', zh: '三天免疫厌食',
        what: ['For 3 days you can\'t get Appetite Loss, but no wishes show up. No numbers.', '持续3天免疫「厌食」，但不会出现许愿。没有数值。'],
        params: [],
        line: ['3 days', '3天'],
        desc: ['For 3 days you can\'t get Appetite Loss, but no wishes show up.', '持续3天，免疫「厌食」效果，但不会出现许愿'] });
      fx({ id: 'game:Buff_Gratitude-wish', cat: 'events', script: 'Buff_Gratitude', en: 'Wish status lasts longer', zh: '愿望状态延长',
        what: ['The status you get after fulfilling a wish lasts longer.', '满足愿望后获得的状态持续更久。'],
        params: [hide('IntValue0', 1009), hide('IntValue1', 1017), hours('FloatValue0', 'Longer by', '延长', [2, 4, 6, 9, 12])],
        line: '+{0}h',
        desc: ['The status you get after fulfilling a wish lasts {0|hour} longer.', '满足愿望后获得的状态持续时间增加{0}小时'] });
      fx({ id: 'game:Buff_Gratitude-appetite', cat: 'food', script: 'Buff_Gratitude', en: 'Shorter Appetite Loss', zh: '厌食时间缩短',
        what: ['Appetite Loss wears off sooner.', '「厌食」状态时间缩短。'],
        params: [hide('IntValue0', 200), hours('FloatValue0', 'Shorter by', '缩短', [1, 2, 3, 4, 5], { scale: -1 / 3600 })],
        line: '-{0}h',
        desc: ['Appetite Loss is {0|hour} shorter.', '厌食状态时间缩短{0}小时'] });
      fx({ cat: 'events', script: 'ExtendSatisfaction', en: 'Contentment lasts longer', zh: '心满意足延长',
        what: ['The Contentment status lasts longer.', '「心满意足」状态的持续时间延长。'],
        params: [hide('IntValue0', 1009), pct('FloatValue0', 'Longer by', '延长', [50, 100, 150], { step: 25 })],
        line: '+{0}%',
        desc: ['Contentment lasts {0}% longer.', '「心满意足」状态的持续时间延长{0}%'] });
      fx({ id: 'game:ExtendSatisfaction-fail', cat: 'events', script: 'ExtendSatisfaction', en: 'Shorter penalty for a failed wish', zh: '愿望失败惩罚缩短',
        what: ['The bad status from a failed wish ends sooner.', '愿望失败时的惩罚状态时间缩短。'],
        params: [hide('IntValue0', 1010), neg('FloatValue0', 'Shorter by', '缩短', [50, 75, 100])],
        line: '-{0}%',
        desc: ['The penalty for a failed wish is {0}% shorter.', '愿望失败时的惩罚时间减少{0}%'] });

      // Body and needs.
      fx({ cat: 'body', script: 'Accelerate_Recovery', en: 'Shorter illnesses', zh: '疾病时间缩短',
        what: ['Illnesses wear off sooner.', '各类疾病状态的持续时间缩短。'],
        params: [hide('IntValue0', 1), neg('FloatValue0', 'Shorter by', '缩短', [25, 50, 75])],
        line: '-{0}%',
        desc: ['Illnesses are {0}% shorter.', '各类疾病状态的持续时间缩短{0}%'] });
      fx({ cat: 'body', script: 'Anti_Corrosion_Training', en: 'Safer expired food', zh: '变质食物更安全',
        what: ['Expired food is less likely to make you sick.', '食用变质食物后罹患疾病的概率降低。'],
        params: [neg('FloatValue0', 'Less likely by', '概率降低', [25, 50, 75])],
        line: '-{0}%',
        desc: ['Expired food is {0}% less likely to make you sick.', '食用变质食物后罹患疾病的概率降低{0}%'] });
      fx({ cat: 'body', script: 'BlackoutAdaptation', en: 'Less Stamina used in outages', zh: '停电时精力消耗降低',
        what: ['You use less Stamina while the power is out.', '停电期间精力消耗降低。'],
        params: [hide('IntValue0', 30002), neg('FloatValue0', 'Less Stamina used', '精力消耗降低', [15])],
        line: '-{0}%',
        desc: ['Use {0}% less Stamina during power outages.', '停电期间，精力消耗降低{0}%'] });
      fx({ cat: 'body', script: 'Buff_300210_EnergyMetabolism', en: 'Slower hunger', zh: '饱腹下降减慢',
        what: ['Satiety drops more slowly.', '饱腹度的下降速度减慢。'],
        params: [neg('FloatValue0', 'Slower by', '减慢', [10, 20, 30])],
        line: '-{0}%',
        desc: ['Satiety drops {0}% slower.', '饱腹度的下降速度减慢{0}%'] });

      const CRISIS_CALM = [{ v: 305311, en: 'Calm in Crisis: 50% less Morale used', zh: '临危不乱：心态消耗-50%' }, { v: 305611, en: 'Steady Focus: 30% less Stamina used', zh: '沉着冷静：精力消耗-30%' }];

      fx({ cat: 'body', script: 'Buff_305310', en: 'A boost when a crisis starts', zh: '危机开始时获得增益',
        what: ['When a crisis starts, you get a status for 1 day. Pick which one.', '进入危机阶段时，获得持续1天的增益状态。可选择哪一种。'],
        params: [pick('IntValue0', 'Status', '状态', () => CRISIS_CALM, 305311, { unknown: (v, zh) => (zh ? '状态 ' : 'Status ') + v })],
        line: '{0}',
        desc: ['When a crisis starts, get 1 day of {0}.', '进入危机阶段时，获得持续1天的增益：{0}'] });
      fx({ cat: 'body', script: 'Buff_306410_DiseaseImmunity', en: 'No diseases for 3 days', zh: '三天免疫疾病',
        what: ['For 3 days you can\'t get sick, but Life doesn\'t recover. The numbers stay as the game set them.', '持续3天免疫所有疾病，但生命停止恢复。数值沿用游戏设定。'],
        params: [],
        line: ['3 days', '3天'],
        desc: ['Immune to all diseases for 3 days, but Life doesn\'t recover.', '持续3天，免疫所有疾病效果，但生命停止恢复'] });
      fx({ cat: 'body', script: 'Buff_306510', en: 'More Stamina from rest', zh: '休息精力增加',
        what: ['For 3 days, resting gives more Stamina. It can also make Satiety drop faster.', '持续3天，休息获得的精力增加。也可以让饱腹下降加快。'],
        params: [pct('FloatValue1', 'More Stamina from rest', '休息精力增加', [100], { step: 25 }), pct('FloatValue2', 'Satiety drops faster', '饱腹下降加快', [30])],
        line: '+{0}%',
        desc: (v, zh) => zh
          ? '持续3天，休息获得的精力+' + fmt(v.FloatValue1) + '%' + (v.FloatValue2 ? '，但饱腹下降速度增加' + fmt(v.FloatValue2) + '%' : '')
          : 'For 3 days, resting gives ' + fmt(v.FloatValue1) + '% more Stamina' + (v.FloatValue2 ? ', but Satiety drops ' + fmt(v.FloatValue2) + '% faster.' : '.') });
      fx({ cat: 'body', script: 'ColdAlertness', en: 'Less Stamina used when chilly', zh: '微寒时精力消耗降低',
        what: ['During a cold wave, you use less Stamina while your room is slightly cold.', '寒潮中身处微寒环境时，精力消耗降低。'],
        params: [neg('FloatValue0', 'Less Stamina used', '精力消耗降低', [15])],
        line: '-{0}%',
        desc: ['During a cold wave, use {0}% less Stamina in a slightly cold room.', '寒潮中身处微寒环境时，精力消耗降低{0}%'] });
      fx({ cat: 'body', script: 'DebuffFreeVigor', en: 'More Stamina with no bad statuses', zh: '无负面状态时精力增加',
        what: ['You gain more Stamina while you have no bad statuses.', '身上没有任何负面状态时，精力获得提高。'],
        params: [pct('FloatValue0', 'More Stamina', '精力获得提高', [15])],
        line: '+{0}%',
        desc: ['Gain {0}% more Stamina while you have no bad statuses.', '身上没有任何负面状态时，精力获得提高{0}%'] });
      fx({ cat: 'body', script: 'Fight_Fire_With_Fire', en: 'Trade Life to cure diseases', zh: '以生命清除疾病',
        what: ['Lose some Life right away and get rid of every disease.', '立即减少生命值，清除身上所有疾病状态。'],
        params: [hide('IntValue0', 1), num('FloatValue0', 'Life lost', '失去生命', ['', '点'], [10], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Life to get rid of every disease right away.', '减少{0}点生命值，立即清除身上所有疾病状态'] });
      fx({ cat: 'body', script: 'LightTravel', en: 'Faster with a light bag', zh: '轻装简行',
        what: ['While your bag is light, you walk 15% faster and use less Stamina.', '背包很轻时，移动速度提高15%，精力消耗降低。'],
        params: [hide('IntValue1', 319011), num('IntValue0', 'Bag weighs under', '背包总重低于', ['g', '克'], [1000], { step: 100, stack: 'each' }), neg('FloatValue0', 'Less Stamina used', '精力消耗降低', [10])],
        line: '-{1}%',
        desc: ['When your bag weighs under {0}g, walk 15% faster and use {1}% less Stamina.', '背包总重低于{0}克时，移动速度提高15%，精力消耗降低{1}%'] });
      fx({ cat: 'body', script: 'Living_With_Illness', en: 'Lose less Life while sick', zh: '生病时生命下降减缓',
        what: ['While you are sick, Life and Health drop more slowly. The reward card only names Life.', '生病期间，生命值和健康值的下降速度减缓。奖励卡片只提到生命。'],
        params: [hide('IntValue0', 1), neg('FloatValue0', 'Life drops slower', '生命下降减缓', [25, 50, 75]), neg('FloatValue1', 'Health drops slower', '健康下降减缓', [25, 50, 75])],
        line: '-{0}%',
        desc: ['While sick, Life drops {0}% slower and Health drops {1}% slower.', '生病期间，生命值的下降速度减缓{0}%，健康值的下降速度减缓{1}%'] });
      fx({ cat: 'body', script: 'Process_Adapt', en: 'Starving hurts less', zh: '极度饥饿伤害减少',
        what: ['While Starving, you lose less Life and Health.', '极度饥饿时，生命值和健康值的下降减少。'],
        params: [hide('IntValue0', 1002), neg('FloatValue1', 'Less Life lost', '生命下降减少', [25, 50, 75]), neg('FloatValue0', 'Less Health lost', '健康下降减少', [25, 50, 75])],
        line: '-{0}%',
        desc: ['While Starving, lose {0}% less Life and {1}% less Health.', '极度饥饿时，生命值下降减少{0}%，健康值下降减少{1}%'] });
      fx({ cat: 'body', script: 'NightHibernation', en: 'Slower at night, slower hunger', zh: '夜间蛰伏',
        what: ['You walk slower at night, but Satiety drops more slowly.', '晚上移动速度变慢，但饱腹度下降速度减慢。'],
        params: [neg('FloatValue1', 'Satiety drops slower', '饱腹下降减慢', [10]), neg('FloatValue0', 'Walk slower at night', '夜间移动减慢', [20])],
        line: '-{0}%',
        desc: ['You walk {1}% slower at night, but Satiety drops {0}% slower.', '晚上移动速度减少{1}%，但饱腹度下降速度减少{0}%'] });
      fx({ cat: 'body', script: 'Patience_Training', en: 'Slower hunger while Hungry', zh: '饥饿时饱腹下降减慢',
        what: ['While Hungry, Satiety drops more slowly, but your max Satiety is lower.', '处于饥饿状态时，饱腹度下降速度减慢，但饱腹度上限降低。'],
        params: [hide('IntValue0', 1001), hide('IntValue1', 1002), neg('FloatValue0', 'Slower while Hungry', '饥饿时减慢', [30]), num('BuffSatietyMax', 'Max Satiety lost', '饱腹上限降低', ['', '点'], [10], { scale: -1 })],
        line: '-{0}%',
        desc: ['While Hungry, Satiety drops {0}% slower, but max Satiety is {1} lower.', '处于饥饿状态时，饱腹度下降速度减慢{0}%，但饱腹度上限降低{1}'] });
      fx({ cat: 'body', script: 'PostDisaster_HealthRecovery', en: 'Faster Health recovery', zh: '健康恢复加快',
        what: ['Health recovers on its own faster.', '健康值的自然恢复速度加快。'],
        params: [pct('FloatValue0', 'Faster by', '加快', [30, 60, 90])],
        line: '+{0}%',
        desc: ['Health recovers {0}% faster.', '健康值的自然恢复速度加快{0}%'] });
      fx({ cat: 'body', script: 'PostDisaster_VitalityRecovery', en: 'Faster Life recovery', zh: '生命恢复加快',
        what: ['Life recovers on its own faster.', '生命值的自然恢复速度加快。'],
        params: [pct('FloatValue0', 'Faster by', '加快', [30, 60, 90])],
        line: '+{0}%',
        desc: ['Life recovers {0}% faster.', '生命值的自然恢复速度加快{0}%'] });
      fx({ cat: 'body', script: 'SleepEnergyManagement', en: 'More Stamina from night sleep', zh: '夜间睡眠精力增加',
        what: ['Sleeping at night gives more Stamina, and sleeping in the day gives less.', '夜间睡觉恢复的精力增加，白天睡觉恢复的精力减少。'],
        params: [pct('FloatValue1', 'More at night', '夜间增加', [50]), neg('FloatValue0', 'Less in the day', '白天减少', [50])],
        line: '+{0}%',
        desc: ['Sleeping at night gives {0}% more Stamina, and sleeping in the day gives {1}% less.', '夜间睡觉恢复的精力+{0}%，白天睡觉恢复的精力-{1}%'] });
      fx({ cat: 'body', script: 'StarvingFocus', en: 'Less Stamina used at low Satiety', zh: '低饱腹时精力消耗降低',
        what: ['When Satiety is very low, you use less Stamina.', '饱腹很低时，精力消耗降低。'],
        params: [num('IntValue0', 'Satiety below', '饱腹低于', ['%', '%'], [10], { stack: 'each' }), neg('FloatValue0', 'Less Stamina used', '精力消耗降低', [20])],
        line: '-{1}%',
        desc: ['When Satiety is below {0}%, use {1}% less Stamina.', '饱腹低于{0}%时，精力消耗降低{1}%'] });

      // Morale.
      fx({ cat: 'morale', script: 'Buff_300410_EmotionManagement', en: 'Slower Morale drop', zh: '心态下降减慢',
        what: ['Morale drops more slowly.', '心态的下降速度减慢。'],
        params: [neg('FloatValue0', 'Slower by', '减慢', [10, 20, 30])],
        line: '-{0}%',
        desc: ['Morale drops {0}% slower.', '心态的下降速度减慢{0}%'] });
      fx({ cat: 'morale', script: 'Endurance_Training', en: 'Hunger hurts Morale less', zh: '饥饿影响心态减少',
        what: ['Being Hungry costs less Morale.', '饥饿导致的心态下降减少。'],
        params: [hide('IntValue0', 1001), neg('FloatValue0', 'Less Morale lost', '心态下降减少', [25, 50, 75])],
        line: '-{0}%',
        desc: ['Being Hungry costs {0}% less Morale.', '饥饿导致的心态下降值减少{0}%'] });
      fx({ cat: 'morale', script: 'HearthComfort', en: 'More Morale in a warm house', zh: '暖屋心态增加',
        what: ['During a cold wave, you gain more Morale while your house is heated.', '寒潮中把屋子烧暖时，心态获得提高。'],
        params: [pct('FloatValue0', 'More Morale', '心态获得提高', [20])],
        line: '+{0}%',
        desc: ['During a cold wave, gain {0}% more Morale while your house is heated.', '寒潮中把屋子烧暖时，心态获得提高{0}%'] });
      fx({ cat: 'morale', script: 'AE_StockpileComfort', en: 'Morale from a full pantry', zh: '家底厚实',
        what: ['Each day, if your stored food adds up to 500 Satiety or more, you get extra Morale.', '家中食物储备总饱腹值达500以上时，每日结算额外恢复心态。'],
        params: [num('FloatValue0', 'Extra Morale', '额外心态', ['', '点'], [5])],
        line: '+{0}',
        desc: ['When your stored food adds up to 500 Satiety or more, get {0} extra Morale at the end of each day.', '家中食物储备总饱腹值达500以上时，每日结算额外恢复{0}点心态'] });
      fx({ cat: 'morale', script: 'Multitask', en: 'Morale from steady work', zh: '乐在其中',
        what: ['For 3 days, every full hour spent doing things gives Morale. Stopping only pauses the count.', '持续3天。做任何事都在累计，每满1小时恢复心态；中断只暂停，不清零。'],
        params: [hide('BuffInterval', 3600), num('BuffMoraleInterval', 'Morale each hour', '每小时心态', ['', '点'], [3])],
        line: '+{0}',
        desc: ['For 3 days, get {0} Morale for every full hour spent doing things. Stopping only pauses the count.', '持续3天。做任何事都在累计，每满1小时恢复{0}点心态；中断只暂停，不清零'] });
      fx({ cat: 'morale', script: 'Passive_Abundance', en: 'Abundance from a full home', zh: '家有余粮',
        what: ['While your home has lots of supplies, you get the Abundance status, and Morale and Stamina keep recovering.', '物资储备充足时获得「富足」状态，心态和精力持续回复。'],
        params: [hide('IntValue0', 314220), num('IntValue1', 'Starts at', '达到时获得', ['', '件'], [150], { step: 10, stack: 'each' }), num('IntValue3', 'Ends below', '低于时移除', ['', '件'], [100], { step: 10, stack: 'each' })],
        line: '{0}+',
        desc: ['With {0} or more items at home you get Abundance. It ends when you have fewer than {1}.', '家中物资达到{0}件时获得「富足」，低于{1}件时移除'] });

      // Statuses the game puts on you. Each one's "what" names the status.
      fx({ cat: 'status', script: 'BagOverweight', en: 'Walk slower (Encumbered)', zh: '移动变慢（超重）',
        what: ['The Encumbered status: you walk slower.', '「超重」状态：移动速度降低。'],
        params: [neg('FloatValue0', 'Slower by', '降低', [40])],
        line: '-{0}%',
        desc: ['Walk {0}% slower.', '移动速度降低{0}%'] });
      fx({ cat: 'status', script: 'BloodSeal', en: 'Lose Life when the disaster starts (Sold Blood)', zh: '灾后开局扣生命（卖血印记）',
        what: ['The Sold Blood status: you lose Life right after the disaster starts. The heavier marks also give Blood Donor Weakness.', '「卖血印记」状态：灾后开局立即扣除生命。较重的印记还会附加献血虚弱。'],
        params: [num('BuffVitalityStart', 'Life lost', '扣除生命', ['', '点'], [5, 15, 25], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Life right after the disaster starts.', '灾后开局立即扣除{0}点生命'] });
      fx({ cat: 'status', script: 'BloodWeak', en: 'Slower recovery (Blood Donor Weakness)', zh: '恢复变慢（献血虚弱）',
        what: ['The Blood Donor Weakness status: every stat recovers more slowly.', '「献血虚弱」状态：所有属性恢复速度降低。'],
        params: [neg('FloatValue0', 'Slower by', '降低', [25, 30])],
        line: '-{0}%',
        desc: ['All stats recover {0}% slower.', '所有属性恢复速度降低{0}%'] });
      fx({ cat: 'status', script: 'Buff20011', en: 'Stamina doesn\'t drop (Energy Drink)', zh: '精力不下降（功能饮料）',
        what: ['The Energy Drink rush: Stamina doesn\'t drop for a while. The numbers stay as the game set them.', '「功能饮料-兴奋」状态：一段时间内精力不下降。数值沿用游戏设定。'],
        params: [],
        line: ['No Stamina drop', '精力不降'],
        desc: ['Stamina doesn\'t drop.', '精力不下降'] });
      fx({ cat: 'status', script: 'Buff20012', en: 'Stamina drops twice as fast (Energy Drink side effects)', zh: '精力双倍下降（功能饮料副作用）',
        what: ['The Energy Drink side effects: Stamina drops twice as fast for a while. The numbers stay as the game set them.', '「功能饮料-副作用」状态：一段时间内精力双倍下降。数值沿用游戏设定。'],
        params: [],
        line: ['x2 Stamina drop', '精力双倍下降'],
        desc: ['Stamina drops twice as fast.', '精力双倍下降'] });
      fx({ cat: 'status', script: 'Buff_1300_Satiated', en: 'Work faster, eat more (Well-Fed)', zh: '做事更快（饱足）',
        what: ['The Well-Fed status: you work faster and Morale and Stamina recover faster, but Satiety drops faster.', '「饱足」状态：做事更快，心态和精力恢复更快，但饱腹下降也更快。'],
        params: [pct('FloatValue3', 'Work faster', '做事加快', [10]), pct('FloatValue0', 'Satiety drops faster', '饱腹下降加快', [30]), pct('FloatValue1', 'Morale recovers faster', '心态恢复加快', [20]), pct('FloatValue2', 'Stamina recovers faster', '精力恢复加快', [20])],
        line: '+{0}%',
        desc: ['Work {0}% faster, but Satiety drops {1}% faster. Morale recovers {2}% faster and Stamina {3}% faster.', '做事速度+{0}%，但饱腹下降速度+{1}%；心态恢复+{2}%，精力恢复+{3}%'] });
      fx({ cat: 'status', script: 'Buff_CondOnOtherBuff', en: 'No losses from the outbreak', zh: '疫病不再消耗属性',
        what: ['Fever Reducing and Anti-Infection give you this status. While you have it, the disease outbreak costs you no stats. The numbers stay as the game set them.', '「退烧中」和「抗感染」会给你这个状态。持续期间，疫病不会让你损失属性。数值沿用游戏设定。'],
        params: [],
        line: ['No outbreak losses', '疫病不掉属性'],
        desc: ['The disease outbreak costs you no stats.', '疫病不会让你损失属性'] });
      fx({ cat: 'status', script: 'Child_Dismay', en: 'Gain less (Depressed)', zh: '获得减少（沮丧）',
        what: ['The Depressed status: you gain less Satiety and Stamina.', '「沮丧」状态：饱腹和精力的获得减少。'],
        params: [neg('FloatValue0', 'Less Satiety gained', '饱腹获得减少', [10]), neg('FloatValue1', 'Less Stamina gained', '精力获得减少', [10])],
        line: '-{0}%',
        desc: ['Gain {0}% less Satiety and {1}% less Stamina.', '饱腹获得减少{0}%，精力获得减少{1}%'] });
      fx({ cat: 'status', script: 'Child_Dying', en: 'Gain less, walk slower (Critical)', zh: '获得减少，移动变慢（垂危）',
        what: ['The Critical status: lower max Stamina, less Satiety, Morale and Stamina gained, and slower walking.', '「垂危」状态：精力上限降低，饱腹、心态、精力获得减少，移动变慢。'],
        params: [num('IntValue0', 'Max Stamina lost', '精力上限降低', ['', '点'], [10], { scale: -1 }), neg('FloatValue0', 'Less Satiety gained', '饱腹获得减少', [10]),
          neg('FloatValue1', 'Less Morale gained', '心态获得减少', [10]), neg('FloatValue2', 'Less Stamina gained', '精力获得减少', [10]), neg('BuffSpeed', 'Walk slower', '移动减慢', [30])],
        line: '-{1}%',
        desc: ['Max Stamina {0} lower. Gain {1}% less Satiety, {2}% less Morale and {3}% less Stamina. Walk {4}% slower.', '精力上限-{0}；饱腹获得-{1}%，心态获得-{2}%，精力获得-{3}%；移动速度-{4}%'] });
      fx({ cat: 'status', script: 'Child_ExtremeFatigue', en: 'Lose Life, walk slower (Exhausted)', zh: '失去生命，移动变慢（极度疲劳）',
        what: ['The Exhausted status: less Satiety, Morale and Life gained, Life lost every hour, and slower walking.', '「极度疲劳」状态：饱腹、心态、生命获得减少，每小时失去生命，移动变慢。'],
        params: [hide('IntValue0', 3600), neg('FloatValue0', 'Less Satiety gained', '饱腹获得减少', [10]), neg('FloatValue1', 'Less Morale gained', '心态获得减少', [10]),
          neg('FloatValue3', 'Less Life gained', '生命获得减少', [10]), num('FloatValue4', 'Life lost each hour', '每小时失去生命', ['', '点'], [2], { scale: -1 }), neg('BuffSpeed', 'Walk slower', '移动减慢', [30])],
        line: '-{3}',
        desc: ['Gain {0}% less Satiety, {1}% less Morale and {2}% less Life. Lose {3} Life every hour. Walk {4}% slower.', '饱腹获得-{0}%，心态获得-{1}%，生命获得-{2}%；每小时生命-{3}；移动速度-{4}%'] });
      fx({ cat: 'status', script: 'Child_ExtremeHunger', en: 'Lose Life, walk slower (Starving)', zh: '失去生命，移动变慢（极度饥饿）',
        what: ['The Starving status: you lose Life every 2 hours and walk slower.', '「极度饥饿」状态：每2小时失去生命，移动变慢。'],
        params: [hide('IntValue0', 7200), num('FloatValue2', 'Life lost every 2 hours', '每2小时失去生命', ['', '点'], [6], { scale: -1 }), neg('BuffSpeed', 'Walk slower', '移动减慢', [30])],
        line: '-{0}',
        desc: ['Lose {0} Life every 2 hours. Walk {1}% slower.', '每2小时生命-{0}；移动速度-{1}%'] });
      fx({ cat: 'status', script: 'Child_Fatigue', en: 'Gain less (Fatigued)', zh: '获得减少（疲劳）',
        what: ['The Fatigued status: you gain less Satiety, Morale and Life.', '「疲劳」状态：饱腹、心态、生命的获得减少。'],
        params: [neg('FloatValue0', 'Less Satiety gained', '饱腹获得减少', [10]), neg('FloatValue1', 'Less Morale gained', '心态获得减少', [10]), neg('FloatValue3', 'Less Life gained', '生命获得减少', [10])],
        line: '-{0}%',
        desc: ['Gain {0}% less Satiety, {1}% less Morale and {2}% less Life.', '饱腹获得-{0}%，心态获得-{1}%，生命获得-{2}%'] });
      fx({ cat: 'status', script: 'Child_Obesity', en: 'Walk slower, get hungry faster (Overweight)', zh: '移动变慢，更易饿（肥胖）',
        what: ['The Overweight status: you walk slower and Satiety drops faster, but eating also gives Morale.', '「肥胖」状态：移动变慢，饱腹下降加快，但进食时额外获得心态。'],
        params: [neg('FloatValue0', 'Walk slower', '移动减慢', [50]), pct('FloatValue1', 'Morale from eating', '进食心态', [20]), pct('FloatValue2', 'Satiety drops faster', '饱腹下降加快', [50])],
        line: '-{0}%',
        desc: ['Walk {0}% slower. Eating gives Morale equal to {1}% of the Satiety it gives. Satiety drops {2}% faster.', '移动速度-{0}%；进食时额外获得相当于饱腹{1}%的心态；饱腹下降速度+{2}%'] });
      fx({ cat: 'status', script: 'Child_Subhealth', en: 'Lower max Life and Stamina (Weakened)', zh: '上限降低（亚健康）',
        what: ['The Weakened status: max Life and max Stamina are lower.', '「亚健康」状态：生命上限和精力上限降低。'],
        params: [num('IntValue0', 'Max Life lost', '生命上限降低', ['', '点'], [10], { scale: -1 }), num('IntValue1', 'Max Stamina lost', '精力上限降低', ['', '点'], [10], { scale: -1 })],
        line: '-{0}',
        desc: ['Max Life {0} lower and max Stamina {1} lower.', '生命上限-{0}，精力上限-{1}'] });
      fx({ cat: 'status', script: 'Energetic', en: 'More Morale gained', zh: '心态获得增加',
        what: ['You gain more Morale.', '心态获得增加。'],
        params: [pct('FloatValue0', 'More Morale', '心态获得增加', [50])],
        line: '+{0}%',
        desc: ['Gain {0}% more Morale.', '心态获得+{0}%'] });
      fx({ cat: 'status', script: 'Excited', en: 'Higher Morale target (Excited)', zh: '心态目标提高（兴奋）',
        what: ['The Excited status. It adds this number to a Morale target the game keeps. What that target changes is not known yet.', '「兴奋」状态。为游戏中的心态目标值增加该数值，具体影响尚不清楚。'],
        params: [num('IntValue0', 'Morale target', '心态目标', ['', '点'], [30])],
        line: '+{0}',
        desc: ['Morale target +{0}.', '心态目标+{0}'] });
      fx({ cat: 'status', script: 'ExpiredFood', en: 'A food poisoning symptom', zh: '变质食物症状',
        what: ['The Food Poisoning status: you get one symptom at random, such as Diarrhea, Stomachache, Vomiting, Fever or Poisoned. The numbers stay as the game set them.', '「过期食品影响」状态：随机获得一种症状，如腹泻、胃痛、呕吐、发热或中毒。数值沿用游戏设定。'],
        params: [],
        line: ['1 symptom', '1种症状'],
        desc: ['Get one food poisoning symptom at random.', '随机获得一种变质食物症状'] });
      fx({ cat: 'status', script: 'FullStomach', en: 'Less Satiety from some food', zh: '部分食物饱腹减少',
        what: ['One kind of food gives less Satiety. Which kind is not known yet.', '某一类食物提供的饱腹减少，具体是哪一类尚不清楚。'],
        params: [neg('FloatValue0', 'Less Satiety', '饱腹减少', [50])],
        line: '-{0}%',
        desc: ['That food gives {0}% less Satiety.', '该类食物的饱腹获得减少{0}%'] });
      fx({ cat: 'status', script: 'FurnitureDurabilityMax', en: 'Tougher furniture (Durable)', zh: '家具更耐用（耐久强化）',
        what: ['The Durable status: furniture has more max durability.', '「耐久强化」状态：家具耐久上限提高。'],
        params: [num('IntValue0', 'Extra durability', '耐久上限提高', ['', ''], [50, 100, 200], { step: 10 })],
        line: '+{0}',
        desc: ['Furniture max durability +{0}.', '家具耐久上限+{0}'] });
      fx({ cat: 'status', script: 'IvyShield', en: 'Vine shield (Vine Transplant)', zh: '藤蔓护盾（藤蔓移植）',
        what: ['The Vine Transplant status: thorny vines form a shield, and zombies that bite it take damage. Its other two numbers stay as the game set them.', '「藤蔓移植」状态：带刺的藤蔓形成护盾，啃咬它的丧尸会受到反伤。另外两个数值沿用游戏设定。'],
        params: [num('IntValue0', 'Shield', '护盾', ['', '点'], [300], { step: 50 })],
        line: '{0}',
        desc: ['A {0} point vine shield.', '{0}点藤蔓护盾'] });
      fx({ cat: 'status', script: 'Life_Movement', en: 'Max Life and more gains (Athlete)', zh: '生命上限与获得提高（生命在于运动）',
        what: ['The Athlete status: higher max Life, and you gain more Health and Stamina.', '「生命在于运动」状态：生命上限提高，健康和精力获得增加。'],
        params: [num('IntValue0', 'Max Life', '生命上限', ['', '点'], [10]), pct('FloatValue0', 'More Health', '健康获得增加', [100], { step: 25 }), pct('FloatValue1', 'More Stamina', '精力获得增加', [50], { step: 25 })],
        line: '+{0}',
        desc: ['Max Life +{0}. Gain {1}% more Health and {2}% more Stamina.', '生命上限+{0}；健康获得+{1}%，精力获得+{2}%'] });
      fx({ cat: 'status', script: 'PoorAppetite', en: 'Appetite Loss', zh: '厌食',
        what: ['The Appetite Loss status, as the game sets it. No numbers.', '「厌食」状态，效果按游戏设定。没有数值。'],
        params: [],
        line: ['Appetite Loss', '厌食'],
        desc: ['You get Appetite Loss.', '获得「厌食」状态'] });
      fx({ cat: 'status', script: 'Sweets', en: 'Health lost when you sleep (Sweets)', zh: '睡觉时失去健康（甜食）',
        what: ['The Sweets status: if you sleep while you have it, you lose Health.', '「甜食」状态：带着它睡觉会失去健康。'],
        params: [num('FloatValue0', 'Health lost', '失去健康', ['', '点'], [3], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Health when you sleep.', '睡觉时失去{0}点健康'] });
      fx({ cat: 'status', script: 'RiotTrampling', en: 'Lose Morale and Stamina at once', zh: '立即失去心态和精力',
        what: ['You lose Morale and Stamina right away. The game has no text for it.', '立即失去心态和精力。游戏中没有它的说明。'],
        params: [num('BuffMoraleStart', 'Morale lost', '失去心态', ['', '点'], [10], { scale: -1 }), num('BuffStaminaStart', 'Stamina lost', '失去精力', ['', '点'], [20], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Morale and {1} Stamina right away.', '立即失去{0}点心态和{1}点精力'] });
      fx({ cat: 'money', script: 'PostDisaster_AddSurvivalPointsByLayer', en: 'Survival Points each day', zh: '每日额外生存点',
        what: ['You get extra Survival Points at the end of each day. The Wish Reward statuses give it.', '每日结算时额外获得生存点。「愿望奖励」状态使用它。'],
        params: [num('IntValue0', 'Extra points', '额外生存点', ['', '点'], [1, 2, 3])],
        line: '+{0}',
        desc: ['Get {0|extra Survival Point} at the end of each day.', '每日结算时额外获得{0}点生存点'] });
      fx({ cat: 'status', script: 'SymptomForcedAction', en: 'Lose Satiety and Stamina (Vomiting)', zh: '失去饱腹和精力（呕吐）',
        what: ['The Vomiting status: you lose Satiety and Stamina every 3 hours and sometimes stop to throw up.', '「呕吐」状态：每3小时失去饱腹和精力，并会被打断去呕吐。'],
        params: [hide('BuffInterval', 10800), num('BuffSatietyInterval', 'Satiety lost', '失去饱腹', ['', '点'], [3], { scale: -1 }), num('BuffStaminaInterval', 'Stamina lost', '失去精力', ['', '点'], [3], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Satiety and {1} Stamina every 3 hours.', '每3小时饱腹-{0}，精力-{1}'] });
      fx({ cat: 'status', script: 'SymptomForcedToiletAction', en: 'Lose Satiety (Diarrhea)', zh: '失去饱腹（腹泻）',
        what: ['The Diarrhea status: you lose Satiety every 3 hours and sometimes have to run to the toilet.', '「腹泻」状态：每3小时失去饱腹，并会被打断去上厕所。'],
        params: [hide('BuffInterval', 10800), num('BuffSatietyInterval', 'Satiety lost', '失去饱腹', ['', '点'], [3], { scale: -1 })],
        line: '-{0}',
        desc: ['Lose {0} Satiety every 3 hours.', '每3小时饱腹-{0}'] });

      // The game's watchers for low stats: each adds a status when the stat falls to one level and takes it off once
      // the stat is back up to another.
      [['Passive_Hunger', 1001, 'Hungry', '饥饿', 'Satiety', 30], ['Passive_Dismay', 1003, 'Depressed', '沮丧', 'Morale', 30], ['Passive_Fatigue', 1004, 'Fatigued', '疲劳', 'Stamina', 10],
        ['Passive_Subhealth', 1006, 'Weakened', '亚健康', 'Health', 30], ['Passive_Dying', 1007, 'Critical', '垂危', 'Vitality', 15]].forEach(w => {
        const stat = STATS.find(s => s.v === w[4]);

        fx({ cat: 'status', script: w[0], en: w[2] + ' at low ' + stat.en, zh: stat.zh + '低时' + w[3],
          what: ['Adds the ' + w[2] + ' status when ' + stat.en + ' falls low, and takes it off once ' + stat.en + ' is back up.', stat.zh + '降低时添加「' + w[3] + '」状态，回升后移除。'],
          params: [hide('IntValue0', w[1]), num('IntValue1', 'Starts at or below', '低于等于时添加', ['', ''], [w[5]], { stack: 'each' }), num('IntValue2', 'Ends at or above', '高于等于时移除', ['', ''], [w[5]], { stack: 'each' })],
          line: ['{0} or less', '{0}及以下'],
          desc: [w[2] + ' starts when ' + stat.en + ' is {0} or lower and ends when it is back to {1} or higher.', stat.zh + '低于等于{0}时出现「' + w[3] + '」，回到{1}及以上时移除'] });
      });
      [['Passive_ExtremeHunger', 1002, 'Starving', '极度饥饿', 'Satiety'], ['Passive_ExtremeFatigue', 1005, 'Exhausted', '极度疲劳', 'Stamina']].forEach(w => {
        const stat = STATS.find(s => s.v === w[4]);

        fx({ cat: 'status', script: w[0], en: w[2] + ' at 0 ' + stat.en, zh: stat.zh + '为0时' + w[3],
          what: ['Adds the ' + w[2] + ' status when ' + stat.en + ' hits 0, and takes it off once it is above 0. The numbers stay as the game set them.', stat.zh + '降到0时添加「' + w[3] + '」状态，高于0后移除。数值沿用游戏设定。'],
          params: [hide('IntValue0', w[1])],
          line: ['at 0', '为0时'],
          desc: [w[2] + ' starts when ' + stat.en + ' hits 0 and ends once it is above 0.', stat.zh + '降到0时出现「' + w[3] + '」，高于0后移除'] });
      });
      fx({ cat: 'status', script: 'Passive_Obesity', en: 'Overweight at high Satiety', zh: '饱腹高时肥胖',
        what: ['Adds the Overweight status while Satiety stays high, and takes it off once Satiety stays lower.', '饱腹持续偏高时添加「肥胖」状态，持续偏低后移除。'],
        params: [hide('IntValue0', 1008), num('IntValue1', 'Starts at or above', '高于等于时添加', ['', ''], [70], { stack: 'each' }), num('IntValue3', 'Ends below', '低于时移除', ['', ''], [70], { stack: 'each' })],
        line: ['{0} or more', '{0}及以上'],
        desc: ['Overweight starts when Satiety stays at {0} or higher and ends when it stays below {1}.', '饱腹持续高于等于{0}时出现「肥胖」，持续低于{1}时移除'] });
      fx({ cat: 'status', script: 'Passive_Satiated', en: 'Well-Fed at high Satiety', zh: '饱腹高时饱足',
        what: ['Adds the Well-Fed status when Satiety is high, and takes it off once Satiety drops.', '饱腹偏高时添加「饱足」状态，饱腹下降后移除。'],
        params: [hide('IntValue0', 1300), num('IntValue1', 'Starts at or above', '高于等于时添加', ['', ''], [80], { stack: 'each' }), num('IntValue3', 'Ends below', '低于时移除', ['', ''], [60], { stack: 'each' })],
        line: ['{0} or more', '{0}及以上'],
        desc: ['Well-Fed starts when Satiety is {0} or higher and ends when it is below {1}.', '饱腹高于等于{0}时出现「饱足」，低于{1}时移除'] });
      fx({ cat: 'status', script: 'Passive_ColdFever', en: 'Chance of High Temperature in the cold', zh: '受寒发烧概率',
        what: ['Staying out in the cold during a cold wave can give you High Temperature.', '寒潮中持续受冻时，可能患上「发烧」。'],
        params: [hide('IntValue0', 1110), pct('FloatValue0', 'Chance', '概率', [50])],
        line: '{0}%',
        desc: ['{0}% chance to get High Temperature from the cold.', '受冻时有{0}%的概率患上「发烧」'] });
      fx({ cat: 'status', script: 'Passive_ColdSickness', en: 'Chance of Wind Chill in the cold', zh: '受寒风寒概率',
        what: ['Going without heat during a cold wave can give you Wind Chill.', '寒潮中持续没有取暖时，可能患上「风寒」。'],
        params: [hide('IntValue0', 1116), pct('FloatValue0', 'Chance', '概率', [60])],
        line: '{0}%',
        desc: ['{0}% chance to get Wind Chill from the cold.', '受冻时有{0}%的概率患上「风寒」'] });
      fx({ cat: 'status', script: 'Passive_Warmth', en: 'Cold wave statuses from room heat', zh: '寒潮时按室温切换状态',
        what: ['During a cold wave, sets your cold status from how warm your room is: Warm, Comfortable, Slight Chill or Freezing. Heating takes off Freezing and Wind Chill. The numbers stay as the game set them.', '寒潮期间，根据屋内温度切换状态：温暖、舒适、微寒或寒冷。取暖会移除寒冷和风寒。数值沿用游戏设定。'],
        params: [],
        line: ['Cold wave', '寒潮'],
        desc: ['During a cold wave, your status follows how warm your room is.', '寒潮期间，状态随屋内温度变化'] });
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

      // A level past the effect's own defaults keeps climbing by the last step. Where levels add up, every level starts
      // at the first value, since the levels below it already count; only a number the highest level sets climbs.
      function defaultAt(p, i) {
        const def = T.levelsAddUp && p.stack !== 'max' ? p.def.slice(0, 1) : p.def;
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

      // The boxes of an effect with no name are numbered from 1 in the order they show.
      const rawLabel = i => ({ en: 'Number ' + (i + 1), zh: '数值 ' + (i + 1) });

      const RAW_NOTE = ['This effect has no name yet. Its boxes hold the numbers as the game stores them, and the game\'s card text says what they do.', '此效果尚未命名。数值框是游戏存储的原始数值，游戏卡片上的说明会告诉你它们的作用。'];

      const unitText = (p, zh) => p.unit ? p.unit[zh ? 1 : 0] : '';

      // A reward level's card text and the reward's line of values per level, both languages, for the percent check.
      const cardText = (t, l) => [normText(l.desc), normText(t.levelText)].map(x => x ? x.en + ' ' + x.zh : '').join(' ');

      // A fraction is shown as a percent when every card that sets it writes it as one (0.3 where the card says 30%).
      function readsAsPercent(k, uses) {
        const set = uses.filter(u => isNum(u.l.values[k]) && u.l.values[k] !== 0);

        if (INT_FIELD.test(k) || !set.length) return false;

        // The number has to stand alone, so 5% never matches inside "15%".
        return set.every(u => new RegExp('(^|[^0-9.])' + fmt(Math.abs(u.l.values[k] * 100)).replace(/\./g, '\\.') + '%').test(u.text));
      }

      // A game script with no friendly name: its numbers are the raw fields the game's rewards set, and its "what" is
      // the first of those rewards' card text.
      function rawFx(script) {
        if (rawCache[script]) return rawCache[script];

        const users = T.st.game.filter(t => t.levels.some(l => l.script === script));
        const uses = [].concat.apply([], users.map(t => t.levels.filter(l => l.script === script).map(l => ({ l, text: cardText(t, l) }))));
        const seen = new Set();

        uses.forEach(u => Object.keys(u.l.values).forEach(k => { if (isNum(u.l.values[k])) seen.add(k); }));

        const keys = Array.from(seen).sort((a, b) => FIELD_RANK(a) - FIELD_RANK(b) || (a < b ? -1 : a > b ? 1 : 0)).slice(0, 4);
        const firstLevels = users.length ? users[0].levels.filter(l => l.script === script) : [];
        const params = keys.map((k, i) => {
          const all = uses.map(u => u.l.values[k]).filter(isNum);
          const label = rawLabel(i);
          const def = firstLevels.length ? firstLevels.map(l => isNum(l.values[k]) ? l.values[k] : 0) : [0];

          if (readsAsPercent(k, uses)) return pct(k, label.en, label.zh, def.map(v => round2(v * 100)), { min: -Infinity });

          return num(k, label.en, label.zh, ['', ''], def, { step: all.every(v => Math.floor(v) === v) ? 1 : 0.05, min: -Infinity });
        });
        const card = uses.length ? normText(uses[0].l.desc) : null;
        const name = prettyScript(script);
        const made = {
          id: 'raw:' + script, src: 'game', cat: 'raw', script, scripts: [script], raw: true, pickable: true, modes: 'both',
          usedBy: [], finishes: [], en: name, zh: name,
          what: card ? [card.en || card.zh, card.zh || card.en] : ['A game effect with no name yet. No reward in the game uses it.', '尚未命名的游戏效果，游戏中没有奖励使用它。'],
          params,
          line: (v, zh) => params.map(p => fmt(v[p.key]) + unitText(p, zh)).join(', '),
          desc: (v, zh) => params.map(p => (zh ? p.zh + '：' : p.en + ': ') + fmt(v[p.key]) + unitText(p, zh)).join(zh ? '，' : ', ')
        };

        rawCache[script] = made;

        return made;
      }

      // Up to three game rewards that run this effect, as their cards word it: the text at the first level that runs
      // it, then the reward's values per level, or the last level's text when the reward has no such line.
      function gameCards(f) {
        const ids = Array.from(new Set(f.finishes.concat(f.usedBy))).filter(id => T.st.byId[id]).slice(0, 3);

        return ids.map(id => {
          const t = T.st.byId[id];
          const runs = t.levels.filter(l => f.scripts.indexOf(l.script) >= 0);
          const firstLevel = runs[0] || t.levels[0] || {};
          const lastLevel = runs.length > 1 ? runs[runs.length - 1] : null;
          const desc = normText(firstLevel.desc);
          const last = lastLevel ? normText(lastLevel.desc) : null;

          return {
            name: normText(t.name),
            desc,
            byLevel: normText(t.levelText),
            last: last && desc && (last.en !== desc.en || last.zh !== desc.zh) ? last : null,
            lastAt: lastLevel ? t.levels.indexOf(lastLevel) + 1 : 0
          };
        }).filter(c => c.desc);
      }

      function gameCardsHtml(f) {
        const lang = T.st.lang;
        const cards = gameCards(f);
        const extra = c => {
          if (c.byLevel) return L('By level: ', '各级：') + textIn(c.byLevel, lang);

          if (c.last) return (lang === 'zh' ? '第' + c.lastAt + '级：' : 'Level ' + c.lastAt + ': ') + textIn(c.last, lang);

          return '';
        };

        if (!cards.length) return '';

        return '<h4>' + L('WHAT THE GAME SAYS', '游戏内说明') + '</h4>' + cards.map(c => '<div class="fxd-gc">' +
          '<div class="n">' + esc(textIn(c.name, lang)) + '</div><div class="d">' + esc(textIn(c.desc, lang)) + '</div>' +
          (extra(c) ? '<div class="m">' + esc(extra(c)) + '</div>' : '') + '</div>').join('');
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

      // Where levels add up, a level's card also says what the player has in total, after each effect's own sentence.
      function autoDesc(e, i, zh) {
        const withTotals = i > 0 && addsUp(e);

        return e.gives.map(g => g.fx.desc(valsOf(g, i), zh, g.fx.params) + (withTotals ? totalAfter(g, i, zh) : '')).join(zh ? '；' : ' ');
      }

      // The card text a tab wrote before cards named their totals, so a tab can still tell it made that text itself.
      const plainDesc = (e, i, zh) => e.gives.map(g => g.fx.desc(valsOf(g, i), zh, g.fx.params)).join(zh ? '；' : ' ');

      function totalAfter(g, i, zh) {
        const total = g.fx.line(totalVals(g, i), zh);

        if (total === autoLine(g, i, zh)) return '';

        return zh ? '（累计' + total + '）' : ' In total: ' + total + '.';
      }

      // A tab sets T.levelsAddUp when each level is a reward of its own that adds to the ones below it (More Rewards).
      // Then a level's total is that level and every level before it, combined the way each number stacks.
      function addsUp(e) {
        if (!T.levelsAddUp || e.givesLocked || !e.gives.length) return false;

        return e.gives.every(g => !g.fx.raw) && e.gives.some(g => numsOf(g.fx).length > 0);
      }

      // The game combines the raw values in the file, so the total is worked out on those and shown like the levels.
      function totalVals(g, i) {
        const v = Object.assign({}, g.pick);

        numsOf(g.fx).forEach(p => {
          const per = g.per[p.key];
          const shownAt = range(i + 1).map(j => per[Math.min(j, per.length - 1)]);
          const raws = shownAt.map(x => (x - p.add) / p.scale);

          if (p.stack === 'each') v[p.key] = shownAt[i];
          else if (p.stack === 'max') v[p.key] = shownAt.reduce((a, b) => Math.max(a, b));
          else if (p.stack === 'times') v[p.key] = round2(display(p, raws.reduce((a, b) => a * b, 1)));
          else v[p.key] = round2(display(p, raws.reduce((a, b) => a + b, 0)));
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
          '<table class="lt glv">' + head + rows + '</table>' + (sums ? totalNoteHtml(e) : '');
      }

      // The note under a levels table whose levels add up, with a line for each number on the reward that combines
      // another way.
      function totalNoteHtml(e) {
        const stacks = e ? e.gives.reduce((all, g) => all.concat(numsOf(g.fx).map(p => p.stack)), []) : [];
        const lines = [L('Each level adds to the ones below it. IN TOTAL is what the player has from that level and every level before it.', '每一级都会叠加前面等级的效果。「累计」是该级加上前面所有等级的总效果。')];

        if (stacks.indexOf('times') >= 0) lines.push(L('Discounts multiply: two levels of 10% off make 19% off.', '折扣相乘：两级各10%的折扣，合计19%。'));

        if (stacks.indexOf('max') >= 0) lines.push(L('Skill levels at the start do not add up: the highest level counts.', '开局熟练度不叠加，以最高一级为准。'));

        return '<div class="nnote">' + lines.join(' ') + '</div>';
      }

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

        const values = range(count).map(i => fmt(defaultAt(p, i)));
        const isSameEachLevel = T.levelsAddUp && count > 1 && values.every(v => v === values[0]);
        const text = isSameEachLevel ? values[0] + ' ' + unitOf(p) + ' ' + L('each level', '每级') : values.join(' / ') + ' ' + unitOf(p);

        return '<tr><td>' + esc(L(p.en, p.zh)) + '</td><td>' + esc(text) + '</td></tr>';
      }

      function readsHtml(e, f, count) {
        try {
          const g = T.makeGive(f, '', e.id, null, count);
          const zh = T.st.lang === 'zh';
          const desc = g.fx.desc(valsOf(g, 0), zh, f.params);

          return '<div class="fxd-rd"><div class="l">' + esc(readsLine(g, count, zh)) + '</div><div class="d">' + esc(desc) + '</div></div>';
        } catch (err) {
          return '';
        }
      }

      // What a new effect gives at each level. Where levels add up, it also gives the totals.
      function readsLine(g, count, zh) {
        const perLevel = range(count).map(i => autoLine(g, i, zh));
        const totals = range(count).map(i => g.fx.line(totalVals(g, i), zh));

        if (!T.levelsAddUp || g.fx.raw || totals.join() === perLevel.join()) return perLevel.join(' / ');

        const each = perLevel.every(x => x === perLevel[0]) ? L('Each level: ', '每级：') + perLevel[0] : L('Levels: ', '各级：') + perLevel.join(' / ');

        return each + L('. In total: ', '。累计：') + totals.join(' / ');
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
      'P .fxd-gc{padding:8px 12px;margin-bottom:6px;border:1px solid var(--bd2);background:rgba(0,0,0,0.2)}',
      'P .fxd-gc .n{color:var(--hl);font-size:.86rem;font-weight:700}',
      'P .fxd-gc .d{margin-top:4px;font-size:.9rem;line-height:1.5}',
      'P .fxd-gc .m{margin-top:4px;font:.8rem var(--mono);color:var(--tm)}',
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

    return { MAX_LEVELS, MAX_GIVES, GAME_PREFIX, REWARD_ICONS, ICON_GROUPS, TOAST_MS, INT_FIELD, STAT_MAX, NO_SCRIPT, VARS, scopedCss, injectStyle, isObj, isArr, isNum, clone, round2, fmt, range, L, ESCAPES, esc, isEmptyVal, zeroOf, allKeys, sameVal, h, normText, textIn, shown, CATS, GROUP_TALENTS, SKILL_TALENTS, XP_SKILLS, WORK, STATS, ROBOTS, robotScript, robotName, named, nameOptions, num, pct, neg, hours, pick, hide, fromTemplate, fill, optionsOf, isOptionOn, availableOptions, groupUnknown, opt, itemCount, plural, numericBuffKeys, FX, fx, FX_BY_ID, picksOf, numsOf, hidesOf, fieldName, display, toRaw, defaultAt, match, rawCache, FIELD_RANK, prettyScript, rawLabel, RAW_NOTE, unitText, cardText, readsAsPercent, rawFx, gameCards, gameCardsHtml, cat, talentIcons, fxIcon, fxName, fromRoot, scriptOfGive, fixedFrom, roundTrips, isEditable, isAutoOn, showsAuto, valsOf, autoLine, autoLevelText, autoDesc, plainDesc, addsUp, totalVals, totalText, totalNoteHtml, baseText, nameOf, descOf, hasLevelDescs, entryOf, entryOrGhost, levelCount, markChanged, givesChanged, adopt, resetOrDelete, nextStep, setGive, setPick, writeText, saved, discard, send, numText, unitOf, tag, isGhost, listIds, searchWords, searchText, matchesSearch, searchHtml, noMatchHtml, refreshList, clearSearch, focusSearch, showSelectedRow, fitHeight, watchHeight, renderAfterChange, watchPresses, onSearchKey, refreshSaved, pvBarHtml, editorHtml, hasNoScript, ghostHtml, ghostLevelsHtml, pickHtml, stepper, descTarget, refreshPreview, talentName, winEl, haystack, matchesQuery, pickerLists, isOnOtherGive, closePicker, renderPicker, railHtml, cardMeta, listHtml, paramRow, readsHtml, readsLine, chooseButton, choose, onPickerDouble, onPickerKey, iconsHtml, iconGroupName, showIconGroup, closeIcons, onDocDown, closeOverlays, closeToast, clampLevel, select, setText, viewIds, keepSelectionInView, afterRemove, onInput, hideTab, escapeTab, editorCss };
  }
})();
