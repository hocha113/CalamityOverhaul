using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【钴套·疾影残像】蓝钴淬火的速攻之甲：①命中积攒迅斩，满五层兑换六道残像剑气
    /// ②五秒姿态期内每次命中，自目标侧翼闪出一道钴蓝残像剑气穿刺补刀 ③姿态中疾跑拖出钴蓝流光。
    /// 原版套装奖励（+15% 移速）保留，神赋叠加
    /// </summary>
    internal class GsCobaltArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.CobaltHat, ItemID.CobaltHelmet, ItemID.CobaltMask];

        public override int BodyID => ItemID.CobaltBreastplate;

        public override int LegsID => ItemID.CobaltLeggings;

        protected override string EndowLineFallback =>
            "Cobalt Flicker: strikes build swiftness; at 5 stacks enter a 5s stance where each strike calls a cobalt afterimage slash from the flank (6 charges)";

        //钴蓝色板
        internal static readonly Color CobaltBright = new(170, 215, 255);
        internal static readonly Color CobaltMain = new(70, 130, 245);
        internal static readonly Color CobaltDeep = new(28, 52, 140);

        protected override int FullCharge => 5;

        protected override Color ThemeMain => CobaltMain;

        protected override Color ThemeBright => CobaltBright;

        /// <summary>姿态期可用的残像剑气道数</summary>
        private const int StanceSlashes = 6;

        /// <summary>姿态持续帧数</summary>
        private const int StanceDuration = 300;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsCobaltAfterimageProj>();

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (!state.EndowFlag) {
                base.UpdateEndowment(player, state);
                return;
            }
            //姿态期：超时或剑气用尽即收势
            if (Main.GameUpdateCount > state.EndowTimer || state.EndowCharge <= 0) {
                state.EndowFlag = false;
                state.EndowCharge = 0;
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(player.Center, CobaltMain.ToVector3() * 0.3f);
            //疾影：移动即拖钴蓝流光（个人读数）
            if (player.velocity.Length() > 2.5f && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(
                    player.Center + Main.rand.NextVector2Circular(8f, 16f) - player.velocity,
                    -player.velocity * 0.15f, CobaltMain, Main.rand.NextFloat(0.10f, 0.16f))
                    ?.Configure(12, 0.6f, 2f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            if (!state.EndowFlag) {
                base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
                return;
            }

            //姿态中：消耗一道剑气，自目标侧翼闪出残像补刀
            state.EndowCharge--;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                int slashDamage = Math.Clamp((int)(damageDone * 0.35f), 10, 150);
                //侧翼起手：随机上下偏摆，穿过目标身位
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.UnitX);
                Vector2 lateral = dir.RotatedBy(MathHelper.PiOver2) * side;
                Vector2 from = target.Center + lateral * 110f + Main.rand.NextVector2Circular(12f, 12f) - dir * 40f;
                Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * 21f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithCobaltEndow"),
                    from, vel, ModContent.ProjectileType<GsCobaltAfterimageProj>(),
                    slashDamage, 1f, player.whoAmI);
            }
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            //满层：兑入疾影姿态
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount + StanceDuration;
            state.EndowCharge = StanceSlashes;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = 0.5f }, player.Center);
            //入势演出：钴蓝环闪炸开
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_Spark>(player.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(3f, 5.5f),
                    i % 2 == 0 ? CobaltBright : CobaltMain, 0.5f)?.Configure(false, Main.rand.Next(14, 22));
            }
            PRTLoader.NewParticle<PRT_Light>(player.Center, Vector2.Zero, CobaltBright, 0.2f)?.Configure(10, 0.8f);
        }
    }

    /// <summary>
    /// 钴蓝残像剑气：一道淬火钢色的月牙残影，自侧翼疾闪穿刺目标；
    /// 月牙本体三层叠色 + 残影拖迹 + 速度拉伸，命中迸出淬火钢屑
    /// </summary>
    internal class GsCobaltAfterimageProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.8231f % 4.13f;

        /// <summary>出生 3 帧淡入、末尾 5 帧淡出</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            //前段全速穿刺，后段骤减收势（不匀速）
            if (Life > 9f) {
                Projectile.velocity *= 0.86f;
            }
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(6f, 6f),
                    Projectile.velocity * 0.08f,
                    Main.rand.NextBool(3) ? GsCobaltArmor.CobaltDeep : GsCobaltArmor.CobaltMain,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(7, 12));
            }
            Lighting.AddLight(Projectile.Center, GsCobaltArmor.CobaltMain.ToVector3() * (0.28f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //淬火钢屑沿刃向迸溅
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            Vector2 edge = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    edge.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? GsCobaltArmor.CobaltBright : GsCobaltArmor.CobaltMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制：三层月牙 + 残影拖迹 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            if (crescent == null || brush == null) {
                return false;
            }
            float fade = VisualFade;
            float rotation = Projectile.rotation;
            Vector2 origin = crescent.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.05f, 0.5f);
            //刃面张力抖动（identity 种子，禁 Main.rand）
            float wob = MathF.Sin(Life * 0.55f + Seed * 5f) * 0.07f;

            //残影拖迹：旧位置画渐淡月牙
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.35f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(crescent, gpos, null,
                    (GsCobaltArmor.CobaltDeep with { A = 0 }) * ghost, rotation, origin,
                    new Vector2(0.30f + stretch * 0.5f, 0.20f) * (1f - i * 0.06f), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //底衬：涂抹拉丝层
            Main.EntitySpriteDraw(brush, pos, null,
                (GsCobaltArmor.CobaltDeep with { A = 0 }) * (0.7f * fade), rotation, brush.Size() * 0.5f,
                new Vector2(0.5f + stretch, 0.16f + wob * 0.5f), SpriteEffects.None, 0);
            //月牙主体
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsCobaltArmor.CobaltMain with { A = 0 }) * fade, rotation, origin,
                new Vector2(0.36f + stretch * 0.8f, 0.26f + wob), SpriteEffects.None, 0);
            //亮刃芯
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsCobaltArmor.CobaltBright with { A = 0 }) * (0.8f * fade), rotation, origin,
                new Vector2(0.24f + stretch * 0.5f, 0.13f + wob * 0.6f), SpriteEffects.None, 0);
            return false;
        }
    }
}
