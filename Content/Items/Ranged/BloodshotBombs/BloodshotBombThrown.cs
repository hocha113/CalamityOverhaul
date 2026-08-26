using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.BloodshotBombs
{
    /// <summary>
    /// 掷出的泣血瞳雷弹体
    /// ai[0]=档位(0-2)，ai[1]=出手时引线进度(0-1，决定帧面与红度)，ai[2]=1 为手中即爆模式
    /// 触物即炸:命中敌人时血雾爆炸并迸出血肉碎块，砸在地上只溅血雾
    /// </summary>
    internal class BloodshotBombThrown : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "BloodshotBombProj";

        private int Tier => Math.Clamp((int)Projectile.ai[0], 0, 2);
        private float FuseProgress => Projectile.ai[1];
        private bool InstantBoom => Projectile.ai[2] > 0.5f;
        /// <summary>是否咬中了血肉，只在持有端结算，血肉碎块据此迸出</summary>
        private bool hitFlesh;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private int GetFuseFrame() => Math.Clamp((int)(FuseProgress * 4f), 0, 3);

        private Vector2 GetFuseTipWorld() {
            Vector2 off = BloodshotBombHeld.FuseTipOffset[GetFuseFrame()];
            return Projectile.Center + off.RotatedBy(Projectile.rotation);
        }

        public override void AI() {
            if (InstantBoom) {
                Projectile.Kill();
                return;
            }

            //旋转下坠，肉球的重量感
            Projectile.rotation += 0.26f * Math.Sign(Projectile.velocity.X);
            if (++Projectile.localAI[0] > 16) {
                Projectile.velocity.X *= 0.995f;
                Projectile.velocity.Y += 0.24f;
                if (Projectile.velocity.Y > 16f) {
                    Projectile.velocity.Y = 16f;
                }
            }

            float redness = FuseProgress;
            Lighting.AddLight(Projectile.Center, 0.3f + redness * 0.5f
                , 0.18f * (1f - redness), 0.08f * (1f - redness));

            //烧点火星拖尾
            if (Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(GetFuseTipWorld(), -Projectile.velocity * 0.12f
                    , Color.Lerp(new Color(255, 190, 80), new Color(255, 90, 40), Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, 12);
            }

            //三档弹体一路滴血
            if (Tier >= 2 && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, Projectile.velocity * 0.2f
                    , new Color(190, 25, 35), Main.rand.NextFloat(0.7f, 1f))?.Configure(30);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hitFlesh = true;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.8f }, Projectile.position);
            if (Tier >= 1) {
                target.AddBuff(BuffID.Bleeding, 120 + Tier * 120);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            int tier = Tier;
            bool fleshBurst = hitFlesh || InstantBoom;

            //爆响随档位加深，叠一层血肉溅裂
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.7f + tier * 0.15f,
                Pitch = 0.2f - tier * 0.22f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.55f + tier * 0.15f,
                Pitch = -0.1f
            }, Projectile.Center);

            if (tier >= 1 && CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, Vector2.UnitY, 2f + tier * 2f, 5f, 8 + tier * 4, 900f, FullName));
            }

            SpawnBloodMist(tier);

            //范围血雾伤害
            Projectile.Explode(BloodshotBomb.TierBlastRadius[tier], spanSound: false);

            //血肉横飞:只有咬中血肉(或手中炸开)才迸出碎块
            if (fleshBurst && Projectile.IsOwnedByLocalPlayer()) {
                int count = BloodshotBomb.TierChunkCount[tier];
                int chunkDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
                for (int i = 0; i < count; i++) {
                    Vector2 vel = VaultUtils.RandVr(3.5f, 8.5f);
                    vel.Y -= Main.rand.NextFloat(0f, 2.5f);//略向上抛，让碎块砸出弧线
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<BloodshotFleshChunk>(), chunkDamage
                        , Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
        }

        /// <summary>血雾爆炸的演出:红雾、血珠、脉冲环、原版血尘，全部随档位放大</summary>
        private void SpawnBloodMist(int tier) {
            float radius = BloodshotBomb.TierBlastRadius[tier];

            //腾起的血雾
            int mistCount = 8 + tier * 6;
            for (int i = 0; i < mistCount; i++) {
                Vector2 vel = VaultUtils.RandVr(1.2f, 3.2f + tier * 1.2f);
                vel.Y -= 0.6f;
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + VaultUtils.RandVr(radius * 0.3f), vel
                    , Color.Lerp(new Color(175, 26, 32), new Color(92, 9, 14), Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.28f, 0.4f) + tier * 0.12f)
                    ?.Configure(Main.rand.Next(26, 40), 0.55f, Main.rand.NextFloat(-0.03f, 0.03f));
            }

            //四溅的血珠
            int dropCount = 12 + tier * 8;
            for (int i = 0; i < dropCount; i++) {
                Vector2 vel = VaultUtils.RandVr(2.5f, 8f + tier * 2f);
                vel.Y -= Main.rand.NextFloat(0f, 2f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel
                    , Color.Lerp(new Color(205, 30, 40), new Color(140, 12, 22), Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.8f, 1.35f))?.Configure(Main.rand.Next(26, 46));
            }

            //冲击脉冲环
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero
                , new Color(255, 65, 55), 1f)?.Configure(0.18f, 0.55f + tier * 0.35f, 13 + tier * 3);

            //原版血尘打底
            int dustCount = 20 + tier * 14;
            for (int i = 0; i < dustCount; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + VaultUtils.RandVr(radius * 0.25f)
                    , DustID.Blood, VaultUtils.RandVr(1.5f, 6f + tier * 2f), 0, default
                    , Main.rand.NextFloat(1.2f, 1.9f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (InstantBoom) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = tex.Height / Main.projFrames[Projectile.type];
            Rectangle rect = new Rectangle(0, GetFuseFrame() * frameHeight, tex.Width, frameHeight);
            Vector2 origin = rect.Size() / 2f;
            float redness = FuseProgress;

            //残影拖尾，档位越高越红越亮
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                float fade = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f;
                Color trailColor = new Color(200, 40 + (int)(60 * (1f - redness)), 40) * (fade * 0.4f);
                Main.EntitySpriteDraw(tex, drawPos, rect, trailColor, Projectile.oldRot[k]
                    , origin, Projectile.scale * fade, SpriteEffects.None);
            }

            //充血辉光垫底(A=0 加色)
            if (redness > 0.05f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null
                    , new Color(255, 28, 18, 0) * (0.25f + redness * 0.5f), 0f, glow.Size() / 2f
                    , 0.8f + redness * 0.4f, SpriteEffects.None, 0);
            }

            Color body = lightColor;
            if (redness > 0f) {
                body.R = (byte)Math.Max(body.R, (int)(110 + 145 * redness));
                body.G = (byte)(body.G * (1f - 0.66f * redness));
                body.B = (byte)(body.B * (1f - 0.72f * redness));
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, rect, body
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }

    /// <summary>
    /// 血肉横飞的碎块:受重力翻滚，可在地面弹跳两次，沿途滴血
    /// </summary>
    internal class BloodshotFleshChunk : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "BloodshotFleshChunk";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity.X *= 0.99f;
            Projectile.velocity.Y += 0.32f;
            if (Projectile.velocity.Y > 15f) {
                Projectile.velocity.Y = 15f;
            }
            Projectile.rotation += 0.2f * Math.Sign(Projectile.velocity.X)
                + Projectile.velocity.X * 0.012f;

            if (Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood
                    , -Projectile.velocity * 0.1f, 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 120);
            for (int i = 0; i < 4; i++) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Blood, VaultUtils.RandVr(1f, 3f));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //至多弹跳两次，弹起时溅一点血
            if (++Projectile.localAI[1] > 2) {
                return true;
            }
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.55f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                if (Math.Abs(oldVelocity.Y) < 2f) {
                    return true;//已经滚不动了，直接碎开
                }
                Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.25f, Pitch = 0.35f }, Projectile.position);
            for (int i = 0; i < 3; i++) {
                Dust.NewDustPerfect(Projectile.Bottom, DustID.Blood, VaultUtils.RandVr(0.8f, 2.2f));
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.position);
            for (int i = 0; i < 6; i++) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Blood, VaultUtils.RandVr(1.5f, 4f)
                    , 0, default, Main.rand.NextFloat(1f, 1.5f));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, VaultUtils.RandVr(1f, 3f)
                    , new Color(170, 18, 26), Main.rand.NextFloat(0.7f, 1f))?.Configure(26);
            }
        }
    }
}
