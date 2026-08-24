using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 沆「沆瀣」（礼物序 09）：夜间墨系全伤 +15%、墨洼向上蒸腾瘴雾柱
    /// （缓升伤害柱 0.3x 滴伤 / 0.5s 一轮）；代价白昼墨系 -5%。
    /// 通道所有权：洼的"蒸腾"归沆。<br/>
    /// 会话仓语义：MeterA=夜色浸染 0~1（各端本地缓变，只喂洼面配色，昼夜判 Main.dayTime 各端一致）
    /// </summary>
    internal sealed class FuHang : KikasaTalismanDefinition
    {
        /// <summary>夜间墨系增伤 / 白昼墨系减伤</summary>
        private const float NightDamageMul = 1.15f;
        private const float DayDamageMul = 0.95f;

        /// <summary>瘴雾柱伤害：0.3x 滴基准。洼伤=滴伤 x0.35，自洼伤反推 0.3/0.35。
        /// 若同挂渍符，洼伤已折其 0.75 代价，柱伤随之略低——跨符污染可接受的基线近似</summary>
        private const float MiasmaFromPuddleMul = 0.86f;

        /// <summary>蒸腾节拍：每口洼隔多少帧升一柱（按洼的 timeLeft 取模，各端同解）</summary>
        private const int MiasmaCadenceFrames = 54;

        public override int SortOrder => 109;

        /// <summary>夜瘴绿：见不得光的水汽</summary>
        public override Color InkAccent => new(112, 178, 118);

        //沆：雨盖下对偶双雾涡，一升一沉，下缀一点朱点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.09f, -0.24f, 0.08f, 0.20f, -0.60f, 3.60f, 12),
            Arc(0.09f, 0.24f, 0.34f, 0.16f, 6.70f, 2.54f, 12),
            Dot(0.10f, 0.02f, 0.60f),
        ];

        //====行为====

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //昼夜窗吃全部墨系来源；Main.dayTime 各端一致，命中判定只在所有者端
            modifiers.FinalDamage *= Main.dayTime ? DayDamageMul : NightDamageMul;
        }

        internal override void OnPuddleUpdate(in KikasaTalismanRainContext ctx, Projectile puddle) {
            if (Main.dayTime) {
                return;
            }
            //夜雾浮点：各端本地表现
            if (!Main.dedServ) {
                FuHangFX.PuddleNightMotes(puddle, InkAccent);
            }
            //蒸腾归沆：所有者端按洼的剩余寿命取模起柱，快干的洼不再蒸
            if (ctx.IsOwnerClient && puddle.timeLeft > 40
                && puddle.timeLeft % MiasmaCadenceFrames == 0) {
                float radiusMul = puddle.ai[0] > 0.01f ? puddle.ai[0] : 1f;
                float xOff = Main.rand.NextFloat(-0.32f, 0.32f) * KikasaInkPuddle.WidthPx * radiusMul;
                Projectile.NewProjectile(puddle.GetSource_FromThis(),
                    puddle.Center + new Vector2(xOff, -10f), Vector2.Zero,
                    ModContent.ProjectileType<FuHangMiasmaColumn>(),
                    Math.Max((int)(puddle.damage * MiasmaFromPuddleMul), 1),
                    0f, ctx.Owner.whoAmI);
            }
        }

        internal override void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) {
            //夜色浸染随会话计量缓变，入夜洼面转瘴绿、拂晓退回墨色
            KikasaTalismanSessionState state = ctx.StateFor(this);
            float night = state?.MeterA ?? (Main.dayTime ? 0f : 1f);
            if (night <= 0.01f) {
                return;
            }
            draw.Deep = Color.Lerp(draw.Deep, new Color(26, 44, 30), night);
            draw.Body = Color.Lerp(draw.Body, new Color(38, 62, 40), night);
            draw.Core = Color.Lerp(draw.Core, InkAccent, night * 0.8f);
            draw.Sheen = Color.Lerp(draw.Sheen, new Color(170, 220, 168), night * 0.7f);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //夜色浸染的缓变泵：各端本地推进，昼夜源头一致故端间近似
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                float target = Main.dayTime ? 0f : 1f;
                state.MeterA = MathHelper.Lerp(state.MeterA, target, 0.02f);
            }
        }
    }

    /// <summary>沆符纸：礼物符不配合成配方，随礼物戏发放（礼物序 09）</summary>
    internal sealed class KikasaTalismanHang : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuHang);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "夜瘴符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "夜间墨伤提升，墨洼蒸起瘴雾柱；白昼略降");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "老话说，夜里的水汽碰不得。符师偏偏收了一瓶，果然碰不得。那就让敌人去碰");
            this.GetLocalization("Power",
                () => "「夜瘴」夜间墨系伤害 +15%，墨洼不断蒸起瘴雾柱（每半秒 30% 滴伤）");
            this.GetLocalization("Burden",
                () => "白昼墨系伤害 -5%");
            base.SetDefaults();
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
