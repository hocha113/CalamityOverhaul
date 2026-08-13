using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 日舞光束：锚在女皇身上的径向光束，预告细线→展开→旋切→收拢；
    /// ai[0]=基准角 ai[1]=宿主whoAmI ai[2]=旋切角速度(带符号)
    /// 旋角是Time确定函数，各端一致
    /// </summary>
    internal class EmpressSunray : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int TelegraphTime = 36;
        internal const int ExpandTime = 14;
        internal const int ActiveTime = 130;
        internal const int FadeTime = 30;
        internal const int TotalLife = TelegraphTime + ExpandTime + ActiveTime + FadeTime;

        private const float BeamLength = 1750f;
        private const float MaxWidth = 58f;

        private ref float Timer => ref Projectile.localAI[0];
        private float BaseAngle => Projectile.ai[0];
        private NPC Host => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private float SweepRate => Projectile.ai[2];

        private float beamWidth;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>宿主失效或已离开需要日舞的状态则快进收拢</summary>
        private bool HostValid {
            get {
                NPC host = Host;
                return host.Alives() && host.type == NPCID.HallowBoss;
            }
        }

        public override void AI() {
            if (!HostValid) {
                if (Timer < TotalLife - FadeTime) {
                    Timer = TotalLife - FadeTime;
                }
                if (Host == null) {
                    Projectile.Kill();
                    return;
                }
            }

            //旋切进度：active期线性推进
            float activeT = MathHelper.Clamp(Timer - TelegraphTime - ExpandTime, 0f, ActiveTime);
            Projectile.rotation = BaseAngle + SweepRate * activeT;

            NPC hostNpc = Host;
            if (hostNpc.Alives()) {
                Projectile.Center = hostNpc.Center;
            }
            Projectile.velocity = Vector2.Zero;

            //宽度包络
            if (Timer < TelegraphTime) {
                beamWidth = 3f;
            }
            else if (Timer < TelegraphTime + ExpandTime) {
                float t = (Timer - TelegraphTime) / ExpandTime;
                beamWidth = MathHelper.Lerp(3f, MaxWidth, VaultUtils.EaseOutCubic(t));
            }
            else if (Timer >= TotalLife - FadeTime) {
                float t = (Timer - (TotalLife - FadeTime)) / FadeTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
            }
            else {
                beamWidth = MaxWidth * (1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f + BaseAngle * 3f));
            }

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //沿束照明
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Color prism = RayColor();
            for (int i = 1; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + dir * (BeamLength / 6f * i), prism.ToVector3() * 0.5f * (beamWidth / MaxWidth));
            }

            //沿束光尘
            if (!VaultUtils.isServer && beamWidth > MaxWidth * 0.5f && Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat(0.15f, 1f);
                Vector2 pos = Projectile.Center + dir * BeamLength * along;
                PRTLoader.NewParticle<PRT_EmpressSpark>(pos,
                    dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * Main.rand.NextFloat(1f, 3f),
                    prism, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(14, HueOf());
            }
        }

        private float HueOf() => (BaseAngle / MathHelper.TwoPi + Timer / (float)TotalLife * 0.35f) % 1f;

        private Color RayColor() => Main.hslToRgb((HueOf() % 1f + 1f) % 1f, 1f, 0.68f);

        //预告与收拢期无伤，判定窗对齐可见光束
        public override bool? CanDamage() {
            bool active = Timer > TelegraphTime + ExpandTime * 0.7f && Timer < TotalLife - FadeTime * 0.6f;
            return active ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            //判定比视觉窄一档
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + dir * 40f, Projectile.Center + dir * BeamLength, beamWidth * 0.55f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (EffectLoader.EmpressSunbeam?.Value != null) {
                return false;
            }
            //着色器缺失后备：细线+光晕
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Color prism = RayColor() with { A = 0 };
            Main.spriteBatch.Draw(line, Projectile.Center - Main.screenPosition, null,
                prism * 0.85f, Projectile.rotation, new Vector2(0f, line.Height / 2f),
                new Vector2(BeamLength / line.Width, beamWidth / line.Height), SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 0.8f) {
                return;
            }
            Effect effect = EffectLoader.EmpressSunbeam?.Value;
            if (effect == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            bool telegraphPhase = Timer <= TelegraphTime;
            float telegraphT = MathHelper.Clamp(Timer / (float)TelegraphTime, 0f, 1f);

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHue"]?.SetValue(HueOf());
            effect.Parameters["uTelegraph"]?.SetValue(telegraphPhase ? telegraphT : 0f);
            effect.Parameters["uWidthRatio"]?.SetValue(beamWidth / MaxWidth);

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //视觉半宽含羽化余量
            float halfW = Math.Max(beamWidth * 2.6f, 20f);
            Vector2 root = Projectile.Center - dir * 40f;
            Vector2 tip = Projectile.Center + dir * BeamLength;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((root + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((root - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
