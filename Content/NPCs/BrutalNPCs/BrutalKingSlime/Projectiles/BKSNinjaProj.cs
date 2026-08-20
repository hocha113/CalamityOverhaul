using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 体内忍者的死亡演出逃逸(纯演出彩蛋)。ai[0]=宿主whoAmI ai[1]=3(历史招式号，其余样式已随影袭删除)<br/>
    /// 沿地奔逃，蹦跳两下，渐隐；无伤害无碰撞
    /// </summary>
    internal class BKSNinjaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int EscapeLife = 170;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EscapeLife + 20;
        }

        public override void AI() {
            //历史样式号防御：影袭样式(0-2)已删除，异常端直接消散
            if ((int)Projectile.ai[1] != 3) {
                Projectile.Kill();
                return;
            }
            UpdateEscape();
        }

        /// <summary>逃逸：贴地奔跑+偶尔跃步，末段渐隐；纯演出</summary>
        private void UpdateEscape() {
            Timer++;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 30);

            //重力+贴地
            Projectile.velocity.Y += 0.42f;
            if (Projectile.velocity.Y > 14f) {
                Projectile.velocity.Y = 14f;
            }
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(Projectile.Center, 8);
            bool onGround = Projectile.Center.Y + 16f >= ground.Y;
            if (onGround && Projectile.velocity.Y > 0f) {
                Projectile.Center = new Vector2(Projectile.Center.X, ground.Y - 16f);
                Projectile.velocity.Y = 0f;
                //每隔一段小跃步
                if ((int)Timer % 46 == 12) {
                    Projectile.velocity.Y = -6.5f;
                }
            }
            //保持奔逃横速(方向由生成时初速决定)
            float runDir = Projectile.velocity.X >= 0f ? 1f : -1f;
            Projectile.velocity.X = runDir * 9.5f;
            Projectile.rotation = Projectile.velocity.X * 0.02f;

            //奔逃尘土
            if (!VaultUtils.isServer && onGround && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Bottom - new Vector2(4f, 4f), 8, 4,
                    DustID.Smoke, 0, 0, 140, default, 0.8f);
                d.velocity = new Vector2(-runDir * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(0.2f, 0.8f));
            }

            if (Timer > EscapeLife - 40) {
                Projectile.alpha = (int)MathHelper.Clamp((Timer - (EscapeLife - 40)) / 40f * 255f, 0f, 255f);
            }
            if (Timer >= EscapeLife) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ninja = TextureAssets.Ninja.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = ninja.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;

            //本色小人贴地奔逃
            SpriteEffects runFlip = Projectile.velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            //奔跑残影一丝
            Main.EntitySpriteDraw(ninja, pos - Projectile.velocity * 1.2f, null, lit * (0.25f * fade),
                Projectile.rotation, origin, 1f, runFlip, 0);
            Main.EntitySpriteDraw(ninja, pos, null, lit * fade, Projectile.rotation, origin, 1f, runFlip, 0);
            return false;
        }
    }
}
