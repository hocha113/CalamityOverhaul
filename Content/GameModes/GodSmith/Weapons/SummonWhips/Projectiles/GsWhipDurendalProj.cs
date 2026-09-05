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
    /// 杜兰达尔处决「圣剑审判」：目标上方预告后巨剑自天落斩（主段 1.8x），
    /// 插地迸出金环（ai[1] 传 0.6x 二段）。剑形 = 原版杜兰达尔物品贴图放大投影。<br/>
    /// ai[0] = 目标 npc.whoAmI；相位由位置判据推进，各端确定性一致
    /// </summary>
    internal class GsWhipDurendalVerdictProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SwordWhip;

        private const int TelegraphFrames = 10;
        private const int BurstWindow = 4;
        private const int LifeFrames = 52;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>0 预告 / 1 落斩 / 2 落地迸发</summary>
        private int phase;
        private int burstTimer;
        private float lastTargetY;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 82;
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
                return null;             //落斩段：剑体判定
            }
            return phase == 2 && burstTimer < BurstWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (phase == 1) {
                return targetHitbox.Intersects(projHitbox);
            }
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(160f)));
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
                    //预告：悬停目标上方，光柱渐亮
                    if (target != null) {
                        Projectile.Center = new Vector2(target.Center.X, target.Center.Y - 210f);
                        lastTargetY = target.Center.Y;
                    }
                    if (Elapsed >= TelegraphFrames) {
                        phase = 1;
                        Projectile.velocity = Vector2.UnitY * 7f;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
                        }
                    }
                    break;
                case 1:
                    //落斩：重加速直坠，横向轻追目标
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 2.7f, 30f);
                    if (target != null) {
                        float dx = target.Center.X - Projectile.Center.X;
                        Projectile.velocity.X = MathHelper.Clamp(dx * 0.12f, -7f, 7f);
                    }
                    //越过目标身位即转迸发（位置判据，各端一致）
                    if (Projectile.Center.Y >= lastTargetY - 6f) {
                        phase = 2;
                        burstTimer = 0;
                        Projectile.velocity = Vector2.Zero;
                        Projectile.damage = Math.Max(1, (int)Projectile.ai[1]);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.1f }, Projectile.Center);
                            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
                        }
                    }
                    break;
                default:
                    burstTimer++;
                    break;
            }
        }

        /// <summary>剑体一笔：原版杜兰达尔物品贴图放大 1.6 倍、剑尖指下；预告期渐显，插地后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D sword = TextureAssets.Projectile[Type].Value;
            //剑尖指下：物品贴图刃朝右上，转 3/4 圈让锋芒向地
            const float SwordRot = MathHelper.Pi * 0.75f;
            float alpha = phase switch {
                0 => 0.25f + 0.75f * (Elapsed / (float)TelegraphFrames),
                1 => 1f,
                _ => 1f - MathHelper.Clamp(burstTimer / (float)(LifeFrames - TelegraphFrames), 0f, 1f),
            };
            if (alpha <= 0.01f) {
                return false;
            }
            Main.EntitySpriteDraw(sword, Projectile.Center - Main.screenPosition, null, lightColor * alpha, SwordRot,
                sword.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 杜兰达尔剑意「横断剑气」：踩拍蓄满后的鞭梢剑气波（0.9x，穿透 3），
    /// 贴图复用原版光束弹幕，飞行带微减速
    /// </summary>
    internal class GsWhipDurendalArcProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        private const int LifeFrames = 36;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity *= 0.985f;
            //原版光束贴图朝上，补四分之一圈对齐飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
