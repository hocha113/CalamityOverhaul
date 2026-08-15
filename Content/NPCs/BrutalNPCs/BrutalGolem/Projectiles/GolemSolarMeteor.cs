using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>大招陨星：高空滞留 → 垂直坠落 → 触地爆散余烬
    /// ai[0]=坠落延迟, ai[1]=预期落点Y</summary>
    internal class GolemSolarMeteor : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        private int Delay => (int)Math.Max(Projectile.ai[0], 1f);
        private bool Falling => Projectile.localAI[0] >= Delay;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            if (!Falling) {
                //滞留期：微光呼吸，客户端看得到高空的备弹
                Projectile.velocity = Vector2.Zero;
                Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.28f, 0.09f));
                return;
            }

            //坠落：复合加速
            if (Projectile.localAI[0] == Delay + 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.15f, Volume = 0.6f }, Projectile.Center);
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 1.6f, 34f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.55f, 0.18f));

            //越过预期落点高度后启用碰撞，防止在空中提前撞浮岛
            if (Projectile.Center.Y > Projectile.ai[1] - 420f) {
                Projectile.tileCollide = true;
            }
            //落点兜底
            if (Projectile.Center.Y > Projectile.ai[1] + 60f) {
                Projectile.Kill();
            }

            if (!Main.dedServ) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.SolarFlare, -Projectile.velocity * 0.12f, 0, default, 1.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.25f, Volume = 1f }, Projectile.Center);
                Core.GolemScreenEffects.Shake(4f);
                Core.GolemScreenEffects.PushShockRing(Projectile.Center, 0.7f, 420f, 20);
                for (int i = 0; i < 18; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                        Main.rand.NextVector2Circular(5f, 4f) - Vector2.UnitY * 2f, 0, default, 1.8f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center,
                        Main.rand.NextVector2Circular(3.5f, 2f) - Vector2.UnitY * 3f,
                        new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.1f)).Configure(40);
                }
            }

            //触地爆散余烬（服务端）
            if (VaultUtils.isClient) {
                return;
            }
            int embers = 3;
            int damage = (int)(Projectile.damage * 0.7f);
            for (int i = 0; i < embers; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3.6f, 3.6f), Main.rand.NextFloat(-7.5f, -4.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center - Vector2.UnitY * 12f, vel,
                    ModContent.ProjectileType<GolemSunMortar>(), damage, 0f, Main.myPlayer, 1f, 0f);
            }
        }

        public override bool? CanDamage() => Falling ? null : false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D streak = CWRAsset.LightShot.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (!Falling) {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI);
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 170, 60, 0) * (0.7f * pulse),
                    0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0);
                return false;
            }

            //坠落拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, oldPos, null, new Color(200, 90, 25, 0) * (0.4f * fade),
                    0f, glow.Size() / 2f, 0.65f * fade, SpriteEffects.None, 0);
            }
            //流星体：竖直拉伸热核
            Main.EntitySpriteDraw(streak, drawPos, null, new Color(255, 160, 55, 0),
                Projectile.rotation - MathHelper.PiOver2, new Vector2(streak.Width * 0.8f, streak.Height / 2f),
                new Vector2(0.65f, 0.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 235, 180, 0),
                0f, glow.Size() / 2f, 0.55f, SpriteEffects.None, 0);
            return false;
        }
    }
}
