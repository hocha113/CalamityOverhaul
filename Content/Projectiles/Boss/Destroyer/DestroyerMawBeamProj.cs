using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Destroyer
{
    /// <summary>
    /// 毁灭者「炽核熔射」巨型口吐光柱：锚定在蠕虫头部口器，按固定角速度缓慢横扫。
    /// <br/>ai[0] = 头部 NPC 的 whoAmI
    /// <br/>ai[1] = 起始角（弧度）
    /// <br/>ai[2] = 每帧扫射角速度（含方向）
    /// <para>较颅骨主炮更厚重华丽：复用 DestroyerBeam.fx 的白热主轴 + 缠绕电弧 + 推进脉冲，
    /// 外覆熔焰浊浪宽晕、沿束熔滴飞溅、口器多层聚能光球，与机械骷髅王橙色主炮形成红色差异化。
    /// 展开期不造成伤害（公平反应窗口），扫射角速度刻意压低，避免远端切向无解。</para>
    /// </summary>
    internal class DestroyerMawBeamProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder2;

        internal static int ExpandTime => 18;
        internal static int SweepFrames => 156;
        internal static int CollapseTime => 16;
        internal static int TotalLife => ExpandTime + SweepFrames + CollapseTime;

        /// <summary>光柱起点相对头部中心的前伸量（落在口器处）</summary>
        internal const float MuzzleOffset = 64f;
        private static float MaxBeamLength => 4500f;
        private static float MaxWidth => 126f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Head => CWRUtils.GetNPCInstance((int)Projectile.ai[0]);

        private float beamWidth;
        private float beamLength;

        private static Color ThemeBlood => new(255, 50, 24);
        private static Color ThemeGlow => new(255, 150, 70);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 30;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>头部仍存活且处于激光弹幕状态机阶段（否则快速收束）</summary>
        private bool HostValid {
            get {
                NPC head = Head;
                return head.Alives() && head.type == NPCID.TheDestroyer
                    && (int)head.ai[2] == (int)DestroyerStateIndex.LaserBarrage;
            }
        }

        /// <summary>激怒/狂暴宿主：beam 走更宽更炽白的 EX 表现</summary>
        private bool IsEnragedHost {
            get {
                NPC head = Head;
                return head.Alives() && head.life * 2 < head.lifeMax;
            }
        }

        /// <summary>供状态机定位本头部正在发射的光柱：多端一致地让口器朝向权威光束角</summary>
        internal static Projectile FindFor(int headWhoAmI) {
            int type = ModContent.ProjectileType<DestroyerMawBeamProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == headWhoAmI) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            NPC head = Head;

            //头部失效或已离开激光弹幕状态：快进到收束段
            if (!HostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
            }

            //扫射角：展开期定格起始角 → 匀速横扫 → 收束期定格末角
            float sweepT = MathHelper.Clamp(Timer - ExpandTime, 0f, SweepFrames);
            float beamAngle = Projectile.ai[1] + Projectile.ai[2] * sweepT;
            Projectile.rotation = beamAngle;

            if (head.Alives()) {
                Projectile.Center = head.Center + beamAngle.ToRotationVector2() * MuzzleOffset;
            }

            //宽度/长度展开与收束缓动
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(4f, MaxWidth, CWRUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, CWRUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, CWRUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            beamWidth *= 1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //沿束光照
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 7; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 7f * i), ThemeBlood.ToVector3() * 0.85f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //全功率期间的低频持续震屏（同 id 刷新，不堆叠）
            if ((int)Timer % 6 == 0) {
                DestroyerMotionFX.CameraPunch(Projectile.Center, 2.4f, 8, "DestroyerMawBeamRumble", beamDir);
            }

            //沿束熔滴飞溅：带重力余烬，"炽核熔射"的滚烫质感
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.45f, beamWidth * 0.45f);
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 9f),
                    Color.Lerp(ThemeGlow, Color.White, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat();
                Vector2 emberPos = Projectile.Center + beamDir * beamLength * along;
                PRTLoader.NewParticle<PRT_LavaFire>(emberPos,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0f, 2.5f)),
                    Color.White, Main.rand.NextFloat(0.8f, 1.4f))?.SetLifetime(20, 40);
            }

            //口器聚能（向心汇聚）
            if (Main.rand.NextBool(2)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(80f, 80f);
                PRTLoader.NewParticle<PRT_Spark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.12f,
                    ThemeBlood, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(false, 14);
            }
        }

        //未完全展开时不造成伤害，给玩家反应窗口
        public override bool? CanDamage() => beamWidth >= MaxWidth * 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            //碰撞宽度小于视觉宽度，宽容判定
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.6f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }

            bool ex = IsEnragedHost;
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);

            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                DrawShaderBeam(effect, noise, opacity, ex);
            }
            DrawAdditiveDressing(effect == null || noise == null, opacity, ex);
        }

        /// <summary>主光柱：DestroyerBeam.fx 在四边形 UV 内生成白热主轴 + 缠绕电弧 + 推进脉冲 + 头部光球</summary>
        private void DrawShaderBeam(Effect effect, Texture2D noise, float opacity, bool ex) {
            Vector2 mouth = Projectile.Center;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = mouth + dir * beamLength;
            //视觉宽度大于碰撞宽度，着色器边缘撕裂与电弧需要余量
            float halfW = beamWidth * (ex ? 2.5f : 2.1f);

            //uv.x: 1=口器(头部光球) → 0=末端(淡出)；uv.y: 0~1 横截面
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((mouth + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((mouth - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["exMode"]?.SetValue(ex ? 1f : 0f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>外覆熔焰浊浪宽晕 + 推进光球脉冲 + 口器多层聚能光球 / 十字星闪（兼任着色器缺失兜底）</summary>
        private void DrawAdditiveDressing(bool shaderMissing, float opacity, bool ex) {
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D line = CWRUtils.GetT2DValue(CWRConstant.Masking + "MaskLaserLine");
            Texture2D glow = CWRAsset.DiffusionCircle.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            Vector2 dir = rot.ToRotationVector2();
            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

            Color blood = ThemeBlood;
            Color amber = ThemeGlow;
            Color core = Color.White;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float lenScale = beamLength / line.Width;

            //外覆熔焰浊浪：宽幅低透红晕，撑起"巨柱"体量
            Main.EntitySpriteDraw(line, drawPos, null, blood * (0.5f * opacity), rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * (ex ? 7f : 6f) * flicker), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, Color.Lerp(blood, amber, 0.5f) * (0.6f * opacity), rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 3f), SpriteEffects.None, 0);
            //着色器缺失时补一条白热核线
            if (shaderMissing) {
                Main.EntitySpriteDraw(line, drawPos, null, core * (0.95f * opacity), rot, lineOrigin,
                    new Vector2(lenScale, beamWidth / line.Height * 1.2f * flicker), SpriteEffects.None, 0);
            }

            //推进能量脉冲：数颗光球自口器奔向末端
            const int pulses = 4;
            for (int i = 0; i < pulses; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)pulses) % 1f;
                Vector2 pPos = drawPos + dir * beamLength * along;
                float pScale = beamWidth / MaxWidth * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                Main.EntitySpriteDraw(glow, pPos, null, amber * (0.7f * opacity), 0f, glow.Size() / 2f,
                    pScale * (ex ? 1.5f : 1.1f) * 0.3f, SpriteEffects.None, 0);
            }

            //口器辉光：多层呼吸光球 + 十字星闪
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, drawPos, null, blood * (0.95f * opacity), 0f, glow.Size() / 2f,
                muzzleScale * (ex ? 3f : 2.4f) * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, amber * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 0.85f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, amber * (0.9f * opacity), Main.GlobalTimeWrappedHourly * 3.2f,
                star.Size() / 2f, muzzleScale * 0.8f * flicker, SpriteEffects.None, 0);

            sb.End();
        }
    }
}
