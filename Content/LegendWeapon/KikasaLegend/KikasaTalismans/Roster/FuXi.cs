using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 汐「潮汐」（礼物序 05）：墨洼得潮性——随潮息呼吸涨落（宽度旋钮，判定可见同源），
    /// 洼龄蓄满即向最近之敌涌潮短移，浪缘拍出一道 0.5x 洼伤判定；代价是洼寿命 -20%。<br/>
    /// 通道所有权：只动洼的<b>运动</b>与湿反光白沫，洼面材质（霜）/蒸腾（沆）不碰。<br/>
    /// 潮钟取洼自身 timeLeft（随生成包同步）推洼龄：墨滴合并续命把 timeLeft 顶回出生值、
    /// 洼龄归零重新蓄潮——"新墨落洼，潮再起"。浪缘判定只在归属端生成；本符不占会话仓
    /// </summary>
    internal sealed class FuXi : KikasaTalismanDefinition
    {
        /// <summary>洼寿命代价</summary>
        private const float PuddleLifePenalty = 0.80f;

        /// <summary>潮息涨落幅度（宽度旋钮，判定随涨落）</summary>
        private const float BreathAmp = 0.10f;

        /// <summary>涌潮蓄势帧：洼出生/续命归零后再蓄这么久才起潮</summary>
        internal const int SurgeArmAge = 30;

        /// <summary>涌潮持续帧</summary>
        internal const int SurgeFrames = 26;

        /// <summary>浪缘判定伤害 = 洼伤 × 此倍率</summary>
        private const float WaveDamageMul = 0.5f;

        /// <summary>涌潮觅敌半径</summary>
        internal const float SeekRange = 520f;

        public override int SortOrder => 105;

        /// <summary>海青：退潮后滩洼里那一汪小海</summary>
        public override Color InkAccent => FuXiFX.Accent;

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.PuddleLifeMul *= PuddleLifePenalty;
        }

        //汐：雨盖下一弯月弧压住底横（潮压着滩），弧梢外扬一点飞沫
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.10f, 0.02f, 0.24f, 0.30f, 3.30f, 6.20f, 12),
            L(0.10f, -0.46f, 0.54f, 0.48f, 0.54f),
            Dot(0.10f, 0.38f, 0.30f),
        ];

        //====行为====

        internal override void OnPuddleUpdate(in KikasaTalismanRainContext ctx, Projectile puddle) {
            if (puddle.ModProjectile is not KikasaInkPuddle host) {
                return;
            }
            //呼吸涨落：宽度旋钮每帧派发前回 1，须逐帧叠乘；涌潮期再鼓一成半
            float surgeT = FuXiFX.SurgeT(puddle);
            float swell = surgeT >= 0f ? 0.15f * (1f - surgeT) : 0f;
            host.TalismanWidthMul *= 1f + BreathAmp * FuXiFX.Breath(puddle) + swell;

            if (surgeT < 0f) {
                return;
            }
            //涌潮：各端同规则压向最近敌（旁观端近似一致，伤害只认归属端的浪缘）
            NPC prey = FuXiFX.NearestPrey(puddle, SeekRange);
            if (prey == null) {
                return;
            }
            float dir = prey.Center.X >= puddle.Center.X ? 1f : -1f;
            FuXiFX.SurgeStep(puddle, dir, surgeT);
            FuXiFX.SurgeFoam(puddle, dir, surgeT);
            if (surgeT == 0f) {
                FuXiFX.SurgeCrash(puddle, dir);
                if (ctx.IsOwnerClient) {
                    //浪缘判定：一潮一浪，半份洼伤（随生成包同步）
                    Projectile.NewProjectile(puddle.GetSource_FromThis(),
                        puddle.Center - Vector2.UnitY * 8f, new Vector2(dir * 4.2f, 0f),
                        ModContent.ProjectileType<FuXiTideWave>(),
                        (int)(puddle.damage * WaveDamageMul), 2f, ctx.Owner.whoAmI);
                }
            }
        }

        internal override void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) {
            //浪缘白沫只动湿反光通道：洼面材质归霜、蒸腾归沆，汐不越界
            float foam = FuXiFX.FoamT(puddle);
            if (foam > 0f) {
                draw.Sheen = Color.Lerp(draw.Sheen, FuXiFX.FoamWhite, foam);
            }
        }
    }

    /// <summary>汐符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanXi : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuXi);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·汐");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨洼随潮呼吸，向近敌涌潮拍浪；洼寿命略短");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "海退了以后，滩上留下一洼一洼的小海。每一洼都还记得涨潮的时辰，到点便自己动身");
            this.GetLocalization("Power",
                () => "「潮汐」墨洼得潮性：随潮息涨落，新墨落洼便向最近之敌涌潮，浪缘拍出半份洼伤");
            this.GetLocalization("Burden",
                () => "墨洼寿命 -20%。潮起得急，退得也快");
            base.SetDefaults();
        }
    }
}
