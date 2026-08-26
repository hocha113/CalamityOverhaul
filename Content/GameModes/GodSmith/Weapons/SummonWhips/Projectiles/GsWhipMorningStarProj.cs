using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 晨星处决「星坠锤」：目标头顶显形蓄势后重力砸落（主段 2.0x），
    /// 落点炸 120px 贴地震波（ai[1] 传 0.6x 二段）+ 距离衰减屏震。<br/>
    /// 锤体 = 原版晨星鞭弹幕贴图的鞭梢段放大 + 下坠自旋 + 速度拖影。
    /// ai[0] = 目标 npc.whoAmI
    /// </summary>
    internal class GsWhipMorningStarFallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal static readonly Color StarBright = new(255, 246, 214);
        internal static readonly Color StarMain = new(255, 226, 150);
        internal static readonly Color StarDeep = new(150, 104, 46);

        private const int RevealFrames = 8;
        private const int QuakeWindow = 5;
        private const int LifeFrames = 64;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>0 显形蓄势 / 1 下坠 / 2 落点震波</summary>
        private int phase;
        private int quakeTimer;
        private float lastTargetY;
        private float spin;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage() {
            if (phase == 1) {
                return null;             //锤体直击
            }
            return phase == 2 && quakeTimer < QuakeWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (phase == 1) {
                return targetHitbox.Intersects(projHitbox);
            }
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(240f)));
        }

        public override void AI() {
            NPC target = null;
            int idx = (int)Projectile.ai[0];
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                target = Main.npc[idx];
                lastTargetY = target.Center.Y;
            }
            switch (phase) {
                case 0:
                    //显形蓄势：微上浮聚光，跟住目标横位
                    Projectile.velocity = -Vector2.UnitY * 0.6f;
                    if (target != null) {
                        Projectile.Center = new Vector2(
                            MathHelper.Lerp(Projectile.Center.X, target.Center.X, 0.2f),
                            Projectile.Center.Y);
                    }
                    if (Elapsed >= RevealFrames) {
                        phase = 1;
                        Projectile.velocity = Vector2.UnitY * 5f;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.6f, Pitch = -0.5f }, Projectile.Center);
                        }
                    }
                    break;
                case 1:
                    //下坠：重力加速 + 横向轻追 + 自旋加快
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 1.5f, 27f);
                    if (target != null) {
                        float dx = target.Center.X - Projectile.Center.X;
                        Projectile.velocity.X = MathHelper.Clamp(dx * 0.1f, -6f, 6f);
                    }
                    spin += 0.06f;
                    //砸到目标身位即转震波（位置判据，各端一致）
                    if (Projectile.Center.Y >= lastTargetY - 18f) {
                        phase = 2;
                        quakeTimer = 0;
                        Projectile.velocity = Vector2.Zero;
                        Projectile.damage = Math.Max(1, (int)Projectile.ai[1]);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.35f }, Projectile.Center);
                            SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
                            //距离衰减屏震：各客户端按自己与落点的距离结算
                            float dist = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
                            float shake = MathHelper.Lerp(2f, 0f, MathHelper.Clamp(dist / 900f, 0f, 1f));
                            if (shake > 0.1f) {
                                Main.LocalPlayer.CWR()?.GetScreenShake(shake);
                            }
                            for (int i = 0; i < 12; i++) {
                                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                                    Main.rand.NextVector2Circular(8f, 4f) - Vector2.UnitY * Main.rand.NextFloat(1f, 4f),
                                    i % 3 == 0 ? StarBright : StarMain,
                                    Main.rand.NextFloat(0.35f, 0.62f))?.Configure(true, Main.rand.Next(15, 26));
                            }
                            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, StarMain, 0.24f)
                                ?.Configure(12, 0.9f);
                        }
                    }
                    break;
                default:
                    quakeTimer++;
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.MaceWhip);
            Texture2D whipTex = TextureAssets.Projectile[ProjectileID.MaceWhip].Value;
            Texture2D flash = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Flashimpact2")?.Value;
            if (flash == null) {
                return false;
            }
            //原版鞭贴图竖排五段，最底段即链锤头
            int segH = whipTex.Height / 5;
            Rectangle tipFrame = new(0, whipTex.Height - segH, whipTex.Width, segH);
            Vector2 tipOrigin = tipFrame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            const float MaceScale = 2.6f;
            if (phase == 0) {
                //显形：锤体从透明拧入 + 顶光渐亮
                float g = Elapsed / (float)RevealFrames;
                Main.EntitySpriteDraw(flash, pos, null, StarMain with { A = 0 } * (0.35f * g),
                    Projectile.identity * 0.5f, flash.Size() * 0.5f, 0.2f * g + 0.05f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(whipTex, pos, tipFrame, Color.White * g, spin,
                    tipOrigin, MaceScale * (0.7f + 0.3f * g), SpriteEffects.None, 0);
                return false;
            }
            if (phase == 1) {
                //下坠：速度拖影三重 + 本体 + 金晕
                for (int i = 3; i >= 1; i--) {
                    Main.EntitySpriteDraw(whipTex, pos - Projectile.velocity * (i * 0.6f), tipFrame,
                        StarMain with { A = 0 } * (0.42f - i * 0.11f), spin - i * 0.1f,
                        tipOrigin, MaceScale, SpriteEffects.None, 0);
                }
                Main.EntitySpriteDraw(whipTex, pos, tipFrame, Color.White, spin,
                    tipOrigin, MaceScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(whipTex, pos, tipFrame, StarBright with { A = 0 } * 0.5f, spin,
                    tipOrigin, MaceScale * 1.12f, SpriteEffects.None, 0);
                return false;
            }
            //落点震波：贴地椭圆震环 + 落锤余像 + 闪光衰减
            float t = MathHelper.Clamp(quakeTimer / (float)(LifeFrames - RevealFrames), 0f, 1f);
            float fade = 1f - t;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                MathHelper.Lerp(20f, 126f, 1f - fade * fade), 13f,
                StarBright, StarMain, StarDeep, fade,
                squish: 0.4f, innerGlow: 0.2f, timeSeed: Projectile.identity * 0.37f);
            Main.EntitySpriteDraw(whipTex, pos, tipFrame, StarMain with { A = 0 } * (0.55f * fade),
                spin, tipOrigin, MaceScale, SpriteEffects.None, 0);
            if (fade > 0.35f) {
                Main.EntitySpriteDraw(flash, pos, null, StarBright with { A = 0 } * fade,
                    -Projectile.identity * 0.3f, flash.Size() * 0.5f, 0.5f * fade, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 晨星「抡锤蓄势」震荡：踩拍挥击的鞭梢落点 80px 贴地震波（0.5x），
    /// 生成位置由方案找地后传入
    /// </summary>
    internal class GsWhipMaceQuakeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 14;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 160;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= 2 && Elapsed < 7 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(160f, 90f)));

        public override void AI() {
            if (Elapsed != 2 || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            float dist = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
            float shake = MathHelper.Lerp(1.2f, 0f, MathHelper.Clamp(dist / 700f, 0f, 1f));
            if (shake > 0.1f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(shake);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f),
                    -Vector2.UnitY * Main.rand.NextFloat(1.5f, 3.5f),
                    GsWhipMorningStarFallProj.StarMain,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = Elapsed / (float)LifeFrames;
            float fade = 1f - t;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                MathHelper.Lerp(12f, 84f, 1f - fade * fade), 10f,
                GsWhipMorningStarFallProj.StarBright,
                GsWhipMorningStarFallProj.StarMain,
                GsWhipMorningStarFallProj.StarDeep, fade * 0.9f,
                squish: 0.4f, innerGlow: 0.15f, timeSeed: Projectile.identity * 0.41f);
            return false;
        }
    }
}
