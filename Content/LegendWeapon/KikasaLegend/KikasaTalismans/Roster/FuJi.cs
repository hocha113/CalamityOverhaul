using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霁「霁光」（礼物序 22，占有"收伞"通道）：撑伞期间命中蓄霁（有上限），
    /// 收伞瞬间云开一线，在光标处轰落一束霁光（按蓄量折算的大额单发，所有者端生成）并清零；
    /// 代价是撑伞期间墨系全伤 -8%。<br/>
    /// 会话仓语义：MeterA=蓄霁量（仅 owner 端累积为权威，起伞清零、收伞结算清零；
    /// 旁观端恒为零，收伞逆卷演出按固定档近似）
    /// </summary>
    internal sealed class FuJi : KikasaTalismanDefinition
    {
        /// <summary>蓄霁上限（命中次数）</summary>
        private const float MeterCap = 30f;

        /// <summary>起束门槛：蓄不满几滴就收伞，云不开</summary>
        private const float MinFireCharge = 3f;

        /// <summary>霁光束伤害折算：基础 2x 单滴，满蓄再 +8x</summary>
        private const float BeamBaseMul = 2f;
        private const float BeamFullBonusMul = 8f;

        public override int SortOrder => 122;

        /// <summary>霁金：雨停那一刻云缝里漏下来的光色</summary>
        public override Color InkAccent => new(240, 206, 118);

        //霁：雨盖自顶裂开一线，缝下三线直光垂落，中长旁短
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Arc(0.12f, 0f, -0.42f, 0.52f, 3.42f, 4.55f, 6),
            Arc(0.12f, 0f, -0.42f, 0.52f, 4.88f, 6.00f, 6),
            L(0.10f, 0.00f, -0.34f, 0.00f, 0.62f),
            L(0.08f, -0.17f, -0.28f, -0.21f, 0.34f),
            L(0.08f, 0.17f, -0.28f, 0.21f, 0.40f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            //蓄霁随撑伞会话走：起伞清零，本次撑伞蓄本次的光
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.MeterA = 0f;
            }
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //四源命中皆蓄霁（本挂钩仅所有者端派发，MeterA 即权威读数）
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.MeterA = MathF.Min(state.MeterA + 1f, MeterCap);
            }
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //代价：撑伞攻击期间全伤 -8%。伞常驻后在场数恒真，改读攻击态口径
            //（随行的闲伞不算；伞收了洼还在烫，那时不减）
            if (KikasaRainUmbrella.OwnerIsRaining(ctx.Owner)) {
                modifiers.FinalDamage *= 0.92f;
            }
        }

        internal override void OnRecall(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            float meterT = MathHelper.Clamp(state.MeterA / MeterCap, 0f, 1f);
            //雨幕逆卷各端本地演出；旁观端蓄量恒零，按固定档近似（演出允许近似）
            FuJiFX.RecallCurl(umbrella, InkAccent, ctx.IsOwnerClient ? meterT : 0.6f);

            //霁光束只在所有者端生成（自然同步）；蓄不够门槛云不开，只清零
            if (ctx.IsOwnerClient && state.MeterA >= MinFireCharge) {
                Vector2 landing = FuJiFX.SolveLanding(Main.MouseWorld);
                int damage = (int)(umbrella.damage * (BeamBaseMul + BeamFullBonusMul * meterT));
                Projectile.NewProjectile(umbrella.GetSource_FromThis(), landing, Vector2.Zero,
                    ModContent.ProjectileType<KikasaFuJiLightBeam>(),
                    damage, umbrella.knockBack * 2f, umbrella.owner, meterT);
            }
            state.MeterA = 0f;
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //蓄量读数是 owner 私仓，蓄光的金浮尘也只给 owner 看
            if (Main.dedServ || !ctx.IsOwnerClient || !Main.rand.NextBool(9)) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.MeterA < MeterCap * 0.5f) {
                return;
            }
            int umbrellaType = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != umbrellaType || proj.owner != ctx.Owner.whoAmI) {
                    continue;
                }
                //蓄霁过半伞缘浮金：一粒金尘缓缓上飘，读作"光在伞里攒着"
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    proj.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(0f, 8f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.1f)),
                    Color.Lerp(InkAccent, Color.White, 0.3f),
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(18, 28), -0.02f, 0.98f);
                break;
            }
        }
    }

    /// <summary>霁符纸：礼物符不配合成配方，随礼物戏发放（获取期四）</summary>
    internal sealed class KikasaTalismanJi : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuJi);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "天晴符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "撑伞命中攒晴意，收伞在光标处轰下一束天光；撑伞期间全伤略减");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "雨过天晴的那一刻，云缝里会漏下一柱光。符师蹲守半月，赶在云合拢前把它拓进了符里");
            this.GetLocalization("Power",
                () => "「放晴」撑伞期间的命中不断积攒晴意；收伞瞬间云开一线，在光标处轰落一束天光，伤害按积攒折算，攒满最烈");
            this.GetLocalization("Burden",
                () => "撑伞期间墨系伤害 -8%");
            base.SetDefaults();
            Item.rare = ItemRarityID.Purple;
        }
    }
}
