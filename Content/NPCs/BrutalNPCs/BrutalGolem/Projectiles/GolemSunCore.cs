using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>太阳核心（大招主体）：升空 → 四辐条旋灼 + 陨星编排 → 坍缩死寂 → 终爆
    /// ai[0..1]=锚点坐标；伤害参数为辐条伤害</summary>
    internal class GolemSunCore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int TotalFrames = 430;
        internal const int RiseEnd = 60;
        internal const int SpokeEnd = 360;
        internal const int CollapseEnd = 400;
        internal const int SpokeCount = 4;
        internal const float SpokeLength = 1150f;
        internal const float SpokeWidth = 26f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private Vector2 Anchor => new(Projectile.ai[0], Projectile.ai[1]);

        private bool initialized;
        private int meteorTimer;
        private int meteorTargetCursor;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        /// <summary>中途加入校时：同步已流逝帧数，防加入者的核心时间轴重置</summary>
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write((short)Math.Max(Elapsed, 0));
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            short elapsed = reader.ReadInt16();
            Projectile.timeLeft = Math.Max(TotalFrames - elapsed, 1);
        }

        /// <summary>辐条基准角（identity 跨端一致）</summary>
        private float SpokeBaseAngle => Projectile.identity * 0.7f;

        /// <summary>当前辐条转角</summary>
        private float SpokeAngle {
            get {
                int t = Math.Max(Elapsed - RiseEnd, 0);
                //慢启动→匀速：前60帧转速热身（公平阀）
                float warm = MathHelper.Clamp(t / 60f, 0.25f, 1f);
                return SpokeBaseAngle + t * 0.0082f * warm;
            }
        }

        /// <summary>辐条长度包络</summary>
        private float SpokeLenScale {
            get {
                if (Elapsed < RiseEnd) {
                    return 0f;
                }
                if (Elapsed < RiseEnd + 40) {
                    //伸展
                    float t = (Elapsed - RiseEnd) / 40f;
                    return 1f - MathF.Pow(1f - t, 3f);
                }
                if (Elapsed < SpokeEnd) {
                    return 1f;
                }
                if (Elapsed < CollapseEnd) {
                    //坍缩收回
                    return MathHelper.Clamp((CollapseEnd - Elapsed) / 40f, 0f, 1f);
                }
                return 0f;
            }
        }

        /// <summary>核心视觉半径比例</summary>
        private float CoreScale {
            get {
                if (Elapsed < RiseEnd) {
                    float t = Elapsed / (float)RiseEnd;
                    return t * t;
                }
                if (Elapsed < SpokeEnd) {
                    return 1f;
                }
                if (Elapsed < CollapseEnd) {
                    //爆前收缩到四成——变小之后才变响
                    return MathHelper.Lerp(1f, 0.4f, (Elapsed - SpokeEnd) / (float)(CollapseEnd - SpokeEnd));
                }
                //终爆展开
                return MathHelper.Lerp(0.4f, 1.6f, MathHelper.Clamp((Elapsed - CollapseEnd) / 10f, 0f, 1f));
            }
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item78 with { Pitch = 0.2f, Volume = 0.9f }, Projectile.Center);
                }
            }

            //升空段趋向锚点，其后驻留微漂
            if (Elapsed < RiseEnd) {
                Projectile.velocity = (Anchor - Projectile.Center) * 0.06f;
            }
            else {
                Projectile.velocity *= 0.9f;
                Projectile.Center = Vector2.Lerp(Projectile.Center,
                    Anchor + new Vector2(0f, MathF.Sin(Elapsed * 0.03f) * 12f), 0.03f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.75f, 0.3f) * CoreScale);

            //辐条期：陨星编排（服务端）
            if (Elapsed >= RiseEnd + 30 && Elapsed < SpokeEnd - 30 && !VaultUtils.isClient) {
                bool death = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
                int interval = death ? 34 : 44;
                if (++meteorTimer >= interval) {
                    meteorTimer = 0;
                    ScheduleMeteor();
                }
            }

            //坍缩死寂拍
            if (Elapsed == SpokeEnd && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item78 with { Pitch = -0.6f, Volume = 0.7f }, Projectile.Center);
            }

            //终爆
            if (Elapsed == CollapseEnd) {
                Detonate();
            }

            //环绕吸积粒子（辐条期，密度截断在72%处）
            if (!Main.dedServ && Elapsed > RiseEnd && Elapsed < SpokeEnd
                && (Elapsed - RiseEnd) < (SpokeEnd - RiseEnd) * 0.9f && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(160f, 300f);
                Dust dust = Dust.NewDustPerfect(from, DustID.SolarFlare,
                    (Projectile.Center - from) * 0.05f + (from - Projectile.Center).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 1.6f,
                    0, default, 1.3f);
                dust.noGravity = true;
            }
        }

        /// <summary>为在场玩家轮流编排陨星（服务端）</summary>
        private void ScheduleMeteor() {
            //收集有效目标
            int found = -1;
            for (int i = 0; i < Main.maxPlayers; i++) {
                int cursor = (meteorTargetCursor + i) % Main.maxPlayers;
                Player p = Main.player[cursor];
                if (p.active && !p.dead && p.Distance(Projectile.Center) < 3200f) {
                    found = cursor;
                    break;
                }
            }
            if (found < 0) {
                return;
            }
            meteorTargetCursor = found + 1;

            Player target = Main.player[found];
            float groundY = States.GolemHookSwingState.FindGroundY(target);
            float x = target.Center.X + target.velocity.X * 18f + Main.rand.NextFloat(-40f, 40f);

            NPC owner = NPC.golemBoss >= 0 && NPC.golemBoss < Main.maxNPCs ? Main.npc[NPC.golemBoss] : null;
            if (owner == null || !owner.active) {
                return;
            }

            //落点环预警 + 高空备弹
            int delay = GolemDirector.MarkTelegraph;
            GolemTelegraph.SpawnRing(owner, new Vector2(x, groundY - 10f), 130f, delay + 16);
            int damage = GolemDirector.ScaleDamage(GolemDirector.MeteorDamage, CWRRef.GetDeathMode());
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), new Vector2(x, groundY - 980f), Vector2.Zero,
                ModContent.ProjectileType<GolemSolarMeteor>(), damage, 0f, Main.myPlayer,
                delay - 22, groundY);
        }

        /// <summary>终爆：白闪+冲击帧+辐射弹扇（一场只此一次的冲击帧）</summary>
        private void Detonate() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 1.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
                GolemScreenEffects.PushSunFlash(Projectile.Center, 1f, 36);
                GolemScreenEffects.PushImpactFrame(0.9f, 14);
                GolemScreenEffects.PushShockRing(Projectile.Center, 1.1f, 980f, 32);
                GolemScreenEffects.Shake(9f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //辐射弹扇：等角分布，速度适中可穿缝
            int bolts = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive() ? 16 : 12;
            int damage = GolemDirector.ScaleDamage(GolemDirector.UltBurstDamage, CWRRef.GetDeathMode());
            for (int i = 0; i < bolts; i++) {
                float angle = MathHelper.TwoPi * i / bolts + Projectile.identity * 0.31f;
                Vector2 vel = angle.ToRotationVector2() * 7.6f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    ModContent.ProjectileType<GolemSunBolt>(), damage, 0f, Main.myPlayer);
            }
            Projectile.netUpdate = true;
        }

        #region 判定
        public override bool? CanDamage() {
            //只有辐条期造成伤害
            return Elapsed >= RiseEnd && Elapsed < CollapseEnd && SpokeLenScale > 0.15f ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float lenScale = SpokeLenScale;
            if (lenScale < 0.15f) {
                return false;
            }
            float len = SpokeLength * lenScale;
            float angle = SpokeAngle;
            for (int i = 0; i < SpokeCount; i++) {
                float a = angle + MathHelper.TwoPi * i / SpokeCount;
                Vector2 end = Projectile.Center + a.ToRotationVector2() * len;
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, end, SpokeWidth, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            float coreRadius = 130f * CoreScale;
            float fade = Elapsed > CollapseEnd
                ? MathHelper.Clamp(1f - (Elapsed - CollapseEnd) / 26f, 0f, 1f)
                : 1f;

            Effect shader = EffectLoader.GolemSolarFlare?.Value;
            if (shader != null) {
                DrawWithShader(shader, coreRadius, fade);
            }
            else {
                DrawFallback(coreRadius, fade);
            }
            return false;
        }

        private void DrawWithShader(Effect shader, float coreRadius, float fade) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D quad = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //辐条束
            float lenScale = SpokeLenScale;
            if (lenScale > 0.02f) {
                shader.CurrentTechnique = shader.Techniques["BeamTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uProgress"]?.SetValue(lenScale);
                shader.Parameters["uIntensity"]?.SetValue(fade);
                float len = SpokeLength * lenScale;
                for (int i = 0; i < SpokeCount; i++) {
                    float a = SpokeAngle + MathHelper.TwoPi * i / SpokeCount;
                    shader.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(quad, drawPos, null, Color.White, a,
                        new Vector2(0f, quad.Height / 2f),
                        new Vector2(len / quad.Width, SpokeWidth * 3.4f / quad.Height), SpriteEffects.None, 0f);
                }
            }

            //日面
            shader.CurrentTechnique = shader.Techniques["CoreTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(Elapsed / (float)RiseEnd, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(fade);
            shader.CurrentTechnique.Passes[0].Apply();
            float size = coreRadius * 2.6f;
            sb.Draw(quad, drawPos, null, Color.White, 0f, quad.Size() / 2f,
                new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无着色器兜底：多层辉光 + 亮线辐条</summary>
        private void DrawFallback(float coreRadius, float fade) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float lenScale = SpokeLenScale;
            if (lenScale > 0.02f) {
                float len = SpokeLength * lenScale;
                for (int i = 0; i < SpokeCount; i++) {
                    float a = SpokeAngle + MathHelper.TwoPi * i / SpokeCount;
                    Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 170, 60, 0) * (0.85f * fade),
                        a, new Vector2(0f, line.Height / 2f),
                        new Vector2(len / line.Width, 0.5f), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 240, 190, 0) * fade,
                        a, new Vector2(0f, line.Height / 2f),
                        new Vector2(len / line.Width, 0.2f), SpriteEffects.None, 0);
                }
            }

            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 140, 40, 0) * (0.9f * fade),
                0f, glow.Size() / 2f, coreRadius / 22f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 220, 150, 0) * fade,
                0f, glow.Size() / 2f, coreRadius / 34f, SpriteEffects.None, 0);
        }
        #endregion
    }
}
