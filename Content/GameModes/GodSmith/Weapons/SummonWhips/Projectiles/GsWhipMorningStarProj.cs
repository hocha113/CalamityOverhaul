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
    /// 锤体 = 原版晨星鞭弹幕贴图的鞭梢段放大 + 下坠自旋。
    /// ai[0] = 目标 npc.whoAmI
    /// </summary>
    internal class GsWhipMorningStarFallProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MaceWhip;

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
                        }
                    }
                    break;
                default:
                    quakeTimer++;
                    break;
            }
        }

        /// <summary>锤体一笔：原版鞭贴图竖排五段，最底段即链锤头；显形期渐显，落地后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D whipTex = TextureAssets.Projectile[Type].Value;
            int segH = whipTex.Height / 5;
            Rectangle tipFrame = new(0, whipTex.Height - segH, whipTex.Width, segH);
            const float MaceScale = 2.6f;
            float alpha = phase switch {
                0 => Elapsed / (float)RevealFrames,
                1 => 1f,
                _ => 1f - MathHelper.Clamp(quakeTimer / (float)(LifeFrames - RevealFrames), 0f, 1f),
            };
            if (alpha <= 0.01f) {
                return false;
            }
            Main.EntitySpriteDraw(whipTex, Projectile.Center - Main.screenPosition, tipFrame, lightColor * alpha, spin,
                tipFrame.Size() * 0.5f, MaceScale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 晨星「抡锤蓄势」震荡：踩拍挥击的鞭梢落点 80px 贴地震波（0.5x），
    /// 生成位置由方案找地后传入
    /// </summary>
    internal class GsWhipMaceQuakeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

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
        }

        /// <summary>范围提示：原版气泡贴图按判定框（宽 160 高 90）拉伸画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - Elapsed / (float)LifeFrames;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, new Vector2(Projectile.width / (float)tex.Width, Projectile.height / (float)tex.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
