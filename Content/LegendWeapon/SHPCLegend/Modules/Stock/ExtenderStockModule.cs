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
    /// <summary>
    /// 延伸枪托：延长线切割——光束飞满里程后就地横向展开垂直光刃旋掠处决（不等消亡）；
    /// 提前耗尽穿透而死的光束按已飞距离在死点折算缩水刀，贴脸消亡不触发
    /// </summary>
    internal sealed class ExtenderStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //延伸冷银青
        public override Color TintColor => new(185, 215, 235);

        //═════ 可调参数（平衡位） ═════
        /// <summary>触发终端切割的最小飞行距离（像素），低于此贴脸消亡不触发</summary>
        internal const float MinTriggerDistance = 380f;
        /// <summary>满规格光刃的就地收刀距离（像素）：飞到即触发，不等消亡，单体Boss战也能兑现</summary>
        internal const float FullTriggerDistance = 950f;
        /// <summary>缩水档光刃伤害倍率（×光束面值）</summary>
        internal const float CleaveDamageMulMin = 0.5f;
        /// <summary>满规格光刃伤害倍率</summary>
        internal const float CleaveDamageMulMax = 0.95f;
        /// <summary>激光收束刀所需的最短持续照射帧数</summary>
        internal const int LaserMinHoldTicks = 30;
        /// <summary>激光收束刀伤害倍率（×激光单跳面值）</summary>
        internal const float LaserCleaveDamageMul = 1.25f;
        /// <summary>镜像 CyberPrismLaserProj.MaxRange（私有常量且共享文件禁改，只能镜像取值）</summary>
        private const float LaserRangeMirror = 1600f;
        /// <summary>追踪字典的周期清扫间隔（帧）</summary>
        private const int PurgeInterval = 90;

        //与 CyberTraceBeamProj.Themes 对齐的三阶主题色（等离子青/电蓝/幻紫）
        internal static readonly Color[] ThemeCore = {
            new(110, 255, 235), new(120, 190, 255), new(190, 150, 255),
        };
        internal static readonly Color[] ThemeGlow = {
            new(25, 200, 185), new(40, 115, 235), new(125, 65, 235),
        };

        /// <summary>单束飞行档案：累计路程与切割资格；per-玩家模块实例持有，仅拥有者客户端填充</summary>
        private sealed class BeamTrack
        {
            /// <summary>累计飞行路程（像素），按逐帧位移求和，时缓/追踪转向天然计入</summary>
            public float Distance;
            public Vector2 LastPos;
            /// <summary>已达档位：0 未成型 / 1 缩水档已成型（380px 起）</summary>
            public int Tier;
            public int SideSparkTimer;
            /// <summary>满档就地收刀已完成，该束切割资格耗尽，消亡时不再落刀</summary>
            public bool Consumed;
            /// <summary>弹幕 identity，防 whoAmI 槽位复用串档</summary>
            public int Identity;
        }

        private readonly Dictionary<int, BeamTrack> beamTracks = [];
        private List<int> staleKeys;
        private int purgeTimer;

        //激光持续照射状态：仅拥有者客户端计数，identity 换束即清零
        private int laserHoldTicks;
        private int laserIdentity = -1;

        public override void Apply(ref ShootContext ctx) {
            //"延长射程"的老底子保留但削弱，让位给终端切割机制
            ctx.BeamLifeMul += 0.5f;
            ctx.BeamSpeedMul += 0.2f;
            ctx.DamageMul += -0.08f;
            ctx.ManaCostMul += 0.15f;
        }

        //═════════════ 光束模式：飞行里程追踪 → 消亡结算 ═════════════

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

            //满档就地收刀：飞满里程立刻在当前位置转化为光刃，不等消亡——
            //光束穿墙且寿命预算极长，单体Boss战里穿透耗不尽、等消亡刀只会落在十几屏外；
            //收刀后该束切割资格耗尽，本体继续飞行（射程加成仍然生效）
            if (track.Distance >= FullTriggerDistance) {
                track.Consumed = true;
                int dmg = Math.Max((int)(p.damage * CleaveDamageMulMax), 1);
                SpawnCleave(p, p.Center, beam.FlightDirection, 1f, BeamThemeIndex(p), dmg, p.knockBack);
                return;
            }

            if (track.Distance < MinTriggerDistance) return;

            //缩水档成型播报（一次性）
            if (track.Tier < 1) {
                track.Tier = 1;
                ArmCue(p.Center, BeamThemeIndex(p), 1);
            }

            //蓄势侧向火花：能量沿垂直方向渗出，预告"会横着展开"；只在可见时生成
            if (Main.netMode != NetmodeID.Server) {
                track.SideSparkTimer++;
                //OnBeamAI 每刻走 3~4 次，此处以 AI 调用数计间隔
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
            //无论何种死法都先摘档案，防泄漏（非拥有者端字典本就无该项）
            if (!beamTracks.Remove(p.whoAmI, out BeamTrack track)) return;
            if (beam.IsDerived || p.owner != Main.myPlayer) return;
            //Consumed=已就地收刀；余下是"提前耗尽穿透死在 380~950px 间"的缩水刀路径
            if (track.Consumed || track.Identity != p.identity || track.Distance < MinTriggerDistance) return;

            float charge = MathHelper.Clamp(
                (track.Distance - MinTriggerDistance) / (FullTriggerDistance - MinTriggerDistance), 0f, 1f);
            int dmg = Math.Max((int)(p.damage
                * MathHelper.Lerp(CleaveDamageMulMin, CleaveDamageMulMax, charge)), 1);
            SpawnCleave(p, p.Center, beam.FlightDirection, charge, BeamThemeIndex(p), dmg, p.knockBack);
        }

        //═════════════ 激光模式：持续照射 → 收束横断 ═════════════

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (laser.Projectile.identity != laserIdentity) {
                laserIdentity = laser.Projectile.identity;
                laserHoldTicks = 0;
            }
            laserHoldTicks++;
            if (laserHoldTicks == LaserMinHoldTicks) {
                //达标一声上膛提示，打在落刀点
                ArmCue(LaserCleavePoint(laser.Projectile), 2, 2);
            }
            //达标后落刀点标记持续跟随光标投影：沿未来刀轴的两枚短促光屑，标出"松手会在这里横断"
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

            //激光恒为满功率照射，收束刀固定满规格；主题取激光的幻紫(2)
            int dmg = Math.Max((int)(p.damage * LaserCleaveDamageMul), 1);
            SpawnCleave(p, LaserCleavePoint(p), p.rotation.ToRotationVector2(), 1f, 2, dmg, p.knockBack);
        }

        /// <summary>
        /// 激光收束刀的落点：光标在光柱上的投影（钳制在最小触发距离与满射程之间）；
        /// 光柱几何终点恒在 1600px 外基本不可见，玩家指哪里、刀落哪里才可读
        /// </summary>
        private static Vector2 LaserCleavePoint(Projectile laser) {
            Vector2 dir = laser.rotation.ToRotationVector2();
            float t = Vector2.Dot(Main.MouseWorld - laser.Center, dir);
            t = MathHelper.Clamp(t, MinTriggerDistance, LaserRangeMirror);
            return laser.Center + dir * t;
        }

        //═════════════ 公共结算与反馈 ═════════════

        /// <summary>在终点生成垂直弹道的切割光刃；仅拥有者客户端调用，扫向 roll 经 ai2 下发保证各端一致</summary>
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

        /// <summary>档位上膛提示：满档带一声轻脆提示音，缩水档只有粒子；屏幕外不播报</summary>
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

        /// <summary>周期清扫：模块卸下再装回等边缘情况下的档案残留兜底</summary>
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

    /// <summary>
    /// 终端切割光刃：钉在光束终点、垂直弹道展开的宽刃光刃，快速旋掠扇形区域一次；
    /// 范围内每个敌人只吃一记终结伤害；SHPCModExtenderCleave.fx
    /// </summary>
    internal sealed class SHPCExtenderCleaveProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int SweepFrames = 13;
        private const int TailFrames = 17;
        private const int Lifetime = SweepFrames + TailFrames;
        /// <summary>扫掠半幅（±40°，总扫幅 80°，小于 π 保证着色器楔区判据成立）</summary>
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
        /// <summary>缓出的扫掠进度：起手迅猛、收尾减速的"快速旋掠"</summary>
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
            //一次旋掠对每个敌人只结算一刀
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害窗口只在扫掠期，尾段是纯残光演出</summary>
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
            //横扫对蠕虫一刀可扫 5~10 节，体节折减对齐仓库先例（Heartcarver 0.425 / Halibut 0.65）
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.45f;
            }
            //击退沿扫掠切线：被刀"扫飞"而不是被弹道推走
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

            //扫掠期间沿刃身甩出切割碎光
            if (Main.netMode != NetmodeID.Server && Age <= SweepFrames) {
                SpawnSweepShreds(dir);
            }
        }

        /// <summary>落刀瞬间的定场演出：相位刃声、中心环闪、满档轻震屏</summary>
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

        /// <summary>切割碎光：沿扫掠切线方向甩出，越靠刃尖线速度越大</summary>
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
            //quad 局部空间的起始/当前刀轴（+X=基准刀轴）
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
            //两端刃尖光斑：星芒+光晕，把扫掠运动的两端点亮
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
