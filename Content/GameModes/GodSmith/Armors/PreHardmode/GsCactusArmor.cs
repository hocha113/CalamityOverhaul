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
    /// 【仙人掌套·针衣】（P10a 移交，键族归 ArmorsB）沙漠韧木的荆棘之衣：
    /// ①命中积攒棘刺，满六层披上针衣四秒 ②针衣贴身旋棘持续扎刺近身之敌
    /// ③疾跑时针衣沿途向身后甩针。原版无套装奖励，神赋即是它的第一件套装奖励
    /// </summary>
    internal class GsCactusArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.CactusHelmet];

        public override int BodyID => ItemID.CactusBreastplate;

        public override int LegsID => ItemID.CactusLeggings;

        protected override string EndowLineFallback =>
            "Needle Shroud: strikes build barbs; at 6 stacks don a 4s needle shroud that pricks foes on contact and flings needles behind you as you run";

        //仙人掌绿 + 针骨色板
        internal static readonly Color CactusBright = new(202, 240, 152);
        internal static readonly Color CactusGreen = new(120, 182, 82);
        internal static readonly Color CactusDeep = new(58, 102, 42);
        internal static readonly Color NeedleBone = new(232, 232, 202);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => CactusGreen;

        protected override Color ThemeBright => CactusBright;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsCactusNeedleCloakProj>()
            || proj.type == ModContent.ProjectileType<GsCactusCloakNeedleProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 18f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1f, 2.5f),
                        i % 2 == 0 ? CactusBright : CactusGreen, 0.35f)?.Configure(false, 14);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int prickDamage = Math.Clamp((int)(damageDone * 0.30f), 5, 60);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithCactusEndow"),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCactusNeedleCloakProj>(),
                prickDamage, 1f, player.whoAmI);
        }
    }

    /// <summary>
    /// 针衣：披在佩戴者身上的旋棘外衣，一圈骨白棘针绕身缓旋、贴身扎刺；
    /// 佩戴者疾跑时每 12 帧向身后甩出两根坠针
    /// </summary>
    internal class GsCactusNeedleCloakProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.6947f % 3.29f;

        /// <summary>针衣半径</summary>
        private const float Radius = 52f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 10f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 25;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2())
                < Radius + targetHitbox.Width * 0.3f;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.velocity = Vector2.Zero;

            //疾跑甩针（佩戴者端裁定）
            if (Projectile.owner == Main.myPlayer && owner.velocity.Length() > 3f && Life % 12 == 0) {
                for (int i = 0; i < 2; i++) {
                    Vector2 back = -owner.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(5f, 7f)
                        - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        owner.Center + Main.rand.NextVector2Circular(10f, 14f), vel,
                        ModContent.ProjectileType<GsCactusCloakNeedleProj>(),
                        Math.Max(4, Projectile.damage * 2 / 3), 1f, Projectile.owner);
                }
            }
            Lighting.AddLight(Projectile.Center, GsCactusArmor.CactusGreen.ToVector3() * (0.14f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    Main.rand.NextBool() ? GsCactusArmor.NeedleBone : GsCactusArmor.CactusGreen,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //谢衣：棘针簌簌散落
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.7f, Radius * 0.7f),
                    DustID.GrassBlades, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f)));
                d.scale = Main.rand.NextFloat(0.8f, 1.2f);
            }
        }

        //==================== 绘制：绕身旋棘一圈（逐针画，近针大远针小） ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (shot == null || core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //周身淡绿护层
            Main.EntitySpriteDraw(core, pos, null,
                (GsCactusArmor.CactusGreen with { A = 0 }) * (0.18f * fade), 0f, core.Size() * 0.5f,
                new Vector2(Radius * 2.2f / core.Width, Radius * 2.2f / core.Width * 0.9f), SpriteEffects.None, 0);
            //八根旋棘：椭圆轨道，前后针以纵向位置分大小造深度
            for (int i = 0; i < 8; i++) {
                float ang = Life * 0.045f + MathHelper.TwoPi * i / 8f + Seed;
                Vector2 at = pos + new Vector2(MathF.Cos(ang) * Radius, MathF.Sin(ang) * Radius * 0.6f);
                float depth = MathF.Sin(ang) * 0.5f + 0.5f;
                float needleScale = 0.55f + depth * 0.45f;
                float rot = ang + MathHelper.PiOver2;
                Main.EntitySpriteDraw(shot, at, null,
                    (GsCactusArmor.CactusDeep with { A = 0 }) * (0.7f * fade * needleScale), rot, shot.Size() * 0.5f,
                    new Vector2(0.10f, 0.03f) * needleScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(shot, at, null,
                    (GsCactusArmor.NeedleBone with { A = 0 }) * (0.85f * fade * needleScale), rot, shot.Size() * 0.5f,
                    new Vector2(0.08f, 0.018f) * needleScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 坠针：针衣沿途甩下的骨白棘针，带坠弧，落地即碎
    /// </summary>
    internal class GsCactusCloakNeedleProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity.Y += 0.22f;
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                    GsCactusArmor.NeedleBone, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 绘制：细针双层 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(shot, pos, null,
                (GsCactusArmor.CactusDeep with { A = 0 }) * (0.7f * fade), Projectile.rotation, shot.Size() * 0.5f,
                new Vector2(0.11f, 0.026f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, pos, null,
                (GsCactusArmor.NeedleBone with { A = 0 }) * fade, Projectile.rotation, shot.Size() * 0.5f,
                new Vector2(0.09f, 0.016f), SpriteEffects.None, 0);
            return false;
        }
    }
}
