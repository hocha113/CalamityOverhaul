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
    /// 杜兰达尔处决「圣剑审判」：光柱预告后金色巨剑自天落斩（主段 1.8x），
    /// 插地迸出金环（ai[1] 传 0.6x 二段）。剑形 = 原版杜兰达尔物品贴图放大投影。<br/>
    /// ai[0] = 目标 npc.whoAmI；相位由位置判据推进，各端确定性一致
    /// </summary>
    internal class GsWhipDurendalVerdictProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal static readonly Color HolyBright = new(255, 244, 208);
        internal static readonly Color HolyMain = new(255, 214, 120);
        internal static readonly Color HolyDeep = new(168, 116, 40);

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
                            for (int i = 0; i < 9; i++) {
                                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                                    Main.rand.NextVector2Circular(7f, 4.5f) - Vector2.UnitY * 2f,
                                    i % 3 == 0 ? HolyBright : HolyMain,
                                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 24));
                            }
                            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, HolyMain, 0.2f)
                                ?.Configure(12, 0.85f);
                        }
                    }
                    break;
                default:
                    burstTimer++;
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.SwordWhip);
            Texture2D sword = TextureAssets.Item[ItemID.SwordWhip].Value;
            Texture2D beam = CWRUtils.GetT2DAsset(CWRConstant.Masking + "LightBeam")?.Value;
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare01")?.Value;
            if (beam == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //剑尖指下：物品贴图刃朝右上，转 3/4 圈让锋芒向地
            const float SwordRot = MathHelper.Pi * 0.75f;
            if (phase == 0) {
                float g = Elapsed / (float)TelegraphFrames;
                //预告光柱：从剑位垂到目标身位
                float beamLen = MathF.Max(60f, lastTargetY - Projectile.Center.Y + 40f);
                Main.EntitySpriteDraw(beam, pos + new Vector2(0f, beamLen * 0.5f), null,
                    HolyMain with { A = 0 } * (0.4f * g), MathHelper.PiOver2,
                    beam.Size() * 0.5f, new Vector2(beamLen / beam.Width, 0.7f), SpriteEffects.None, 0);
                //剑形渐显微上提
                Main.EntitySpriteDraw(sword, pos - new Vector2(0f, 8f * (1f - g)), null,
                    HolyBright with { A = 128 } * (0.25f + 0.6f * g), SwordRot,
                    sword.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
                return false;
            }
            if (phase == 1) {
                //落斩：剑体全亮 + 速度拖影
                for (int i = 3; i >= 1; i--) {
                    Main.EntitySpriteDraw(sword, pos - Projectile.velocity * (i * 0.55f), null,
                        HolyMain with { A = 0 } * (0.4f - i * 0.1f), SwordRot,
                        sword.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
                }
                Main.EntitySpriteDraw(sword, pos, null, Color.White, SwordRot,
                    sword.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(sword, pos, null, HolyBright with { A = 0 } * 0.55f, SwordRot,
                    sword.Size() * 0.5f, 1.72f, SpriteEffects.None, 0);
                return false;
            }
            //迸发：金环扩张 + 剑身余像渐隐 + 中心闪
            float t = MathHelper.Clamp(burstTimer / (float)(LifeFrames - TelegraphFrames), 0f, 1f);
            float fade = 1f - t;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                MathHelper.Lerp(16f, 84f, 1f - fade * fade), 11f,
                HolyBright, HolyMain, HolyDeep, fade,
                squish: 0.55f, innerGlow: 0.25f, timeSeed: Projectile.identity * 0.29f);
            Main.EntitySpriteDraw(sword, pos, null, HolyMain with { A = 0 } * (0.5f * fade), SwordRot,
                sword.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
            if (fade > 0.4f) {
                Main.EntitySpriteDraw(flare, pos, null, HolyBright with { A = 0 } * fade,
                    Projectile.identity * 0.4f, flare.Size() * 0.5f, 0.42f * fade, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 杜兰达尔剑意「横断剑气」：踩拍蓄满后的鞭梢金色弧波（0.9x，穿透 3），
    /// 弧光贴图拉伸 + 渐隐，飞行带微减速
    /// </summary>
    internal class GsWhipDurendalArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Projectile.velocity * 0.06f,
                    GsWhipDurendalVerdictProj.HolyMain,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D arc = CWRUtils.GetT2DAsset(CWRConstant.Masking + "CrescentSoft01")?.Value;
            if (arc == null) {
                return false;
            }
            float t = 1f - Projectile.timeLeft / (float)LifeFrames;
            float fade = 1f - t * t;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //弧口朝前的双层金弧：亮缘略小，主体拉长
            Main.EntitySpriteDraw(arc, pos, null,
                GsWhipDurendalVerdictProj.HolyMain with { A = 0 } * (0.7f * fade),
                Projectile.rotation, arc.Size() * 0.5f, new Vector2(1.25f, 1.05f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(arc, pos, null,
                GsWhipDurendalVerdictProj.HolyBright with { A = 0 } * (0.85f * fade),
                Projectile.rotation, arc.Size() * 0.5f, new Vector2(1.05f, 0.85f), SpriteEffects.None, 0);
            return false;
        }
    }
}
