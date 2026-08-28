using CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【黑曜石套·曜刃回旋】（P10a 移交，键族归 ArmorsB）水火相激的玻璃锋芒：
    /// ①命中积攒曜热，满七层后下一击掷出上下两道曜玻刃 ②刃走 V 形回旋：去程减速、
    /// 折返加速归主，沿途穿斩 ③刃面熔芯透亮，命中挂燃、碎玻迸溅。原版套装奖励（鞭系强化）保留，神赋叠加
    /// </summary>
    internal class GsObsidianArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.ObsidianHelm];

        public override int BodyID => ItemID.ObsidianShirt;

        public override int LegsID => ItemID.ObsidianPants;

        protected override string EndowLineFallback =>
            "Glassfang Return: strikes build obsidian heat; at 7 stacks the next strike hurls two volcanic glass fangs that sweep out and boomerang home, searing all in their path";

        //黑曜玻璃色板：暗玻璃 + 熔橙芯
        internal static readonly Color GlassShine = new(222, 202, 255);
        internal static readonly Color GlassPurple = new(128, 82, 198);
        internal static readonly Color GlassDark = new(42, 30, 58);
        internal static readonly Color LavaOrange = new(255, 122, 42);

        protected override int FullCharge => 7;

        protected override Color ThemeMain => GlassPurple;

        protected override Color ThemeBright => LavaOrange;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsObsidianGlassFangProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = -0.35f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, Pitch = 0.2f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int fangDamage = Math.Clamp((int)(damageDone * 0.35f), 7, 75);
            Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.UnitX);
            //上下两道 V 形张开
            for (int i = -1; i <= 1; i += 2) {
                Vector2 vel = dir.RotatedBy(i * 0.42f) * 13f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithObsidianEndow"),
                    player.Center + vel.SafeNormalize(Vector2.UnitX) * 16f, vel,
                    ModContent.ProjectileType<GsObsidianGlassFangProj>(),
                    fangDamage, 2.5f, player.whoAmI, 0f, 0f, i);
            }
        }
    }

    /// <summary>
    /// 曜玻刃：一片火山玻璃锻成的回旋弯刃，去程减速、驻点一顿、折返加速归主；
    /// 暗玻璃刃身 + 熔橙内芯透光 + 高速自旋，命中挂燃并迸碎玻
    /// </summary>
    internal class GsObsidianGlassFangProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>V 形侧别（±1，定自旋向）</summary>
        private ref float SideSign => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.8887f % 3.93f;

        /// <summary>去程帧数</summary>
        private const int OutFrames = 22;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Life <= OutFrames) {
                //去程：渐缓，尾端近乎驻点
                Projectile.velocity *= 0.93f;
            }
            else {
                //折返：加速归主，近主即收
                Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    * MathHelper.Clamp(6f + (Life - OutFrames) * 0.5f, 6f, 21f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.15f);
                if (Projectile.Center.Distance(owner.Center) < 26f) {
                    Projectile.Kill();
                    return;
                }
            }
            //玻璃刃高速自旋
            Projectile.rotation += 0.5f * SideSign;

            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? GsObsidianArmor.LavaOrange : GsObsidianArmor.GlassPurple,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(8, 13));
            }
            Lighting.AddLight(Projectile.Center, GsObsidianArmor.LavaOrange.ToVector3() * (0.2f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 150);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 4 }, target.Center);
            //碎玻迸溅
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsObsidianArmor.GlassShine : GsObsidianArmor.GlassPurple,
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //归主收刃：一点熔光
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsObsidianArmor.LavaOrange, 0.09f)?.Configure(7, 0.7f);
        }

        //==================== 绘制：暗玻璃弯刃 + 熔芯透光 + 回旋残影 ====================

        private void DrawFang(Vector2 pos, float rotation, float alpha) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return;
            }
            Vector2 origin = crescent.Size() * 0.5f;
            //暗玻璃刃身（真 alpha 占体积）
            Main.EntitySpriteDraw(crescent, pos, null,
                GsObsidianArmor.GlassDark * (0.95f * alpha), rotation, origin,
                new Vector2(0.15f, 0.105f), SpriteEffects.None, 0);
            //紫玻璃面
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsObsidianArmor.GlassPurple with { A = 0 }) * (0.8f * alpha), rotation, origin,
                new Vector2(0.12f, 0.075f), SpriteEffects.None, 0);
            //熔橙内芯（玻璃里的火）
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsObsidianArmor.LavaOrange with { A = 0 }) * (0.75f * alpha), rotation, origin,
                new Vector2(0.085f, 0.04f), SpriteEffects.None, 0);
            //玻璃高光丝
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsObsidianArmor.GlassShine with { A = 0 }) * (0.5f * alpha), rotation, origin,
                new Vector2(0.06f, 0.018f), SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            //回旋残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                DrawFang(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    Projectile.rotation - i * 0.5f * SideSign, ghost);
            }
            DrawFang(Projectile.Center - Main.screenPosition, Projectile.rotation, fade);
            //熔芯呼吸光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float heat = 0.6f + MathF.Sin(Life * 0.3f + Seed * 5f) * 0.25f;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                    (GsObsidianArmor.LavaOrange with { A = 0 }) * (0.35f * heat * fade), 0f, glow.Size() * 0.5f,
                    0.3f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
