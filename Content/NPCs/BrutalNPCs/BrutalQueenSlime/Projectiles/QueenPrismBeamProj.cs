using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>
    /// 棱镜折射光束：光束必须打在棱晶节点上折射改向<br/>
    /// ai[0]=光源NPC ai[1]=终点节点NPC ai[2]=打包(模式+相位+时长)；节点被击碎光束即断
    /// </summary>
    internal class QueenPrismBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int BeamDamage = 36;

        internal enum BeamMode : int
        {
            /// <summary>齐射馈线：短束，命中节点后折射出瞄准碎晶</summary>
            FeederVolley = 0,
            /// <summary>持续馈线：王冠→节点常亮供能</summary>
            Feeder = 1,
            /// <summary>环网跑马灯：节点→节点，亮暗窗口轮转</summary>
            WebMarquee = 2,
            /// <summary>圣殿辐辏跑马灯：王冠→节点</summary>
            CathedralSpoke = 3,
        }

        #region 参数打包
        /// <summary>打包 ai[2]：模式 + 相位*10 + 时长*1000</summary>
        internal static float PackMode(BeamMode mode, int phase, int duration) {
            return (int)mode + phase * 10 + duration * 1000;
        }

        private BeamMode Mode => (BeamMode)((int)Projectile.ai[2] % 10);
        private int Phase => (int)Projectile.ai[2] / 10 % 100;
        private int PackedDuration => (int)Projectile.ai[2] / 1000;
        #endregion

        #region 节奏常量
        private const int VolleyExpand = 8;
        private const int VolleyEmitFrame = 16;
        private const int VolleyCollapseStart = 38;
        private const int VolleyLife = 50;
        private const int ExpandTime = 10;
        private const int CollapseTime = 14;
        private const float MaxWidth = 30f;
        #endregion

        private ref float Timer => ref Projectile.localAI[0];
        private bool emitted;
        private float beamWidth;

        private NPC SourceNpc => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private NPC TargetNpc => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>光束寿命(按模式)</summary>
        private int TotalLife => Mode == BeamMode.FeederVolley ? VolleyLife : Math.Max(PackedDuration, 60);

        /// <summary>光源锚点：皇后取王冠，节点取中心</summary>
        private Vector2 SourceAnchor {
            get {
                NPC src = SourceNpc;
                if (src == null) {
                    return Projectile.Center;
                }
                return src.type == NPCID.QueenSlimeBoss ? QueenSlimeRenderHelper.CrownAnchor(src) : src.Center;
            }
        }

        private Vector2 TargetAnchor => TargetNpc?.Center ?? Projectile.Center;

        /// <summary>宿主有效性：两端存活+皇后仍处于对应状态</summary>
        private bool HostValid {
            get {
                NPC src = SourceNpc;
                NPC dst = TargetNpc;
                if (!src.Alives() || !dst.Alives()) {
                    return false;
                }

                //解析皇后
                NPC queen = src.type == NPCID.QueenSlimeBoss ? src
                    : (int)dst.ai[2] >= 0 && (int)dst.ai[2] < Main.maxNPCs ? Main.npc[(int)dst.ai[2]] : null;
                if (!queen.Alives() || queen.type != NPCID.QueenSlimeBoss) {
                    return false;
                }

                int state = (int)queen.ai[2];
                return Mode switch {
                    BeamMode.FeederVolley => state == (int)QueenSlimeStateIndex.PrismVolley,
                    BeamMode.Feeder or BeamMode.WebMarquee => state == (int)QueenSlimeStateIndex.CrystalCathedral,
                    BeamMode.CathedralSpoke => state == (int)QueenSlimeStateIndex.CrystalCathedral,
                    _ => false,
                };
            }
        }

        /// <summary>跑马灯窗口：0~1亮度，非跑马灯模式恒1</summary>
        private float MarqueeGlow() {
            if (Mode is BeamMode.FeederVolley or BeamMode.Feeder) {
                return 1f;
            }
            int period = Mode == BeamMode.WebMarquee ? 100 : 120;
            int activeLen = Mode == BeamMode.WebMarquee ? 55 : 44;
            float cyc = (Timer + Phase * 20) % period;
            if (cyc >= activeLen) {
                //暗窗保留12%微光提示走线
                return 0.12f;
            }
            //亮窗内边缘渐入渐出
            float edge = Math.Min(cyc, activeLen - cyc);
            return MathHelper.Clamp(edge / 9f, 0.12f, 1f);
        }

        /// <summary>亮窗判定(伤害门)</summary>
        private bool MarqueeDamaging() {
            if (Mode is BeamMode.FeederVolley or BeamMode.Feeder) {
                return true;
            }
            int period = Mode == BeamMode.WebMarquee ? 100 : 120;
            int activeLen = Mode == BeamMode.WebMarquee ? 55 : 44;
            float cyc = (Timer + Phase * 20) % period;
            //伤害窗比可见亮窗略窄，贴合视觉
            return cyc > 5f && cyc < activeLen - 4f;
        }

        public override void AI() {
            //宿主失效快进收束
            if (!HostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = Mode == BeamMode.FeederVolley ? 0.8f : 0.55f,
                    Pitch = 0.45f,
                    MaxInstances = 5
                }, SourceAnchor);
            }

            //光束附着两端
            Projectile.Center = SourceAnchor;

            //宽度包络
            int expand = Mode == BeamMode.FeederVolley ? VolleyExpand : ExpandTime;
            int collapseStart = TotalLife - (Mode == BeamMode.FeederVolley ? VolleyLife - VolleyCollapseStart : CollapseTime);
            if (Timer < expand) {
                beamWidth = MaxWidth * QueenMotion.SnapOut(Timer / (float)expand, 4);
            }
            else if (Timer >= collapseStart) {
                float p = (Timer - collapseStart) / (float)(TotalLife - collapseStart);
                beamWidth = MaxWidth * (1f - p * p);
            }
            else {
                beamWidth = MaxWidth;
            }

            //折射发射帧(仅齐射馈线，服务端)
            if (Mode == BeamMode.FeederVolley && !emitted && Timer >= VolleyEmitFrame) {
                emitted = true;
                if (!VaultUtils.isClient) {
                    EmitRefraction();
                }
                if (!VaultUtils.isServer) {
                    QueenMotion.CrystalShatterBurst(TargetAnchor, 0.75f, Phase * 0.2f, playSound: false);
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.85f, Pitch = 0.3f, MaxInstances = 5 }, TargetAnchor);
                }
            }

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //沿束点亮
            float glow = MarqueeGlow();
            if (glow > 0.3f) {
                Vector2 a = SourceAnchor;
                Vector2 b = TargetAnchor;
                for (int i = 0; i < 4; i++) {
                    Lighting.AddLight(Vector2.Lerp(a, b, i / 3f),
                        QueenMotion.PrismHue(Phase * 0.17f).ToVector3() * 0.5f * glow);
                }
            }

            //节点馈能视觉信号(各端本地)：齐射馈线蓄能爬升+折射拍过冲，其余随亮窗
            NPC feedDst = TargetNpc;
            if (feedDst.Alives() && feedDst.type == NPCID.QueenSlimeMinionBlue) {
                float feed = Mode == BeamMode.FeederVolley
                    ? (Timer < VolleyEmitFrame
                        ? Timer / (float)VolleyEmitFrame
                        : MathHelper.Clamp(1.2f - (Timer - VolleyEmitFrame) * 0.05f, 0f, 1.2f))
                    : glow * 0.65f;
                feedDst.localAI[3] = Math.Max(feedDst.localAI[3], feed);
            }
        }

        /// <summary>折射：自节点向其最近玩家散射密集尖刺扇(服务端)，材质化出生自带前摇</summary>
        private void EmitRefraction() {
            NPC node = TargetNpc;
            if (!node.Alives()) {
                return;
            }
            int closest = Player.FindClosest(node.position, node.width, node.height);
            if (closest < 0) {
                return;
            }
            Player target = Main.player[closest];
            Vector2 baseDir = (target.Center - node.Center).SafeNormalize(Vector2.UnitY);
            int count = CWRWorld.Asura ? 6 : 5;
            for (int i = 0; i < count; i++) {
                float spread = MathHelper.Lerp(-0.4f, 0.4f, count == 1 ? 0.5f : i / (float)(count - 1));
                Vector2 vel = baseDir.RotatedBy(spread) * 9.2f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), node.Center, vel,
                    ModContent.ProjectileType<QueenCrystalSpikeProj>(), QueenCrystalSpikeProj.SpikeDamage, 0f, Main.myPlayer,
                    (int)QueenCrystalSpikeProj.Mode.Aimed, 0f, (Phase * 0.2f + i * 0.13f) % 1f);
            }
        }

        /// <summary>展开完成+亮窗才有伤害；持续馈线是供能演出，长期横穿场内不参与判定</summary>
        public override bool? CanDamage() {
            if (Mode == BeamMode.Feeder) {
                return false;
            }
            int expand = Mode == BeamMode.FeederVolley ? VolleyExpand : ExpandTime;
            if (Timer <= expand || beamWidth < MaxWidth * 0.5f) {
                return false;
            }
            return MarqueeDamaging() ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                SourceAnchor, TargetAnchor, beamWidth * 0.62f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>光束主体(棱彩色散着色器)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1.2f) {
                return;
            }
            Effect effect = EffectLoader.QueenPrismBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            Vector2 a = SourceAnchor;
            Vector2 b = TargetAnchor;
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //视觉半宽含色散/辉光余量
            float halfW = beamWidth * 2.8f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((a + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((a - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((b + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((b - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f) * MarqueeGlow());
            effect.Parameters["uHueSeed"]?.SetValue(Phase * 0.17f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uBeamLen"]?.SetValue(Vector2.Distance(a, b));
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

        /// <summary>端点缀饰：源辉光、节点晶闪、行进光子</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (beamWidth <= 1.2f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float glowLevel = MarqueeGlow();
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f) * glowLevel;
            Color hue = QueenMotion.PrismHue(Phase * 0.17f);
            Vector2 a = SourceAnchor - Main.screenPosition;
            Vector2 b = TargetAnchor - Main.screenPosition;
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            float dist = Vector2.Distance(a, b);
            float flick = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 36f + Phase);

            //源端辉光
            spriteBatch.Draw(glow, a, null, hue * (0.7f * opacity), 0f, glow.Size() / 2f, 0.9f * flick, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, a, null, Color.White * (0.45f * opacity), 0f, glow.Size() / 2f, 0.45f, SpriteEffects.None, 0f);

            //终端节点晶闪(折射点)
            float emitPulse = Mode == BeamMode.FeederVolley && Timer > VolleyEmitFrame - 4 && Timer < VolleyEmitFrame + 10
                ? 1.6f : 1f;
            spriteBatch.Draw(glow, b, null, hue * (0.85f * opacity), 0f, glow.Size() / 2f, 1.1f * emitPulse * flick, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, b, null, Color.White * (0.7f * opacity),
                Main.GlobalTimeWrappedHourly * 3f + Phase, star.Size() / 2f, 0.4f * emitPulse, SpriteEffects.None, 0f);

            //行进光子(源→节点，读出能量流向)
            const int photons = 3;
            for (int i = 0; i < photons; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 1.1f + i / (float)photons + Phase * 0.29f) % 1f;
                Vector2 pos = a + dir * dist * along;
                float pScale = 0.32f * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                spriteBatch.Draw(glow, pos, null, Color.White * (0.6f * opacity), 0f, glow.Size() / 2f, pScale, SpriteEffects.None, 0f);
            }
        }
    }
}
