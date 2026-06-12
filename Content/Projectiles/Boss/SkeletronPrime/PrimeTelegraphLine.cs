using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 机械骷髅王通用预警弹幕：线/扇/环三模式，纯视觉不可伤害。
    /// ai[0]=模式(0线1扇2环) ai[1]=预警进度0~1 ai[2]=强度
    /// </summary>
    internal class PrimeTelegraphLine : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 90;
            Projectile.hide = true;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) {
            if (EffectLoader.PrimeTelegraph == null || !EffectLoader.PrimeTelegraph.IsLoaded) {
                return false;
            }

            Effect fx = EffectLoader.PrimeTelegraph.Value;
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(Projectile.ai[1], 0f, 1f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(Projectile.ai[2], 0f, 1f));
            fx.Parameters["uMode"]?.SetValue(Projectile.ai[0]);

            float length = MathHelper.Lerp(120f, 520f, Projectile.ai[1]);
            float width = MathHelper.Lerp(24f, 64f, Projectile.ai[1]);
            Vector2 origin = new(0f, 0.5f);
            Rectangle frame = new(0, 0, 1, 1);
            Texture2D white = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            fx.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(white, Projectile.Center - Main.screenPosition, frame, Color.White,
                Projectile.rotation, origin, new Vector2(length, width), SpriteEffects.None, 0f);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        internal static int SpawnLine(Vector2 center, Vector2 direction, float progress, float intensity, int timeLeft = 36) {
            if (VaultUtils.isClient) {
                return -1;
            }
            float rot = direction.ToRotation();
            return Projectile.NewProjectile(new EntitySource_WorldEvent(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer, 0f, progress, intensity);
        }

        internal static int SpawnFan(Vector2 center, float rotation, float progress, float intensity, int timeLeft = 90) {
            if (VaultUtils.isClient) {
                return -1;
            }
            int id = Projectile.NewProjectile(new EntitySource_WorldEvent(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer, 1f, progress, intensity);
            if (id >= 0) {
                Main.projectile[id].rotation = rotation;
                Main.projectile[id].timeLeft = timeLeft;
            }
            return id;
        }

        internal static int SpawnRing(Vector2 center, float progress, float intensity, int timeLeft = 90) {
            if (VaultUtils.isClient) {
                return -1;
            }
            int id = Projectile.NewProjectile(new EntitySource_WorldEvent(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer, 2f, progress, intensity);
            if (id >= 0) {
                Main.projectile[id].timeLeft = timeLeft;
            }
            return id;
        }
    }
}
