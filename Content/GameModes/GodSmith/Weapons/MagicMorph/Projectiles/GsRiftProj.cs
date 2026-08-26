using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 天空裂痕 B 形态「裂口」：品字形预告裂隙，0.5s 后向汇聚点各射一剑。<br/>
    /// ai[0]/ai[1]=汇聚点世界坐标（随生成包过线）；本体无伤纯预告，
    /// 剑由 owner 端在裂口消亡时生成（Parent 源承签，档位随父标传染）
    /// </summary>
    internal class GsRiftProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTicks = 30;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        private Vector2 Focus => new(Projectile.ai[0], Projectile.ai[1]);

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //裂隙渐开：碎裂粒子沿裂口朝向汇聚点的轴线聚拢
            if (Projectile.timeLeft % 3 == 0) {
                Vector2 axis = (Focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_SpaceFracture>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    axis * Main.rand.NextFloat(0.4f, 1.2f),
                    new Color(150, 220, 255), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(12, 20), 0.1f);
            }
            Lighting.AddLight(Projectile.Center, 0.16f, 0.25f, 0.3f);
        }

        public override void OnKill(int timeLeft) {
            //剑只由 owner 端生成（Parent 源，父标已带 B 档，承签自动传染）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vel = (Focus - Projectile.Center).SafeNormalize(Vector2.UnitX) * 16f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    ProjectileID.SkyFracture, Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            //裂口窄缝：朝汇聚点的细长冷光，开合曲线随寿命（timeLeft 确定函数）
            float t = 1f - Projectile.timeLeft / (float)LifeTicks;
            float open = MathF.Sin(t * MathHelper.Pi);
            float rot = (Focus - Projectile.Center).ToRotation();
            Color slit = new Color(150, 220, 255) * (0.75f * open);
            slit.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, slit, rot,
                glow.Size() / 2f, new Vector2(1.15f, 0.14f + 0.1f * open),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Color core = Color.White * (0.5f * open);
            core.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, core, rot,
                glow.Size() / 2f, new Vector2(0.8f, 0.08f),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}
