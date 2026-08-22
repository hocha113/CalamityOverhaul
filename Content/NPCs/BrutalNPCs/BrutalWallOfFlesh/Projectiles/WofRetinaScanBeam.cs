using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 视网膜扫描光束：三角波上下扫掠的持续血光。
    /// ai[0]=眼whoAmI ai[1]=相位偏移(0/0.5) ai[2]=扫速倍率。
    /// 上下眼错半周期，一束在极点时另一束恰过中线，窗口恒存
    /// </summary>
    internal class WofRetinaScanBeam : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandTime = 20;
        internal const int CollapseTime = 18;
        /// <summary>三角波单周期帧数(基准)</summary>
        private const float TrianglePeriod = 160f;
        private const float MaxBeamLength = 3200f;
        private const float MaxWidth = 58f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Eye => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;
        private float beamLength;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>扫掠帧数：阶段3更长</summary>
        private int SweepFrames {
            get {
                if (WallOfFleshAI.TryGetWall(out NPC wall) && (int)wall.ai[1] >= 3) {
                    return WofDirector.ScanDuration + 40;
                }
                return WofDirector.ScanDuration;
            }
        }

        private int TotalLife => ExpandTime + SweepFrames + CollapseTime;

        /// <summary>宿主有效：眼活着且墙仍在扫描态</summary>
        private bool HostValid {
            get {
                NPC eye = Eye;
                if (!eye.Alives() || eye.type != NPCID.WallofFleshEye) {
                    return false;
                }
                return WallOfFleshAI.TryGetWall(out NPC wall)
                    && WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.EyeScan;
            }
        }

        /// <summary>按眼whoAmI找束(眼部锁定跟随用)</summary>
        internal static Projectile FindForEye(int eyeWhoAmI) {
            int type = Terraria.ModLoader.ModContent.ProjectileType<WofRetinaScanBeam>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == eyeWhoAmI) {
                    return p;
                }
            }
            return null;
        }

        /// <summary>三角波 [-1,1]</summary>
        private static float Triangle(float x) {
            return 4f * Math.Abs(x - (float)Math.Floor(x) - 0.5f) - 1f;
        }

        /// <summary>当前扫掠偏移相位值</summary>
        private float SweepValue {
            get {
                float sweepT = MathHelper.Clamp(Timer - ExpandTime, 0f, SweepFrames);
                float speedScale = Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
                return Triangle(sweepT * speedScale / TrianglePeriod + Projectile.ai[1]);
            }
        }

        public override void AI() {
            NPC eye = Eye;

            //宿主失效快进收束
            if (!HostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.85f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
            }

            //扫掠角：水平基线±弧半径的三角波
            int dir = 1;
            if (eye.Alives()) {
                dir = eye.direction != 0 ? eye.direction : 1;
                Projectile.Center = eye.Center;
            }
            float offset = SweepValue * WofDirector.ScanArcHalf;
            float beamAngle = dir > 0 ? offset : MathHelper.Pi - offset;
            Projectile.rotation = beamAngle;

            //宽长缓动
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / (float)ExpandTime;
                beamWidth = MathHelper.Lerp(3f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            //湿性搏动
            beamWidth *= 1f + 0.07f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.ai[1] * 9f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            Vector2 beamDir = beamAngle.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 6f * i),
                    WofMotionFX.BloodHot.ToVector3() * 0.6f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //沿束血珠滴洒
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat(0.1f, 0.9f);
                Vector2 dropPos = Projectile.Center + beamDir * beamLength * along;
                if (WofMotionFX.OnScreen(dropPos, 60f)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(dropPos,
                        beamDir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * Main.rand.NextFloat(1f, 3f),
                        WofMotionFX.BloodMid, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 28), 0.3f);
                }
            }
            //折返拍音效：扫掠方向掉头的湿响
            float sweep = SweepValue;
            if (Math.Abs(sweep) > 0.985f && (int)Timer % 8 == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.3f, Volume = 0.4f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        //展开完才可伤
        public override bool? CanDamage() => Timer > ExpandTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.55f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 240);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }
            Effect effect = EffectLoader.WofRetinaBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Vector2 muzzle = Projectile.Center;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = muzzle + dir * beamLength;
            //起始边后撤进眼球内：着色器最后46px是根部生长段，切边永藏眼内
            float backBleed = beamWidth * 0.4f + 30f;
            Vector2 origin = muzzle - dir * backBleed;
            float halfW = beamWidth * 2.6f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            //预乘AlphaBlend：着色器输出暗血鞘遮挡背景，光束不再是只加亮的幻影(契约4)
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float turn = (float)Math.Pow(Math.Abs(SweepValue), 8);
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["seed"]?.SetValue(Projectile.ai[1] * 0.61f + Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uScanTurn"]?.SetValue(turn);
            effect.Parameters["uQuadLen"]?.SetValue(beamLength + backBleed);
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (beamWidth <= 1f) {
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float flicker = 1f + 0.09f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);

            //眼窝聚光，此批是真加色(源因子=SourceAlpha)，A=0 什么都画不出，A 必须随强度走
            Main.EntitySpriteDraw(glow, screenPos, null, WofMotionFX.BloodHot * (0.9f * opacity),
                0f, glow.Size() / 2f, 1.7f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, new Color(255, 170, 150) * (0.7f * opacity),
                0f, glow.Size() / 2f, 0.9f, SpriteEffects.None, 0);
        }
    }
}
