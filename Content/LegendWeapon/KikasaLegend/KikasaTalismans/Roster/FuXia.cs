using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霞「焚霞」（礼物序 21）：晨昏窗内墨系全伤 +18%，墨滴命中点燃『霞焰』短灼（叠层 DoT），
    /// 全套雨具换橙金调、滴尾霞光；正午与夜半 -6%。<br/>
    /// 时窗边界（Main.time/dayTime 原版同步，各端确定一致；
    /// 与沆的夜窗、霸的月窗互补成全天钟面）：<br/>
    /// 晨昏窗＝日出日落前后各 1.5 游戏小时（5400 tick）——
    /// dayTime 且 time&lt;5400（日出后）或 time&gt;48600（日落前）；
    /// !dayTime 且 time&lt;5400（日落后）或 time&gt;27000（日出前）。<br/>
    /// 钝窗＝正午/夜半前后各 1.5 游戏小时——
    /// dayTime 且 |time-27000|&lt;5400；!dayTime 且 |time-16200|&lt;5400。<br/>
    /// 会话仓：本符不占任何会话字段（时窗读全局时钟，霞焰在 <see cref="KikasaTalismanStackNPC"/>）
    /// </summary>
    internal sealed class FuXia : KikasaTalismanDefinition
    {
        /// <summary>霞焰层上限</summary>
        private const int EmberCap = 3;

        /// <summary>霞焰单次刷新时长（帧）＝短灼 2.5 秒</summary>
        private const int EmberTimerFrames = 150;

        public override int SortOrder => 121;

        /// <summary>焚霞橙：烧了半边天的那个颜色</summary>
        public override Color InkAccent => new(255, 158, 82);

        /// <summary>晨昏窗：日出日落前后各 1.5 游戏小时（边界见类头注释）</summary>
        internal static bool InGlowWindow => Main.dayTime
            ? Main.time < 5400.0 || Main.time > 48600.0
            : Main.time < 5400.0 || Main.time > 27000.0;

        /// <summary>钝窗：正午与夜半前后各 1.5 游戏小时（边界见类头注释）</summary>
        internal static bool InDeadWindow => Main.dayTime
            ? Math.Abs(Main.time - 27000.0) < 5400.0
            : Math.Abs(Main.time - 16200.0) < 5400.0;

        //霞：雨盖下三层渐染横霞一层淡过一层，天边半沉一轮日核
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.11f, -0.44f, -0.14f, 0.40f, -0.10f),
            L(0.09f, -0.34f, 0.10f, 0.30f, 0.14f),
            L(0.07f, -0.22f, 0.34f, 0.20f, 0.36f),
            Dot(0.16f, 0.00f, 0.58f),
        ];

        //====行为====

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //晨昏 +18%，正午/夜半 -6%，其余时段恒等
            if (InGlowWindow) {
                modifiers.FinalDamage *= 1.18f;
            }
            else if (InDeadWindow) {
                modifiers.FinalDamage *= 0.94f;
            }
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //窗内滴击点燃霞焰：短灼叠层，窗一过就只剩余烬烧完
            if (kind != KikasaRainSourceKind.Drop || !InGlowWindow) {
                return;
            }
            KikasaTalismanStackNPC.AddStacks(npc, this, 1, EmberCap, EmberTimerFrames);
        }

        internal override void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) {
            //霞焰短灼：每层 4/s，短而烈
            npc.lifeRegen -= 8 * stacks;
            damage = Math.Max(damage, 2 * stacks);
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //窗内打霞标（先到先得）：橙金滴色与滴尾霞光都认这枚标
            if (!InGlowWindow || drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //霞标滴换橙金调：焚霞暗体+燃橙缘+金白芯
            draw.Body = new Color(52, 24, 10);
            draw.Deep = new Color(150, 70, 26);
            draw.Core = new Color(255, 216, 140);
        }

        internal override void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) {
            //窗内墨洼浮霞：整套换橙金，窗外还墨
            if (!InGlowWindow) {
                return;
            }
            draw.Deep = new Color(112, 48, 20);
            draw.Body = new Color(58, 26, 12);
            draw.Core = new Color(255, 190, 96);
            draw.Sheen = new Color(255, 228, 152);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            if (Main.dedServ || !InGlowWindow) {
                return;
            }
            //霞标滴的滴尾霞光：纯表现各端本地跑；标随生成包同步，旁观端同样看得到
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                    continue;
                }
                FuXiaFX.DropGlowTail(proj, InkAccent);
            }
        }

        internal override void DrawNPCStack(SpriteBatch spriteBatch, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            FuXiaFX.DrawEmberFlecks(spriteBatch, npc, stacks, screenPos, InkAccent);
        }
    }

    /// <summary>霞符纸：礼物符不配合成配方，随礼物戏发放（获取期四）</summary>
    internal sealed class KikasaTalismanXia : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuXia);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霞");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "晨昏时段墨系全伤更烈，滴击燃霞焰；正午与夜半略钝");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "晨霞烧了半边天，雨还没有停。写符的人蘸着那个颜色落笔——霞也是被烧开的雨");
            this.GetLocalization("Power",
                () => "「焚霞」日出日落前后各一个半时辰，墨系全伤 +18%，墨滴命中点燃『霞焰』短灼；霞时全套雨具染作橙金");
            this.GetLocalization("Burden",
                () => "正午与夜半墨系 -6%。霞只肯在天边烧");
            base.SetDefaults();
            Item.rare = ItemRarityID.Purple;
        }
    }
}
