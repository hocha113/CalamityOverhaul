using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 泷「泷落」（礼物序 12，肉山后）：墨瀑化急泷。瀑身获得持续"冲刷"位移——
    /// 落线上的敌人被顺瀑向推涌（推力只在权威端生效，联机各端靠同步收敛，
    /// 与瀑线沉溺内吸同一纪律），瀑源直击的击退顺流增强；代价是冲刷时长 -20%。
    /// 现行墨瀑本就全向跟光标，本符不动方向、只补"湍"的力与白沫水线。<br/>
    /// 会话仓语义：CounterA=本场瀑序（各端随 OnPourStart 自增，演出种子）、
    /// TimerA=最近起瀑帧锚（各端，白沫演出节奏用）
    /// </summary>
    internal sealed class FuLong : KikasaTalismanDefinition
    {
        /// <summary>瀑源直击的顺流击退倍率</summary>
        private const float PourKnockbackMul = 1.35f;

        public override int SortOrder => 112;

        /// <summary>湍白：急流劈开水面翻出的那道白</summary>
        public override Color InkAccent => new(214, 232, 236);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //泷急则短：冲刷时长 -20%
            profile.PourSustainMul *= 0.80f;
        }

        //泷：雨盖下三道斜贯急线，一急一主一短，右下朱点收势
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.10f, -0.42f, -0.18f, -0.16f, 0.60f),
            L(0.13f, -0.06f, -0.24f, 0.18f, 0.66f),
            L(0.08f, 0.28f, -0.16f, 0.46f, 0.42f),
            Dot(0.10f, 0.56f, 0.16f),
        ];

        //====行为====

        internal override void OnPourStart(in KikasaTalismanRainContext ctx, Projectile pour) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                //起瀑记录：帧锚供白沫演出定节奏，瀑序做演出种子
                state.TimerA = (int)Main.GameUpdateCount;
                state.CounterA++;
            }
            FuLongFX.PourStartRush(pour, InkAccent);
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            if (kind != KikasaRainSourceKind.Pour) {
                return;
            }
            //瀑源直击顺流：击退加成走命中管线自然同步，水平向压向瀑落向
            modifiers.Knockback *= PourKnockbackMul;
            float dirX = MathF.Cos(source.ai[0]);
            if (MathF.Abs(dirX) > 0.05f) {
                modifiers.HitDirectionOverride = dirX >= 0f ? 1 : -1;
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //冲刷位移与白沫水线都以"活着的自家墨瀑"为锚逐帧扫描，无瀑零开销；
            //推力在 RunWash 内按权威端分流，表现按 !dedServ 分流
            int pourType = ModContent.ProjectileType<KikasaInkPour>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != pourType || proj.owner != ctx.Owner.whoAmI) {
                    continue;
                }
                FuLongFX.RunWash(proj, InkAccent);
            }
        }
    }

    /// <summary>泷符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanLong : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuLong);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "冲瀑符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨瀑化作急流冲开敌人；瀑势较短");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "山洪下来的时候，没人站得住脚。符师在崖上看过一回，回来只往符里写了一个\"冲\"字");
            this.GetLocalization("Power",
                () => "「冲瀑」墨瀑化作急流：瀑身持续把落线上的敌人顺流推开，瀑源直击的击退 +35%");
            this.GetLocalization("Burden",
                () => "墨瀑持续时间 -20%");
            base.SetDefaults();
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 1);
        }
    }
}
