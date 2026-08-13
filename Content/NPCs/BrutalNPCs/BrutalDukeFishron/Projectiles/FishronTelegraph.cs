using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 风暴预警线；ai[0] 锚NPC(-1定点) ai[1] 追玩家(-1不追) ai[2]=PackParams(模式,时长)。<br/>
    /// 模式0 锚定追转（冲刺瞄准线）；模式1 天雷垂直落线；模式2 固定斜线（俯冲航道）
    /// </summary>
    internal class FishronTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>末段停追+白闪帧数</summary>
        internal const int LockTime = 14;

        /// <summary>模式+时长打进 ai[2]，随生成同步</summary>
        internal static float PackParams(int mode, int duration) => mode + duration * 4f;

        private int AnchorNpc => (int)Projectile.ai[0];
        private int TrackPlayer => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2] % 4;
        private int Duration => (int)Projectile.ai[2] / 4;
        private bool Locked => Projectile.timeLeft <= LockTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧套打包时长
            if (Projectile.localAI[0] == 0f) {
                if (Duration > 0) {
                    Projectile.timeLeft = Duration;
                }
                Projectile.localAI[0] = Projectile.timeLeft;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            NPC anchor = AnchorNpc.TryGetNPC(out NPC a) ? a : null;
            if (anchor.Alives()) {
                Projectile.Center = anchor.Center;
            }

            Player player = TrackPlayer.TryGetPlayer(out Player p) ? p : null;
            if (!Locked && player.Alives() && Mode == 0) {
                //硬追踪：与状态侧的冻结时机严格同拍，锁线即是承诺的冲刺线
                Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Lighting.AddLight(Projectile.Center, new Vector3(0.1f, 0.4f, 0.45f));
        }

        public override void OnKill(int timeLeft) {
            //退场余韵：线被冲刺"吃掉"的一小口水汽，源头处顺线喷散
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            FishronMotionFX.SpawnSprayCone(Projectile.Center + dir * 40f, dir, 5, 2f, 7f, 0.5f, 0.7f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float total = Math.Max(Projectile.localAI[0], 1f);
            float lived = total - Projectile.timeLeft;
            float fadeIn = MathHelper.Clamp(lived / 6f, 0f, 1f);
            //线体从源头长出：前 9 帧推进到全长（生长前沿在着色器里带软边毛边）
            float grow = MathHelper.Clamp(lived / 9f, 0f, 1f);
            //末 5 帧向轴心收拢：绷紧让位给冲刺本体，不是瞬灭
            float collapse = MathHelper.Clamp(1f - Projectile.timeLeft / 5f, 0f, 1f);
            float lockT = Locked ? 1f - Projectile.timeLeft / (float)LockTime : 0f;
            float lineLength = Mode == 1 ? 2600f : 1900f;

            if (EffectLoader.FishronTelegraph?.Value != null) {
                DrawShaderLine(EffectLoader.FishronTelegraph.Value, lineLength, fadeIn, grow, lockT, collapse);
                return false;
            }

            DrawSpriteFallback(lineLength, fadeIn, grow, lockT, collapse);
            return false;
        }

        /// <summary>着色器拉伸线：两端羽化 + 生长前沿 + 退场收拢全在 UV 层完成</summary>
        private void DrawShaderLine(Effect effect, float lineLength, float fadeIn, float grow, float lockT, float collapse) {
            float width = 96f + lockT * 54f;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(fadeIn * (0.66f + lockT * 0.4f));
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uLockProgress"]?.SetValue(lockT);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uAspect"]?.SetValue(lineLength / width);
            //落雷线根部在地面锚点：要实不要虚；冲刺/航道线根部藏进本体：羽化
            effect.Parameters["uRootFeather"]?.SetValue(Mode == 1 ? 0.015f : 0.07f);
            effect.Parameters["uColor"]?.SetValue(new Vector3(0.2f, 0.78f, 0.82f));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(lineLength / pixel.Width, width / pixel.Height);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, new Vector2(0, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失兜底：分段包络拉伸线，端部仍然渐入渐出</summary>
        private void DrawSpriteFallback(float lineLength, float fadeIn, float grow, float lockT, float collapse) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            //横向包络走分段：每段独立 alpha，源头/末端软化
            const int Segments = 16;
            float segLen = lineLength / Segments;
            float widthScale = 1f - collapse * 0.6f;

            for (int i = 0; i < Segments; i++) {
                float t0 = i / (float)Segments;
                if (t0 > grow) {
                    break;
                }
                //两端包络 + 生长前沿软边
                float envelope = MathHelper.Clamp(t0 / 0.08f, 0f, 1f)
                    * MathHelper.Clamp((1f - t0) / 0.16f, 0f, 1f)
                    * MathHelper.Clamp((grow - t0) / 0.1f, 0f, 1f);
                Vector2 segPos = Projectile.Center + dir * (t0 * lineLength) - Main.screenPosition;
                Color c = !Locked
                    ? new Color(45, 200, 210, 0) * (0.4f * fadeIn * pulse * envelope)
                    : new Color(170, 245, 245, 0) * (0.8f * (0.7f + 0.3f * (float)Math.Sin(lockT * MathHelper.Pi * 6f)) * envelope);
                Main.EntitySpriteDraw(tex, segPos, null, c, Projectile.rotation,
                    new Vector2(0, tex.Height / 2f), new Vector2(segLen / tex.Width * 1.04f, (!Locked ? 0.35f : 0.55f) * widthScale),
                    SpriteEffects.None, 0);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
