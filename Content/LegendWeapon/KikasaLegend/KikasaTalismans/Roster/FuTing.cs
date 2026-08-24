using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霆「雷霆」（礼物序 07）：满蓄墨泉化雷霆水柱（柱高 x1.5、打霆标），
    /// 霆标泉命中链电至附近至多 2 敌（各 0.5x）；未满蓄墨瀑触地 25% 唤起一道小雷泉。
    /// 代价蓄墨速率 x0.87。<br/>
    /// 会话仓语义：TimerB=链电节流冷却（各端本地自减，链电本体仅所有者端生成）
    /// </summary>
    internal sealed class FuTing : KikasaTalismanDefinition
    {
        /// <summary>满蓄雷泉柱高倍率</summary>
        private const float GeyserHeightMul = 1.5f;

        /// <summary>链电伤害倍率（取泉伤）</summary>
        private const float ChainDamageMul = 0.5f;

        /// <summary>链电最多跳向几敌</summary>
        private const int ChainTargets = 2;

        /// <summary>自被击者搜链电目标的半径</summary>
        private const float ChainRange = 320f;

        /// <summary>链电节流帧：两次跳电之间的最短间隔</summary>
        private const int ChainCooldownFrames = 18;

        /// <summary>非满蓄小雷泉：触发概率与折减</summary>
        private const float MinorGeyserChance = 0.25f;
        private const float MinorGeyserDamageMul = 0.65f;
        private const float MinorGeyserHeightMul = 1.15f;

        public override int SortOrder => 107;

        /// <summary>紫电：湖记住的那道雷</summary>
        public override Color InkAccent => new(182, 138, 244);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //雷在碗底攒着：蓄墨慢一分
            profile.ChargeRateMul *= 0.87f;
        }

        //霆：雨盖下一道折雷贯底，旁一点朱点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.11f, 0.10f, -0.26f, -0.14f, 0.06f, 0.16f, 0.16f, -0.06f, 0.62f),
            Dot(0.10f, 0.36f, 0.28f),
        ];

        //====行为====

        internal override void ModifyGeyserVolley(in KikasaTalismanRainContext ctx,
            Projectile pour, ref KikasaGeyserVolleyContext geysers) {
            if (geysers.FromFullCharge) {
                //满蓄终幕：全部墨泉化雷霆水柱，柱高随泉 ai[2] 同步到各端
                geysers.HeightMul *= GeyserHeightMul;
                if (geysers.TagId == 0) {
                    geysers.TagId = KikasaTalismanHooks.TagIdFor(this);
                }
                return;
            }
            //基础条件不满足也会派发：非满蓄墨瀑触地 25% 起一道小雷泉。
            //本挂钩仅所有者端、一瀑一次，掷骰不违确定性纪律（结果随生成包同步）
            if (!geysers.Fire && Main.rand.NextFloat() < MinorGeyserChance) {
                geysers.Fire = true;
                geysers.Count = 1;
                geysers.DamageMul *= MinorGeyserDamageMul;
                geysers.HeightMul *= MinorGeyserHeightMul;
                if (geysers.TagId == 0) {
                    geysers.TagId = KikasaTalismanHooks.TagIdFor(this);
                }
            }
        }

        internal override void OnGeyserErupt(in KikasaTalismanRainContext ctx, Projectile geyser) {
            //标签派发（各端喷发帧一次），勿重复查标：雷冠+震屏纯表现
            FuTingFX.EruptCrown(geyser, InkAccent);
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //只认霆标泉的命中（泉标签在 ai[1]）；挂钩本身仅所有者端
            if (kind != KikasaRainSourceKind.Geyser
                || KikasaTalismanHooks.ReadTagId(source.ai[1]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerB > 0) {
                return;
            }
            state.TimerB = ChainCooldownFrames;

            //自被击者就近选至多 2 个旁敌，各起一道链电弧（弹幕自然同步，各端凭生成包自绘）
            int found = 0;
            Span<int> picked = stackalloc int[ChainTargets];
            for (int n = 0; n < ChainTargets; n++) {
                int best = -1;
                float bestDist = ChainRange;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC other = Main.npc[i];
                    if (other?.active != true || i == npc.whoAmI
                        || !other.CanBeChasedBy(source)) {
                        continue;
                    }
                    bool taken = false;
                    for (int k = 0; k < found; k++) {
                        if (picked[k] == i) {
                            taken = true;
                            break;
                        }
                    }
                    if (taken) {
                        continue;
                    }
                    float dist = Vector2.Distance(other.Center, npc.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best = i;
                    }
                }
                if (best < 0) {
                    break;
                }
                picked[found++] = best;
                Projectile.NewProjectile(source.GetSource_FromThis(),
                    Main.npc[best].Center, Vector2.Zero,
                    ModContent.ProjectileType<FuTingChainZap>(),
                    Math.Max((int)(source.damage * ChainDamageMul), 1),
                    2f, ctx.Owner.whoAmI,
                    best, npc.Center.X, npc.Center.Y);
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null && state.TimerB > 0) {
                state.TimerB--;
            }
            if (Main.dedServ) {
                return;
            }
            //霆标泉逐帧裹紫电：纯表现，各端本地跑（标签随泉生成包同步）
            int geyserType = ModContent.ProjectileType<KikasaInkGeyser>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != geyserType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[1]) != myTag) {
                    continue;
                }
                FuTingFX.GeyserWrap(proj, InkAccent);
            }
        }
    }

    /// <summary>霆符纸：礼物符不配合成配方，随礼物戏发放（礼物序 07）</summary>
    internal sealed class KikasaTalismanTing : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuTing);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "雷泉符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨泉化作雷霆水柱并链电近敌，墨瀑触地偶起小雷泉；蓄墨稍缓");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "雷雨夜的湖面最吓人。闪电劈进水里，整片湖跟着一起亮。符师照着那一幕，画了这张符");
            this.GetLocalization("Power",
                () => "「雷泉」墨泉柱高 +50%，命中后链电至附近至多两名敌人（各 50% 泉伤）；未满蓄的墨瀑触地时也有 25% 唤起一道小雷泉");
            this.GetLocalization("Burden",
                () => "倒撑蓄墨速率 -13%");
            base.SetDefaults();
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
