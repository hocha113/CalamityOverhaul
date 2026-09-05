using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 雷区殉爆弹：任一引爆后邻雷位置生成，15 帧引信起爆，链式传播封顶 4 层。<br/>
    /// ai[0]=链深（1 起）ai[1]=爆炸半径。完全独立于原版陷阱 AI 与冷却；
    /// 起爆帧由 owner 端继续传播（每座陷阱 90 帧至多参与一次，节流在 SentryGrid）
    /// </summary>
    internal class GsSentryChainBlastProj : ModProjectile
    {
        /// <summary>引信帧数</summary>
        private const int FuseFrames = 15;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Depth => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FuseFrames + 16;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>引信期无判定，起爆后 5 帧判定窗</summary>
        public override bool? CanDamage() => Age >= FuseFrames && Age <= FuseFrames + 5 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsSentryBurstProj.DistRectPoint(targetHitbox, Projectile.Center) <= Radius;

        public override void AI() {
            Age++;
            if (Age != FuseFrames) {
                return;
            }
            //起爆帧：音效（各端）+ 链式传播（owner）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.75f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
            }
            if (Projectile.IsOwnedByLocalPlayer() && Depth < 4f) {
                SentryGrid.PropagateChain(Projectile.Center, Projectile.owner,
                    (int)Depth + 1, Projectile.damage);
            }
        }

        /// <summary>范围提示：引信期按判定框大小画一笔原版贴图，起爆后按爆炸半径缩放并随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale;
            float fade = 1f;
            if (Age < FuseFrames) {
                scale = Projectile.width / (float)tex.Width;
            }
            else {
                scale = MathHelper.Max(Radius, 8f) * 2f / tex.Width;
                fade = 1f - MathHelper.Clamp((Age - FuseFrames) / 14f, 0f, 1f);
                if (fade <= 0.01f) {
                    return false;
                }
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
