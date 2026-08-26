using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 磁力场表现体（伤害 0）：一场战斗只有一枚，进场磁力启动时由服务端生成，
    /// 各端本地从统帅的 MagnetGlow/MagnetPull 读强度与流向，不再发任何包。
    /// 可见层画 ScrapMagnetField 力线，扭曲层走 NeutronWarp 引力透镜。
    /// ai[0]=统帅 whoAmI
    /// </summary>
    internal class ScrapMagnetFieldProj : ScrapModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>场半径（世界 px）</summary>
        private const float FieldRadius = 420f;

        private NPC Boss => Main.npc[(int)Projectile.ai[0]];
        /// <summary>各端本地平滑后的场强</summary>
        private float strength;
        /// <summary>流向：+1 收束 / -1 外掷</summary>
        private float pull = 1f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            NPC boss = Boss;
            if (boss == null || !boss.active || boss.ModNPC is not ScrapCommander owner || owner.Context == null) {
                //统帅没了：场强泄掉后自灭
                strength *= 0.9f;
                if (strength < 0.02f) {
                    Projectile.Kill();
                }
                return;
            }
            Projectile.Center = boss.Center;
            Projectile.timeLeft = 90;
            strength = MathHelper.Lerp(strength, owner.Context.MagnetGlow, 0.14f);
            pull = MathHelper.Lerp(pull, owner.Context.MagnetPull, 0.2f);
        }

        public bool CanDrawCustom() => false;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>扭曲层：引力透镜，强度跟场强走；场歇着时零开销</summary>
        public void Warp() {
            if (strength < 0.04f) {
                return;
            }
            NeutronWarpHelper.DrawWarp(Projectile.Center,
                FieldRadius * 2.2f, FieldRadius * 2.2f,
                strength * 0.3f, 1f, 0f, "GravitationalLens", 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (strength < 0.03f) {
                return false;
            }
            Effect field = EffectLoader.ScrapMagnetField?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (field == null || noise == null || pixel == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            field.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            field.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.53f);
            field.Parameters["uStrength"]?.SetValue(strength);
            field.Parameters["uPull"]?.SetValue(pull);
            field.Parameters["uColorHot"]?.SetValue(new Vector3(1f, 0.59f, 0.23f));
            field.Parameters["uColorDeep"]?.SetValue(new Vector3(0.62f, 0.2f, 0.1f));
            field.CurrentTechnique.Passes[0].Apply();

            Vector2 size = new(FieldRadius * 2f);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, size / pixel.Size(), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
