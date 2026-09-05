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
    /// 暗黑收割处决「大镰收魂」：短暂聚拢后紫魂自目标涌出（主段 1.6x），
    /// 随即魂爆二段（ai[0] 传 0.6x）
    /// </summary>
    internal class GsWhipReapSoulProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DemonSickle;

        private const int GatherFrames = 6;
        private const int SoulWindow = 4;
        private const int BurstAt = 12;
        private const int BurstWindow = 4;
        private const int LifeFrames = 30;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage() {
            int elapsed = Elapsed;
            if (elapsed >= GatherFrames && elapsed < GatherFrames + SoulWindow) {
                return null;
            }
            return elapsed >= BurstAt && elapsed < BurstAt + BurstWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = Elapsed < BurstAt ? 100f : 70f;
            return targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(r * 2f)));
        }

        public override void AI() {
            int elapsed = Elapsed;
            if (elapsed == BurstAt) {
                Projectile.damage = Math.Max(1, (int)Projectile.ai[0]);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.9f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.55f, Pitch = 0.1f }, Projectile.Center);
            }
            if (elapsed == BurstAt) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版恶魔镰刀贴图按当前判定半径缩放画一笔（lightColor 着色），随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            if (elapsed < GatherFrames) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp((elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float r = elapsed < BurstAt ? 100f : 70f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, r * 2f / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 暗黑收割「闪劈」：瞬至目标位置的一道短镰（处决全场补劈与
    /// 跳劈传染共用，伤害由生成方折算）。ai[0] = 目标 npc.whoAmI，全程贴身
    /// </summary>
    internal class GsWhipReapFlashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DemonSickle;

        private const int LifeFrames = 16;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= 3 && Elapsed < 8 ? null : false;

        public override void AI() {
            int idx = (int)Projectile.ai[0];
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                Projectile.Center = Main.npc[idx].Center;
            }
            if (Elapsed == 3 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版恶魔镰刀贴图按判定框缩放画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - Elapsed / (float)LifeFrames;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, Projectile.width / (float)tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
