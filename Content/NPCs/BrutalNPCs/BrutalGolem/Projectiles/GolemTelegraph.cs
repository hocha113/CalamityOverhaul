using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>金色预警：ai[0]模式(0线/2环)，ai[1]转角或半径，ai[2]时长</summary>
    internal class GolemTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal static float LineLength => 1300f;

        private int Mode => (int)Projectile.ai[0];
        private int Duration => (int)System.Math.Max(Projectile.ai[2], 1f);
        /// <summary>预警进度，timeLeft 推导（各端一致）</summary>
        private float Progress => MathHelper.Clamp(1f - Projectile.timeLeft / (float)Duration, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        /// <summary>timeLeft 不随原版包同步，走 ExtraAI 校时保证各端预警进度一致</summary>
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write((short)Projectile.timeLeft);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            Projectile.timeLeft = System.Math.Max((int)reader.ReadInt16(), 1);
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.4f, 0.12f) * (0.4f + 0.6f * Progress));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Mode == 0) {
                DrawLine();
            }
            else {
                DrawRing();
            }
            return false;
        }

        /// <summary>射线预判线：金橙基线 + 充能推进段，末20%白热</summary>
        private void DrawLine() {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = new(0f, line.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.ai[1];
            float progress = Progress;
            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 0.7f);

            float flash = MathHelper.Clamp((progress - 0.8f) / 0.2f, 0f, 1f);
            Color baseCol = Color.Lerp(new Color(255, 150, 30), new Color(255, 225, 140), flash) with { A = 0 };
            Color hotCol = Color.Lerp(new Color(255, 200, 80), Color.White, flash) with { A = 0 };

            float lenScale = LineLength / line.Width;

            Main.EntitySpriteDraw(line, drawPos, null, baseCol * (0.5f * pulse),
                rot, origin, new Vector2(lenScale, 0.2f), SpriteEffects.None, 0);

            if (progress > 0.02f) {
                Rectangle chargeSrc = new(0, 0, (int)(line.Width * progress), line.Height);
                Main.EntitySpriteDraw(line, drawPos, chargeSrc, hotCol * 0.9f,
                    rot, origin, new Vector2(lenScale, 0.4f * pulse), SpriteEffects.None, 0);
            }

            Vector2 tip = drawPos + rot.ToRotationVector2() * LineLength * progress;
            Main.EntitySpriteDraw(glow, tip, null, hotCol * 0.9f,
                0f, glow.Size() / 2f, 0.5f + 0.3f * flash, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, baseCol * 0.8f,
                0f, glow.Size() / 2f, 0.65f * pulse, SpriteEffects.None, 0);
        }

        /// <summary>落点环：主环 + 收缩圈重合即起爆</summary>
        private void DrawRing() {
            Effect shader = EffectLoader.GolemSunTelegraph?.Value;
            if (shader == null) {
                DrawRingFallback();
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique = shader.Techniques["RingTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(Progress);
            shader.Parameters["uIntensity"]?.SetValue(1f);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            float size = Projectile.ai[1] * 2.6f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawRingFallback() {
            Texture2D circle = CWRAsset.DiffusionCircle.Value;
            float alpha = 0.3f + 0.5f * Progress;
            Color color = new Color(255, 170, 40, 0) * alpha;
            float scale = Projectile.ai[1] * 2f / circle.Width;
            Main.EntitySpriteDraw(circle, Projectile.Center - Main.screenPosition, null, color,
                0f, circle.Size() / 2f, scale, SpriteEffects.None, 0);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);

        #region 生成助手（服务端裁决，netImportant 同步到客户端）
        /// <summary>方向线预警</summary>
        internal static void SpawnLine(NPC owner, Vector2 center, float rotation, int duration) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<GolemTelegraph>(), 0, 0f, Main.myPlayer,
                0f, rotation, duration);
            ApplyDuration(id, duration);
        }

        /// <summary>落点环预警</summary>
        internal static void SpawnRing(NPC owner, Vector2 center, float radiusPx, int duration) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<GolemTelegraph>(), 0, 0f, Main.myPlayer,
                2f, radiusPx, duration);
            ApplyDuration(id, duration);
        }

        private static void ApplyDuration(int id, int duration) {
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].timeLeft = duration;
                Main.projectile[id].netUpdate = true;
            }
        }
        #endregion
    }
}
