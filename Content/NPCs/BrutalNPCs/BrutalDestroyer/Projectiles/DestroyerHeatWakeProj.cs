using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles
{
    /// <summary>热浪尾流，Warp扭曲；ai[0]头whoAmI；同头至多一条见EnsureForHead</summary>
    internal class DestroyerHeatWakeProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出现最低头速</summary>
        private const float MinSpeed = 20f;
        /// <summary>低速自毁帧</summary>
        private const int SlowKillTime = 40;

        private float intensity;
        private float headSpeed;
        private float headRotation;
        private Vector2 headCenter;
        private int slowTimer;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>服务端补头尾流</summary>
        internal static void EnsureForHead(NPC head) {
            if (VaultUtils.isClient || !head.Alives()) {
                return;
            }
            int type = ModContent.ProjectileType<DestroyerHeatWakeProj>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == head.whoAmI) {
                    return;
                }
            }
            Projectile.NewProjectile(head.GetSource_FromAI(), head.Center, Vector2.Zero,
                type, 0, 0f, Main.myPlayer, head.whoAmI);
        }

        public override void AI() {
            ((int)Projectile.ai[0]).TryGetNPC(out NPC head);
            if (!head.Alives() || head.type != NPCID.TheDestroyer) {
                Projectile.Kill();
                return;
            }

            headCenter = head.Center;
            headSpeed = head.velocity.Length();
            headRotation = head.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
            Projectile.Center = headCenter;

            //强度跟头速
            float target = MathHelper.Clamp((headSpeed - MinSpeed) / 34f, 0f, 1f);
            intensity = MathHelper.Lerp(intensity, target, 0.2f);

            if (target > 0.05f) {
                slowTimer = 0;
                Projectile.timeLeft = 90;
            }
            else if (++slowTimer > SlowKillTime) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => false;

        public bool DontUseBlueshiftEffect() => true;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        public void Warp() {
            if (intensity <= 0.05f) {
                return;
            }

            //尾流随速度拉长，前缘略超出头部
            float length = 340f + headSpeed * 6.2f;
            float width = 150f + headSpeed * 1.2f;
            Vector2 dir = headRotation.ToRotationVector2();
            Vector2 center = headCenter - dir * (length * 0.5f - 70f);

            DestroyerMotionFX.DrawHeatWakeWarp(center, length, width, headRotation,
                intensity * 0.55f, 1f);
        }
    }
}
