using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 冲刺预警线（黄色，残酷 Boss 同一语法）：蓄势期沿即将出手的射向铺一条细线，
    /// 末段停追白闪 = 锁定承诺，出手帧收拢让位给冲刺本体。<br/>
    /// ai[0] 锚 NPC（-1 = 定点）；ai[1] 追踪玩家（-1 = 不追）；ai[2] = <see cref="PackParams"/>。<br/>
    /// 模式 0 定向（生成速度即射向，破土出土线）；模式 1 贴地掠冲瞄准（预测 + 仰角钳制，
    /// 与 <see cref="GroundDashAim"/> 同式）；模式 2 扑击瞄准（直指预测位）。
    /// 瞄准公式与状态侧共用同一静态函数，各端从同步的玩家位姿同算，线即承诺的射向。
    /// </summary>
    internal class BssDashOmen : BssModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>预警线主色（暖黄，沙色板里的警示色）</summary>
        private static readonly Vector3 LineColor = new(1f, 0.82f, 0.24f);

        /// <summary>模式 + 锁定帧 + 寿命打进 ai[2]（随生成包同步）</summary>
        internal static float PackParams(int mode, int duration, int lockLead)
            => mode + lockLead * 4f + duration * 256f;

        private int AnchorNpc => (int)Projectile.ai[0];
        private int TrackPlayer => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2] % 4;
        private int LockLead => (int)Projectile.ai[2] / 4 % 64;
        private int Duration => (int)Projectile.ai[2] / 256;
        private bool Locked => Projectile.timeLeft <= LockLead;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>贴地掠冲射向：预测 12 帧 + 仰角钳制（贴地承诺），状态与预警线共用</summary>
        internal static Vector2 GroundDashAim(Vector2 from, Player target) {
            Vector2 predicted = target.Center + target.velocity * 12f;
            Vector2 aim = (predicted - from).SafeNormalize(Vector2.UnitX);
            float ang = MathHelper.Clamp(MathF.Asin(MathHelper.Clamp(aim.Y, -1f, 1f)),
                -BssDirector.DashMaxPitch, BssDirector.DashMaxPitch);
            float sign = aim.X >= 0f ? 1f : -1f;
            return new Vector2(sign * MathF.Cos(ang), MathF.Sin(ang));
        }

        /// <summary>扑击瞄准点：预测半程飞行时间的玩家位（弹道反解与预警线共用）</summary>
        internal static Vector2 PounceAimPoint(Player target)
            => target.Center + target.velocity * (BssDirector.PounceFlightTime * 0.5f);

        public override void AI() {
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
            if (!Locked && player.Alives()) {
                if (Mode == 1) {
                    Projectile.rotation = GroundDashAim(Projectile.Center, player).ToRotation();
                }
                else if (Mode == 2) {
                    Projectile.rotation = (PounceAimPoint(player) - Projectile.Center).ToRotation();
                }
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.32f, 0.08f));
        }

        public override void OnKill(int timeLeft) {
            //退场余韵：线被冲刺吃掉时源头顺线扬一口沙
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(20f, 60f), DustID.Sand,
                    dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 5f), 110, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float total = Math.Max(Projectile.localAI[0], 1f);
            float lived = total - Projectile.timeLeft;
            float fadeIn = MathHelper.Clamp(lived / 5f, 0f, 1f);
            //线体从源头长出：前 8 帧推进到全长
            float grow = MathHelper.Clamp(lived / 8f, 0f, 1f);
            //末 4 帧向轴心收拢：绷紧让位给冲刺本体
            float collapse = MathHelper.Clamp(1f - Projectile.timeLeft / 4f, 0f, 1f);
            float lockT = Locked ? 1f - Projectile.timeLeft / (float)Math.Max(LockLead, 1) : 0f;

            if (EffectLoader.FishronTelegraph?.Value != null) {
                DrawShaderLine(EffectLoader.FishronTelegraph.Value, fadeIn, grow, lockT, collapse);
                return false;
            }
            DrawSpriteFallback(fadeIn, grow, lockT, collapse);
            return false;
        }

        /// <summary>着色器拉伸线：复用风暴预警线的水汽材质，换沙色板的暖黄</summary>
        private void DrawShaderLine(Effect effect, float fadeIn, float grow, float lockT, float collapse) {
            float width = 30f + lockT * 12f;
            float lineLength = BssDirector.DashOmenLength;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(fadeIn * (0.7f + lockT * 0.45f));
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uLockProgress"]?.SetValue(lockT);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uAspect"]?.SetValue(lineLength / width);
            //根部藏进头本体：羽化
            effect.Parameters["uRootFeather"]?.SetValue(Mode == 0 ? 0.02f : 0.06f);
            effect.Parameters["uColor"]?.SetValue(LineColor);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(lineLength / pixel.Width, width / pixel.Height);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, new Vector2(0, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失兜底：分段包络拉伸线</summary>
        private void DrawSpriteFallback(float fadeIn, float grow, float lockT, float collapse) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            const int Segments = 14;
            float segLen = BssDirector.DashOmenLength / Segments;
            float widthScale = 1f - collapse * 0.6f;

            for (int i = 0; i < Segments; i++) {
                float t0 = i / (float)Segments;
                if (t0 > grow) {
                    break;
                }
                float envelope = MathHelper.Clamp(t0 / 0.08f, 0f, 1f)
                    * MathHelper.Clamp((1f - t0) / 0.16f, 0f, 1f)
                    * MathHelper.Clamp((grow - t0) / 0.1f, 0f, 1f);
                Vector2 segPos = Projectile.Center + dir * (t0 * BssDirector.DashOmenLength) - Main.screenPosition;
                Color c = !Locked
                    ? new Color(255, 205, 60, 0) * (0.45f * fadeIn * pulse * envelope)
                    : new Color(255, 245, 190, 0) * (0.85f * (0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 6f)) * envelope);
                Main.EntitySpriteDraw(tex, segPos, null, c, Projectile.rotation,
                    new Vector2(0, tex.Height / 2f), new Vector2(segLen / tex.Width * 1.04f, (!Locked ? 0.2f : 0.3f) * widthScale),
                    SpriteEffects.None, 0);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
