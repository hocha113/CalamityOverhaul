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
    /// 【山铜套·繁花乱舞】粉晶花铜之甲：①命中积攒花粉，满七层绽入乱舞窗
    /// ②四秒窗内每次命中自肩后迸出两片追踪粉晶刃瓣螺旋咬向目标，共十片
    /// ③刃瓣旋切与原版垂落花瓣区分（原版套装奖励保留，神赋叠加）
    /// </summary>
    internal class GsOrichalcumArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.OrichalcumMask, ItemID.OrichalcumHelmet, ItemID.OrichalcumHeadgear];

        public override int BodyID => ItemID.OrichalcumBreastplate;

        public override int LegsID => ItemID.OrichalcumLeggings;

        protected override string EndowLineFallback =>
            "Petal Waltz: strikes build pollen; at 7 stacks enter a 4s bloom where each strike looses two spiraling crystal petal blades (10 petals)";

        //山铜粉晶色板
        internal static readonly Color OrichalcumBright = new(255, 205, 228);
        internal static readonly Color OrichalcumMain = new(248, 108, 176);
        internal static readonly Color OrichalcumDeep = new(148, 40, 96);

        protected override int FullCharge => 7;

        protected override Color ThemeMain => OrichalcumMain;

        protected override Color ThemeBright => OrichalcumBright;

        /// <summary>乱舞窗刃瓣预算</summary>
        private const int PetalBudget = 10;

        /// <summary>乱舞窗时长（帧）</summary>
        private const int BloomDuration = 240;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsOrichalcumPetalBladeProj>();

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (!state.EndowFlag) {
                base.UpdateEndowment(player, state);
                return;
            }
            //乱舞窗：超时或刃瓣用尽即谢幕
            if (Main.GameUpdateCount > state.EndowTimer || state.EndowCharge <= 0) {
                state.EndowFlag = false;
                state.EndowCharge = 0;
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(player.Center, OrichalcumMain.ToVector3() * 0.24f);
            //窗内绕身花粉飘旋（个人读数）
            if (Main.rand.NextBool(5)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Sparkle>(player.Center + ang.ToRotationVector2() * Main.rand.NextFloat(16f, 30f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    OrichalcumBright, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(OrichalcumMain, 20, 0.1f, 0.7f);
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

            //窗内命中：肩后迸出两片刃瓣
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 3 }, player.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                int petalDamage = Math.Clamp((int)(damageDone * 0.25f), 8, 120);
                for (int i = 0; i < 2; i++) {
                    float side = i == 0 ? 1f : -1f;
                    Vector2 from = player.Center + new Vector2(-player.direction * 14f, -20f + side * 8f);
                    Vector2 vel = new Vector2(-player.direction * Main.rand.NextFloat(2f, 3.5f),
                        -Main.rand.NextFloat(3f, 5f) * (side > 0 ? 1f : 0.6f));
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithOrichalcumEndow"),
                        from, vel, ModContent.ProjectileType<GsOrichalcumPetalBladeProj>(),
                        petalDamage, 1f, player.whoAmI, 0f, target.whoAmI, side);
                }
            }
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            //满层：绽入乱舞窗
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount + BloomDuration;
            state.EndowCharge = PetalBudget;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.3f }, player.Center);
            //开窗演出：花粉环炸
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                PRTLoader.NewParticle<PRT_Sparkle>(player.Center, ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f),
                    i % 2 == 0 ? OrichalcumBright : OrichalcumMain, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(OrichalcumMain, Main.rand.Next(18, 28), 0.15f, 0.8f);
            }
        }
    }

    /// <summary>
    /// 粉晶刃瓣：一片旋切的山铜晶瓣，出手甩离后螺旋咬向点名目标；
    /// 瓣体自旋 + 三层粉晶叠色 + 白晶芯，命中碎为晶屑
    /// </summary>
    internal class GsOrichalcumPetalBladeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>螺旋方向（±1）</summary>
        private ref float SpiralSide => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.7793f % 3.53f;

        /// <summary>出手散开帧数，之后开始咬向</summary>
        private const int ScatterFrames = 10;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 4f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //瓣体自旋
            Projectile.rotation += 0.34f * SpiralSide;

            if (Life > ScatterFrames) {
                NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                if (target != null && target.active && target.CanBeChasedBy(Projectile)) {
                    //螺旋咬向：追踪向量叠加垂直摆动
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 sway = dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Life * 0.3f + Seed * 4f) * 2.4f * SpiralSide;
                    Vector2 want = dir * 12.5f + sway;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.07f, 0.2f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.95f;
                }
            }

            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                    -Projectile.velocity * 0.1f, GsOrichalcumArmor.OrichalcumBright,
                    Main.rand.NextFloat(0.3f, 0.45f))
                    ?.Configure(GsOrichalcumArmor.OrichalcumMain, Main.rand.Next(10, 16), 0.1f, 0.6f);
            }
            Lighting.AddLight(Projectile.Center, GsOrichalcumArmor.OrichalcumMain.ToVector3() * (0.2f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //晶瓣碎裂
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsOrichalcumArmor.OrichalcumBright : GsOrichalcumArmor.OrichalcumMain,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：三层粉晶瓣 + 白晶芯 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crescent.Size() * 0.5f;
            float rotation = Projectile.rotation;
            //瓣面呼吸（identity 种子）
            float breathe = 1f + MathF.Sin(Life * 0.2f + Seed * 5f) * 0.06f;

            //深晶衬底
            Main.EntitySpriteDraw(crescent, pos, null,
                GsOrichalcumArmor.OrichalcumDeep * (0.7f * fade), rotation, origin,
                new Vector2(0.17f, 0.13f) * breathe, SpriteEffects.None, 0);
            //粉晶主瓣
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsOrichalcumArmor.OrichalcumMain with { A = 0 }) * fade, rotation, origin,
                new Vector2(0.14f, 0.10f) * breathe, SpriteEffects.None, 0);
            //白晶芯
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsOrichalcumArmor.OrichalcumBright with { A = 0 }) * (0.85f * fade), rotation, origin,
                new Vector2(0.09f, 0.05f) * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
