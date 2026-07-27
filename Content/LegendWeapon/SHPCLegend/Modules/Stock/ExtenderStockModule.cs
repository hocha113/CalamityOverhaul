using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>延伸枪托，飞满里程就地横切光刃；穿透早死折算缩水刀，贴脸不触发</summary>
    internal sealed class ExtenderStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //延伸冷银青
        public override Color TintColor => new(185, 215, 235);

        //═════ 可调参数 ═════
        /// <summary>最小切割距离 px，低于此贴脸不触发</summary>
        internal const float MinTriggerDistance = 380f;
        /// <summary>满规格就地收刀距离 px，飞到即触发</summary>
        internal const float FullTriggerDistance = 950f;
        /// <summary>缩水档光刃伤害倍率（×光束面值）</summary>
        internal const float CleaveDamageMulMin = 0.35f;
        /// <summary>满规格光刃伤害倍率</summary>
        internal const float CleaveDamageMulMax = 0.65f;
        /// <summary>激光收束刀所需的最短持续照射帧数</summary>
        internal const int LaserMinHoldTicks = 30;
        /// <summary>激光收束刀伤害倍率（×激光单跳面值）</summary>
        internal const float LaserCleaveDamageMul = 0.85f;
        /// <summary>镜像 CyberPrismLaserProj.MaxRange（共享文件禁改）</summary>
        private const float LaserRangeMirror = 1600f;
        /// <summary>追踪字典的周期清扫间隔（帧）</summary>
        private const int PurgeInterval = 90;

        //主题色对齐 CyberTraceBeamProj.Themes
        internal static readonly Color[] ThemeCore = {
            new(110, 255, 235), new(120, 190, 255), new(190, 150, 255),
        };
        internal static readonly Color[] ThemeGlow = {
            new(25, 200, 185), new(40, 115, 235), new(125, 65, 235),
        };

        /// <summary>单束飞行档案，仅 owner 客户端</summary>
        private sealed class BeamTrack
        {
            /// <summary>累计飞行路程 px</summary>
            public float Distance;
            public Vector2 LastPos;
            /// <summary>档位 0未成型 / 1缩水档（380px起）</summary>
            public int Tier;
            public int SideSparkTimer;
            /// <summary>满档已收刀，消亡不再落刀</summary>
            public bool Consumed;
            /// <summary>弹幕 identity，防 whoAmI 复用串档</summary>
            public int Identity;
        }

        private readonly Dictionary<int, BeamTrack> beamTracks = [];
        private List<int> staleKeys;
        private int purgeTimer;

        //激光照射计数，仅 owner，换束清零
        private int laserHoldTicks;
        private int laserIdentity = -1;

        public override void Apply(ref ShootContext ctx) {
            //射程加成削弱，让位终端切割
            ctx.BeamLifeMul += 0.5f;
            ctx.BeamSpeedMul += 0.2f;
            ctx.DamageMul += -0.08f;
            ctx.ManaCostMul += 0.15f;
        }

        //═════════════ 光束里程追踪 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            Projectile p = beam.Projectile;
            if (beam.IsDerived || p.owner != Main.myPlayer) return;

            if (!beamTracks.TryGetValue(p.whoAmI, out BeamTrack track) || track.Identity != p.identity) {
                track = new BeamTrack { LastPos = p.Center, Identity = p.identity };
                beamTracks[p.whoAmI] = track;
            }
            track.Distance += Vector2.Distance(track.LastPos, p.Center);
            track.LastPos = p.Center;

            if (track.Consumed) return;

            //满档就地收刀，不等消亡；收刀后资格耗尽，本体继续飞
            if (track.Distance >= FullTriggerDistance) {
                track.Consumed = true;
                int dmg = Math.Max((int)(p.damage * CleaveDamageMulMax), 1);
                SpawnCleave(p, p.Center, beam.FlightDirection, 1f, BeamThemeIndex(p), dmg, p.knockBack);
                return;
            }

            if (track.Distance < MinTriggerDistance) return;

            //缩水档成型播报
            if (track.Tier < 1) {
                track.Tier = 1;
                ArmCue(p.Center, BeamThemeIndex(p), 1);
            }

            //蓄势侧向火花，可见时
            if (Main.netMode != NetmodeID.Server) {
                track.SideSparkTimer++;
                //按 AI 调用数计间隔（每刻 3~4 次）
                if (track.SideSparkTimer >= 9
                    && VaultUtils.IsPointOnScreen(p.Center - Main.screenPosition, 100)) {
                    track.SideSparkTimer = 0;
                    int theme = BeamThemeIndex(p);
                    Vector2 perp = beam.FlightDirection.RotatedBy(MathHelper.PiOver2)
                        * (Main.rand.NextBool() ? 1f : -1f);
                    PRTLoader.NewParticle<PRT_SHPCExtenderShred>(p.Center,
                        perp * 2.2f, ThemeCore[theme], Main.rand.NextFloat(0.35f, 0.7f))
                        ?.Configure(ThemeGlow[theme], Main.rand.Next(10, 18));
                }
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            Projectile p = beam.Projectile;
            //死前先摘档案
            if (!beamTracks.Remove(p.whoAmI, out BeamTrack track)) return;
            if (beam.IsDerived || beam.SuppressDeathEffects || p.owner != Main.myPlayer) return;
            //Consumed=已收刀；否则 380~950px 缩水刀
            if (track.Consumed || track.Identity != p.identity || track.Distance < MinTriggerDistance) return;

            float charge = MathHelper.Clamp(
                (track.Distance - MinTriggerDistance) / (FullTriggerDistance - MinTriggerDistance), 0f, 1f);
            int dmg = Math.Max((int)(p.damage
                * MathHelper.Lerp(CleaveDamageMulMin, CleaveDamageMulMax, charge)), 1);
            SpawnCleave(p, p.Center, beam.FlightDirection, charge, BeamThemeIndex(p), dmg, p.knockBack);
        }

        //═════════════ 激光收束横断 ═════════════

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (laser.Projectile.identity != laserIdentity) {
                laserIdentity = laser.Projectile.identity;
                laserHoldTicks = 0;
            }
            laserHoldTicks++;
            if (laserHoldTicks == LaserMinHoldTicks) {
                //达标上膛提示
                ArmCue(LaserCleavePoint(laser.Projectile), 2, 2);
            }
            //落刀点跟光标投影
            if (laserHoldTicks >= LaserMinHoldTicks && Main.netMode != NetmodeID.Server
                && laserHoldTicks % 3 == 0) {
                Vector2 point = LaserCleavePoint(laser.Projectile);
                Vector2 perp = laser.Projectile.rotation.ToRotationVector2()
                    .RotatedBy(MathHelper.PiOver2);
                for (int s = -1; s <= 1; s += 2) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(
                        point + perp * (s * Main.rand.NextFloat(8f, 34f)),
                        perp * (s * 0.8f), ThemeCore[2], 0.55f)?.Configure(ThemeGlow[2], 6);
                }
            }
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            Projectile p = laser.Projectile;
            if (p.owner != Main.myPlayer) return;
            bool fire = p.identity == laserIdentity && laserHoldTicks >= LaserMinHoldTicks;
            laserHoldTicks = 0;
            laserIdentity = -1;
            if (!fire) return;

            //激光收束刀满规格，主题幻紫(2)
            int dmg = Math.Max((int)(p.damage * LaserCleaveDamageMul), 1);
            SpawnCleave(p, LaserCleavePoint(p), p.rotation.ToRotationVector2(), 1f, 2, dmg, p.knockBack);
        }

        /// <summary>激光收束刀落点，光标在光柱上的投影</summary>
        private static Vector2 LaserCleavePoint(Projectile laser) {
            Vector2 dir = laser.rotation.ToRotationVector2();
            float t = Vector2.Dot(Main.MouseWorld - laser.Center, dir);
            t = MathHelper.Clamp(t, MinTriggerDistance, LaserRangeMirror);
            return laser.Center + dir * t;
        }

        //═════════════ 公共结算与反馈 ═════════════

        /// <summary>终点生成切割光刃，仅 owner，扫向 ai2 同步</summary>
        private static void SpawnCleave(Projectile source, Vector2 pos, Vector2 flightDir,
            float charge, int theme, int damage, float knockback) {
            float baseAxis = flightDir.ToRotation() + MathHelper.PiOver2;
            int sweepDir = Main.rand.NextBool() ? 1 : -1;
            Projectile.NewProjectile(source.GetSource_FromThis(),
                pos, Vector2.Zero,
                ModContent.ProjectileType<SHPCExtenderCleaveProj>(),
                damage, knockback, source.owner,
                ai0: baseAxis, ai1: charge, ai2: sweepDir * (theme + 1));
        }

        /// <summary>档位上膛提示，屏外不播</summary>
        private static void ArmCue(Vector2 pos, int theme, int tier) {
            if (Main.netMode == NetmodeID.Server) return;
            if (!VaultUtils.IsPointOnScreen(pos - Main.screenPosition, 150)) return;
            Color core = ThemeCore[theme];
            Color glow = ThemeGlow[theme];
            if (tier >= 2) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.24f, Pitch = 0.45f }, pos);
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero,
                    new Color(core.R, core.G, core.B, 0), 0.04f)?.Configure(0.04f, 0.2f, 14);
            }
            int count = tier >= 2 ? 6 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 vel = (MathHelper.TwoPi * i / count).ToRotationVector2() * 2.2f;
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, vel, core,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(glow, 14);
            }
        }

        /// <summary>周期清扫档案残留</summary>
        public override void OnPlayerUpdate(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer || beamTracks.Count == 0) return;
            if (++purgeTimer < PurgeInterval) return;
            purgeTimer = 0;
            staleKeys ??= [];
            staleKeys.Clear();
            foreach ((int who, BeamTrack track) in beamTracks) {
                if (who < 0 || who >= Main.maxProjectiles) {
                    staleKeys.Add(who);
                    continue;
                }
                Projectile p = Main.projectile[who];
                if (!p.active || p.identity != track.Identity
                    || p.owner != Main.myPlayer || p.ModProjectile is not CyberTraceBeamProj) {
                    staleKeys.Add(who);
                }
            }
            foreach (int k in staleKeys) {
                beamTracks.Remove(k);
            }
        }

        private static int BeamThemeIndex(Projectile beam) => Math.Clamp((int)beam.ai[0] % 3, 0, 2);
    }

    /// <summary>终端切割光刃，旋掠一次一刀；SHPCModExtenderCleave.fx</summary>
    internal sealed class SHPCExtenderCleaveProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int SweepFrames = 13;
        private const int TailFrames = 17;
        private const int Lifetime = SweepFrames + TailFrames;
        /// <summary>扫掠半幅 ±40°</summary>
        private const float SweepHalfArc = MathHelper.Pi * 40f / 180f;
        private const float HalfLenMin = 120f;
        private const float HalfLenMax = 215f;
        private const float BladeHitWidth = 30f;

        private float BaseAxis => Projectile.ai[0];
        private float Charge => Projectile.ai[1];
        private int SweepSign => Projectile.ai[2] >= 0f ? 1 : -1;
        private int ThemeIndex => Math.Clamp((int)MathF.Abs(Projectile.ai[2]) - 1, 0, 2);

        private float HalfLen => MathHelper.Lerp(HalfLenMin, HalfLenMax, Charge);
        private int Age => Lifetime - Projectile.timeLeft;
        /// <summary>扫掠进度缓出</summary>
        private float SweepT {
            get {
                float x = MathHelper.Clamp(Age / (float)SweepFrames, 0f, 1f);
                return 1f - MathF.Pow(1f - x, 3f);
            }
        }
        private float CurrentDelta => SweepSign * SweepHalfArc * (2f * SweepT - 1f);
        private Vector2 BladeDir => (BaseAxis + CurrentDelta).ToRotationVector2();
        private float FadeAlpha => Projectile.timeLeft > TailFrames
            ? 1f : Projectile.timeLeft / (float)TailFrames;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            //每敌只结算一刀
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害只在扫掠期</summary>
        public override bool? CanDamage() => Projectile.timeLeft > TailFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = BladeDir;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                Projectile.Center - dir * HalfLen, Projectile.Center + dir * HalfLen,
                BladeHitWidth, ref _);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //蠕虫体节折减（Heartcarver 0.425 / Halibut 0.65）
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.45f;
            }
            //击退沿扫掠切线
            Vector2 tangent = BladeDir.RotatedBy(MathHelper.PiOver2 * SweepSign);
            if (MathF.Abs(tangent.X) > 0.25f) {
                modifiers.HitDirectionOverride = tangent.X >= 0f ? 1 : -1;
            }
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = BaseAxis;
                SpawnFlashFX();
            }

            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            float fade = FadeAlpha;
            Lighting.AddLight(Projectile.Center, core.ToVector3() * 0.85f * fade);
            Vector2 dir = BladeDir;
            Lighting.AddLight(Projectile.Center + dir * HalfLen * 0.85f, core.ToVector3() * 0.4f * fade);
            Lighting.AddLight(Projectile.Center - dir * HalfLen * 0.85f, core.ToVector3() * 0.4f * fade);

            //扫掠碎光
            if (Main.netMode != NetmodeID.Server && Age <= SweepFrames) {
                SpawnSweepShreds(dir);
            }
        }

        /// <summary>落刀定场演出</summary>
        private void SpawnFlashFX() {
            if (Main.netMode == NetmodeID.Server) return;
            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            Color glow = ExtenderStockModule.ThemeGlow[ThemeIndex];

            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.3f + Charge * 0.15f },
                Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.05f }, Projectile.Center);

            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(core.R, core.G, core.B, 0), 0.05f)
                ?.Configure(0.05f, 0.3f + Charge * 0.25f, 18);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, core,
                    Main.rand.NextFloat(0.7f, 1.4f))?.Configure(glow, Main.rand.Next(16, 30));
            }

            if (Charge > 0.85f && CWRServerConfig.Instance.ScreenVibration) {
                Vector2 tangent = BladeDir.RotatedBy(MathHelper.PiOver2 * SweepSign);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                    tangent, 4.5f, 7f, 10, 900f, FullName));
            }
        }

        /// <summary>切割碎光，沿切线甩出</summary>
        private void SpawnSweepShreds(Vector2 dir) {
            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            Color glow = ExtenderStockModule.ThemeGlow[ThemeIndex];
            Vector2 tangent = dir.RotatedBy(MathHelper.PiOver2 * SweepSign);
            int count = Charge > 0.7f ? 3 : 2;
            for (int i = 0; i < count; i++) {
                float t = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = Projectile.Center + dir * (t * HalfLen);
                float speed = 2f + MathF.Abs(t) * 6f;
                PRTLoader.NewParticle<PRT_SHPCExtenderShred>(pos,
                    tangent * speed + dir * (t * 0.8f), core,
                    Main.rand.NextFloat(0.5f, 1.1f))?.Configure(glow, Main.rand.Next(16, 30));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            Color glow = ExtenderStockModule.ThemeGlow[ThemeIndex];
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.3f, Pitch = 0.35f }, target.Center);
            Vector2 tangent = BladeDir.RotatedBy(MathHelper.PiOver2 * SweepSign);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = tangent.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f))
                    * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_SHPCExtenderShred>(target.Center, vel, core,
                    Main.rand.NextFloat(0.5f, 1.0f))?.Configure(glow, Main.rand.Next(14, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCModExtenderCleave?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            Color glow = ExtenderStockModule.ThemeGlow[ThemeIndex];
            //quad 局部刀轴，+X=基准
            float delta0 = -SweepSign * SweepHalfArc;
            float deltaCur = CurrentDelta;
            float drawSize = HalfLen * 2.7f;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["sweepT"]?.SetValue(SweepT);
            shader.Parameters["lifeT"]?.SetValue(MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f));
            shader.Parameters["fadeAlpha"]?.SetValue(FadeAlpha);
            shader.Parameters["dir0"]?.SetValue(new Vector2(MathF.Cos(delta0), MathF.Sin(delta0)));
            shader.Parameters["dirCur"]?.SetValue(new Vector2(MathF.Cos(deltaCur), MathF.Sin(deltaCur)));
            shader.Parameters["bladeHalfLen"]?.SetValue(HalfLen / (drawSize * 0.5f));
            shader.Parameters["charge"]?.SetValue(Charge);
            shader.Parameters["coreColor"]?.SetValue(Color.Lerp(core, Color.White, 0.35f).ToVector3());
            shader.Parameters["mainColor"]?.SetValue(core.ToVector3());
            shader.Parameters["deepColor"]?.SetValue(glow.ToVector3());
            shader.Parameters["dispColorA"]?.SetValue(new Vector3(0.27f, 0.94f, 1.0f));
            shader.Parameters["dispColorB"]?.SetValue(new Vector3(1.0f, 0.35f, 0.92f));

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                BaseAxis, canvas.Size() * 0.5f,
                new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float fade = FadeAlpha;
            if (fade < 0.02f) return;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Color core = ExtenderStockModule.ThemeCore[ThemeIndex];
            Color glow = ExtenderStockModule.ThemeGlow[ThemeIndex];
            Vector2 dir = BladeDir;
            float bladeRot = dir.ToRotation();

            //中心能量核
            if (glowTex != null) {
                spriteBatch.Draw(glowTex, Projectile.Center - Main.screenPosition, null,
                    glow * (0.5f * fade), 0f, glowTex.Size() * 0.5f,
                    0.7f + Charge * 0.4f, SpriteEffects.None, 0f);
            }
            //两端刃尖光斑
            for (int s = -1; s <= 1; s += 2) {
                Vector2 tipPos = Projectile.Center + dir * (HalfLen * s) - Main.screenPosition;
                if (glowTex != null) {
                    spriteBatch.Draw(glowTex, tipPos, null, core * (0.55f * fade), 0f,
                        glowTex.Size() * 0.5f, 0.32f, SpriteEffects.None, 0f);
                }
                if (star != null) {
                    spriteBatch.Draw(star, tipPos, null,
                        Color.Lerp(core, Color.White, 0.4f) * (0.85f * fade), bladeRot,
                        star.Size() * 0.5f, 0.055f + Charge * 0.025f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
