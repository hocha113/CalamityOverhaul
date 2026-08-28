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
    /// 潦「积潦」（合成次符，SortOrder 1）：大滴落地必积洼（不须湖倾档）、
    /// 洼寿 x1.5、洼径 x1.3、滴伤 x0.95；
    /// 新增「涨潦」：墨滴汇入既有墨洼（合并续命）令水位 +1，第三注「满潦」漫溢——
    /// 洼阔一步（+12%/次，至多两步）并向两侧拍出溢流波（各 0.5x 洼伤，仅所有者端），
    /// 水位回落重蓄。
    /// 通道所有权：洼的水位/涨溢（材质归霜、蒸腾归沆、运动归汐，互不越界）。<br/>
    /// 会话仓语义：不使用。涨潦账全在洼实例上（TalismanMeter=续命总数、
    /// TalismanLifeAnchor=上帧寿命锚）；续命的 timeLeft 回顶不在弹幕同步包里，
    /// 只有所有者端观测得到——溢流波是真弹幕随生成包同步（旁观照见），
    /// 水位染色/涨圈/浮泡为 owner 端近似独享，联机纪律已核（2026-08 案 27 包核对）
    /// </summary>
    internal sealed class FuLao : KikasaTalismanDefinition
    {
        /// <summary>涨满几注漫溢一次</summary>
        private const int StageCap = 3;

        /// <summary>每次满潦的洼宽加成与次数上限</summary>
        private const float WidenPerOverflow = 0.12f;
        private const int OverflowCap = 2;

        /// <summary>溢流波伤害 = 洼伤 x 此倍率</summary>
        private const float SurgeDamageMul = 0.5f;

        public override int SortOrder => 1;

        public override Color InkAccent => new(92, 156, 134);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.PuddleUnlock = true;
            profile.PuddleLifeMul *= 1.50f;
            profile.PuddleRadiusMul *= 1.30f;
            profile.DropDamageMul *= 0.95f;
        }

        //潦：一滴垂落，碗形积潦，潦面两圈涟纹，潦缘一勾外溢
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.11f, -0.52f, 0.46f),
            L(0.08f, 0.00f, -0.34f, 0.00f, -0.02f),
            Dot(0.12f, 0.00f, -0.44f),
            Arc(0.13f, 0.00f, 0.10f, 0.56f, 0.30f, 2.84f, 14),
            Arc(0.09f, 0.00f, 0.22f, 0.34f, 0.48f, 2.66f, 10),
            Arc(0.07f, 0.00f, 0.30f, 0.16f, 0.60f, 2.54f, 8),
            L(0.07f, 0.54f, 0.26f, 0.74f, 0.44f),
        ];

        //====行为====

        internal override void OnPuddleUpdate(in KikasaTalismanRainContext ctx, Projectile puddle) {
            if (puddle.ModProjectile is not KikasaInkPuddle host) {
                return;
            }
            //涨潦账：凭 timeLeft 回顶观测续命。立锚帧只取出生寿命上界不做比较——
            //洼身在本帧挂钩之后才把 timeLeft 钳到该值，出生钳制拍不当续命
            if (host.TalismanLifeAnchor == 0) {
                float lifeMul = puddle.ai[1] > 0.01f ? puddle.ai[1] : 1f;
                host.TalismanLifeAnchor = KikasaInkPuddle.SpawnLifeFrames(lifeMul);
            }
            else {
                int anchor = host.TalismanLifeAnchor;
                host.TalismanLifeAnchor = Math.Max(puddle.timeLeft, 1);
                if (puddle.timeLeft > anchor) {
                    //一注墨到账：水位 +1；第三注满潦漫溢，水位归零重蓄
                    host.TalismanMeter += 1f;
                    int total = (int)host.TalismanMeter;
                    if (total % StageCap == 0) {
                        FuLaoFX.OverflowCrash(puddle, InkAccent);
                        if (ctx.IsOwnerClient) {
                            //溢流波：向两侧各拍一道，半份洼伤（生成包自然同步，旁观照见）
                            int damage = Math.Max((int)(puddle.damage * SurgeDamageMul), 1);
                            for (int dir = -1; dir <= 1; dir += 2) {
                                Projectile.NewProjectile(puddle.GetSource_FromThis(),
                                    puddle.Center - Vector2.UnitY * 6f, new Vector2(dir * 3.4f, 0f),
                                    ModContent.ProjectileType<FuLaoOverflowSurge>(),
                                    damage, 1.5f, ctx.Owner.whoAmI);
                            }
                        }
                    }
                    else {
                        FuLaoFX.RiseRipple(puddle, total % StageCap, InkAccent);
                    }
                }
            }
            //满潦次数折洼宽：判定可见同源旋钮，逐帧叠乘（旋钮每帧派发前回 1）
            int overflows = Math.Min((int)host.TalismanMeter / StageCap, OverflowCap);
            if (overflows > 0) {
                host.TalismanWidthMul *= 1f + WidenPerOverflow * overflows;
            }
            //洼面浮泡：水位越高泡越稠，纯表现
            if (!Main.dedServ) {
                FuLaoFX.StageBubbles(puddle, (int)host.TalismanMeter % StageCap, InkAccent);
            }
        }

        internal override void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) {
            //水位读数上色：一注深一分，沼青缘、亮反光；空位保持墨色底。
            //只动缘/体/反光，血芯不碰（墨的身份留住）
            if (puddle.ModProjectile is not KikasaInkPuddle host) {
                return;
            }
            int stage = (int)host.TalismanMeter % StageCap;
            if (stage <= 0) {
                return;
            }
            float t = stage / (float)(StageCap - 1);
            draw.Deep = Color.Lerp(draw.Deep, new Color(24, 46, 36), t * 0.8f);
            draw.Body = Color.Lerp(draw.Body, new Color(34, 60, 46), t * 0.65f);
            draw.Sheen = Color.Lerp(draw.Sheen, new Color(178, 226, 198), t * 0.7f);
        }
    }

    /// <summary>潦符纸：合成次符（近水工作台），非礼物符</summary>
    internal sealed class KikasaTalismanLao : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuLao);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "墨洼符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨滴落地必积墨洼，墨洼吃雨涨水位、满则漫溢；直击略轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "符师嫌雨落完就干，白瞎了一场墨，便画了这张符把雨留在地上。多淹一刻是一刻");
            this.GetLocalization("Power",
                () => "「积洼」墨滴落地必积成墨洼（不必等湖倾档），墨洼持续 +50%、范围 +30%；墨滴汇入已有墨洼令水位上涨，第三注满潦漫溢：洼面阔一步，并向两侧拍出溢流波（各 50% 洼伤）");
            this.GetLocalization("Burden",
                () => "墨滴直击伤害 -5%");
            base.SetDefaults();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.RainCloud, 4)
                .AddIngredient(ItemID.Silk, 2)
                .AddIngredient(ItemID.BlackInk, 1)
                .AddIngredient(ItemID.BottledWater, 2)
                .AddTile(TileID.WorkBenches)
                .AddCondition(Condition.NearWater)
                .Register();
        }
    }
}
