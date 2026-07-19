using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 共振机匣：驻波节拍器——射击计数，每第 N 发升格为共振节拍束（显著增粗+驻波护层+增伤），
    /// 节拍束首次命中掀起驻波震荡环（范围伤害+轻推）；跟稳节奏连续打拍叠节奏层小幅提升节拍威力。
    /// 激光模式按固定节拍周期蓄振染色，节拍窗口内命中打出震荡环
    /// </summary>
    internal sealed class ResonanceFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //节拍洋红
        public override Color TintColor => new(255, 100, 175);

        #region 可调参数

        /// <summary>每第几次射击升格为节拍束</summary>
        private const int BeatEvery = 4;
        /// <summary>节拍束基础伤害倍率</summary>
        private const float BeatDamageMul = 1.6f;
        /// <summary>每层节奏追加的节拍伤害倍率</summary>
        private const float TempoDamageStep = 0.15f;
        /// <summary>节奏层上限</summary>
        private const int MaxTempo = 3;
        /// <summary>节拍束额外穿透</summary>
        private const int BeatExtraPierce = 1;
        /// <summary>两次射击间隔超过此帧数视为断拍，节奏层清零</summary>
        private const int TempoBreakFrames = 90;
        /// <summary>震荡环基础半径（像素）</summary>
        private const float RingBaseRadius = 150f;
        /// <summary>每层节奏追加的环半径</summary>
        private const float RingRadiusPerTempo = 15f;
        /// <summary>震荡环伤害 = 节拍束伤害 × 此值</summary>
        private const float RingDamageRatio = 0.45f;
        /// <summary>震荡环击退（轻推）</summary>
        private const float RingKnockback = 2.5f;
        /// <summary>激光模式节拍周期（帧）</summary>
        private const int LaserBeatPeriod = 48;
        /// <summary>激光节拍染色/取拍窗口（帧）</summary>
        private const int LaserFlashFrames = 14;
        /// <summary>激光震荡环半径（像素）</summary>
        private const float LaserRingRadius = 130f;
        /// <summary>激光震荡环伤害 = 激光伤害 × 此值</summary>
        private const float LaserRingDamageRatio = 0.6f;

        #endregion

        //驻波节拍配色：洋红三阶，与同槽量子紫/谐振绿明确区分
        internal static readonly Color BeatBright = new(255, 170, 220);
        internal static readonly Color BeatMain = new(240, 80, 165);
        internal static readonly Color BeatDeep = new(130, 20, 85);

        //═════ 每玩家节拍器状态（模块实例即每玩家实例，不入 static） ═════

        /// <summary>自上一节拍以来的射击数，达 BeatEvery 时该发为节拍束</summary>
        private int _shotsSinceBeat;
        /// <summary>节奏层 0~MaxTempo，断拍清零</summary>
        private int _tempo;
        /// <summary>距上次射击的帧数，判定断拍；按 TimeGear 推进</summary>
        private int _framesSinceShot = 100000;
        private float _frameCarry;
        /// <summary>上次登记射击的帧号，同帧多束视作同一次扣扳机</summary>
        private uint _lastVolleyTick = uint.MaxValue;

        private struct BeamEntry
        {
            /// <summary>节奏层，-1=平拍束</summary>
            public int Tempo;
            /// <summary>identity 校验，防 whoAmI 槽位复用串台</summary>
            public int Identity;
        }
        /// <summary>已登记的原生光束：whoAmI → 节拍信息</summary>
        private readonly Dictionary<int, BeamEntry> _beams = new();

        private struct LaserBeatState
        {
            public int Identity;
            public int Timer;
            public int Flash;
            public bool Armed;
        }
        /// <summary>激光节拍状态：whoAmI → 计时/染色窗/取拍待命</summary>
        private readonly Dictionary<int, LaserBeatState> _lasers = new();
        /// <summary>字典周期清扫计时，防泄漏</summary>
        private int _sweepTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.05f;
            ctx.ManaCostMul += 0.22f;
        }

        #region 光束节拍

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            int id = beam.Projectile.whoAmI;
            if (_beams.TryGetValue(id, out BeamEntry existing)
                && existing.Identity == beam.Projectile.identity) {
                return; //已登记
            }

            //首见新光束＝一次射击登记；同一帧的散射姊妹束归入同一次扣扳机
            int beatTempo = -1;
            if (Main.GameUpdateCount != _lastVolleyTick) {
                _lastVolleyTick = Main.GameUpdateCount;
                if (RegisterShot(beam, out int tempoUsed)) {
                    beatTempo = tempoUsed;
                }
            }
            _beams[id] = new BeamEntry { Tempo = beatTempo, Identity = beam.Projectile.identity };
            if (beatTempo >= 0) {
                UpgradeToBeatBeam(beam, beatTempo);
            }
        }

        /// <summary>推进节拍器一格；返回该发是否为节拍，tempoUsed 为本拍生效的节奏层</summary>
        private bool RegisterShot(CyberTraceBeamProj beam, out int tempoUsed) {
            tempoUsed = 0;
            //断拍判定先于计数：停火过久节奏层归零（射击计数保留，节拍位置不漂移）
            if (_framesSinceShot > TempoBreakFrames) {
                _tempo = 0;
            }
            _framesSinceShot = 0;
            _frameCarry = 0f;

            Vector2 muzzle = beam.Projectile.Center;
            _shotsSinceBeat++;
            if (_shotsSinceBeat < BeatEvery) {
                //平拍：渐强渐高的节拍器嘀嗒，铺垫下一记重拍
                if (Main.netMode != NetmodeID.Server) {
                    float progress = _shotsSinceBeat / (float)BeatEvery;
                    SoundEngine.PlaySound(SoundID.DrumHiHat with {
                        Volume = 0.28f + progress * 0.1f,
                        Pitch = -0.1f + progress * 0.55f
                    }, muzzle);
                }
                return false;
            }

            //节拍落下
            _shotsSinceBeat = 0;
            tempoUsed = _tempo;
            int newTempo = Math.Min(_tempo + 1, MaxTempo);
            if (Main.netMode != NetmodeID.Server) {
                //定音鼓主拍 + 底鼓补点
                SoundEngine.PlaySound(SoundID.DrumFloorTom with { Volume = 0.85f, Pitch = 0.12f + tempoUsed * 0.05f }, muzzle);
                SoundEngine.PlaySound(SoundID.DrumKick with { Volume = 0.5f, Pitch = 0.2f }, muzzle);
                PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero,
                    BeatBright with { A = 0 }, 0.05f).Configure(0.05f, 0.3f + tempoUsed * 0.04f, 14);
                Vector2 dir = beam.FlightDirection;
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(muzzle, vel, BeatBright,
                        Main.rand.NextFloat(0.7f, 1.3f)).Configure(BeatMain, Main.rand.Next(14, 24));
                }
                SHPCNaturalFx.Shake(1.5f);
                if (newTempo > _tempo) {
                    Player owner = Main.player[beam.Projectile.owner];
                    if (owner != null && owner.active) {
                        CombatText.NewText(owner.getRect(), BeatMain, $"// TEMPO {newTempo}", true, false);
                    }
                }
            }
            _tempo = newTempo;
            return true;
        }

        /// <summary>升格节拍束：增伤+穿透，并挂上驻波护层弹幕</summary>
        private void UpgradeToBeatBeam(CyberTraceBeamProj beam, int beatTempo) {
            float mul = BeatDamageMul + beatTempo * TempoDamageStep;
            beam.Projectile.damage = Math.Max((int)(beam.Projectile.damage * mul), 1);
            beam.Projectile.penetrate += BeatExtraPierce;
            //ai0 传 identity（跨端稳定），ai1 传节奏层强度
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCResonanceWaveProj>(),
                0, 0f, beam.Projectile.owner,
                ai0: beam.Projectile.identity,
                ai1: beatTempo / (float)MaxTempo);
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (!_beams.TryGetValue(beam.Projectile.whoAmI, out BeamEntry entry)
                || entry.Identity != beam.Projectile.identity
                || entry.Tempo < 0) {
                return;
            }
            //每束节拍只在首个命中打一次拍，穿透后续不重复掀环
            if (beam.Projectile.numHits > 0) return;

            int dmg = Math.Max((int)(beam.Projectile.damage * RingDamageRatio), 1);
            float radius = RingBaseRadius + entry.Tempo * RingRadiusPerTempo;
            SpawnResonanceRing(beam.Projectile, target.Center, dmg, radius, entry.Tempo / (float)MaxTempo);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _beams.Remove(beam.Projectile.whoAmI);
        }

        #endregion

        #region 激光节拍

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            int id = laser.Projectile.whoAmI;
            LaserBeatState st = _lasers.TryGetValue(id, out LaserBeatState s) && s.Identity == laser.Projectile.identity
                ? s
                : new LaserBeatState { Identity = laser.Projectile.identity };
            st.Timer++;
            if (st.Flash > 0) {
                st.Flash--;
                if (st.Flash == 0) st.Armed = false; //取拍窗口关闭，节拍过期作废
            }
            if (st.Timer >= LaserBeatPeriod) {
                st.Timer = 0;
                st.Flash = LaserFlashFrames;
                st.Armed = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.DrumFloorTom with { Volume = 0.5f, Pitch = 0.25f }, laser.Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(laser.Projectile.Center, Vector2.Zero,
                        BeatBright with { A = 0 }, 0.05f).Configure(0.05f, 0.2f, 12);
                }
            }
            //节拍窗口内洋红蓄振染色，随窗口衰减淡回原配色
            if (st.Flash > 0) {
                float f = st.Flash / (float)LaserFlashFrames;
                laser.ThemeCore = Color.Lerp(laser.ThemeCore, BeatBright, f);
                laser.ThemeGlow = Color.Lerp(laser.ThemeGlow, BeatMain, f);
                laser.ThemeAura = Color.Lerp(laser.ThemeAura, BeatDeep, f);
                laser.ThemeParticleMain = Color.Lerp(laser.ThemeParticleMain, BeatBright, f);
                laser.ThemeParticleEdge = Color.Lerp(laser.ThemeParticleEdge, BeatMain, f);
            }
            _lasers[id] = st;
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            int id = laser.Projectile.whoAmI;
            if (!_lasers.TryGetValue(id, out LaserBeatState st)
                || st.Identity != laser.Projectile.identity
                || !st.Armed) {
                return;
            }
            //窗口内首个命中取走本拍
            st.Armed = false;
            _lasers[id] = st;

            int dmg = Math.Max((int)(laser.Projectile.damage * LaserRingDamageRatio), 1);
            SpawnResonanceRing(laser.Projectile, target.Center, dmg, LaserRingRadius, 0.3f);
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            _lasers.Remove(laser.Projectile.whoAmI);
        }

        #endregion

        /// <summary>命中点掀起驻波震荡环（范围伤害+轻推）</summary>
        private static void SpawnResonanceRing(Projectile source, Vector2 center, int damage, float radius, float waveBoost) {
            Projectile.NewProjectile(source.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<SHPCResonanceRingProj>(),
                damage, RingKnockback, source.owner,
                ai0: radius, ai1: waveBoost);
        }

        #region 每帧维护与蓄振预兆

        public override void OnPlayerUpdate(Player player) {
            //清扫对所有实例执行：远端玩家实例的 _lasers 也会被 OnLaserAI（染色路径）写入
            SweepDead();
            if (player.whoAmI != Main.myPlayer) return;
            if (_framesSinceShot < 100000) {
                _framesSinceShot += TickUp(ref _frameCarry);
            }
            if (!player.active || player.dead) return;
            SpawnBuildupFx(player);
        }

        /// <summary>节拍临近的枪口蓄振粒子与临界提示音</summary>
        private void SpawnBuildupFx(Player player) {
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            //激光/蓄力通道模式没有离散射击节拍，不给预兆
            if (player.channel) return;
            if (_shotsSinceBeat <= 0 || _framesSinceShot > TempoBreakFrames) return;

            float t = _shotsSinceBeat / (float)(BeatEvery - 1);
            bool primed = _shotsSinceBeat >= BeatEvery - 1;
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 muzzle = player.GetPlayerStabilityCenter() + aim * 52f;

            //蓄振粒子向枪口收束，越临近节拍越密
            if (Main.rand.NextFloat() < 0.2f + t * 0.5f) {
                Vector2 from = muzzle + Main.rand.NextVector2CircularEdge(36f, 36f);
                PRTLoader.NewParticle<PRT_CyberConverge>(from, Vector2.Zero,
                    Color.Lerp(BeatDeep, BeatMain, t), Main.rand.NextFloat(0.4f, 0.8f))
                    .Configure(muzzle, BeatDeep, Main.rand.Next(10, 16), t);
            }
            if (primed) {
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(muzzle + Main.rand.NextVector2Circular(8f, 8f),
                        aim * Main.rand.NextFloat(0.5f, 1.5f), BeatBright,
                        Main.rand.NextFloat(0.4f, 0.8f)).Configure(BeatMain, Main.rand.Next(8, 14));
                }
                //渐强提示音的临界闪音
                if (Main.rand.NextBool(20)) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.2f, Pitch = 0.4f }, muzzle);
                }
            }
        }

        /// <summary>周期清扫失效字典项（改件被卸下时 OnBeamKill 不再触达本模块，防泄漏）</summary>
        private void SweepDead() {
            if (++_sweepTimer < 150) return;
            _sweepTimer = 0;
            if (_beams.Count > 0) {
                int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
                List<int> dead = null;
                foreach ((int key, BeamEntry entry) in _beams) {
                    Projectile p = Main.projectile[key];
                    if (!p.active || p.type != beamType || p.identity != entry.Identity) {
                        (dead ??= new List<int>()).Add(key);
                    }
                }
                if (dead != null) foreach (int key in dead) _beams.Remove(key);
            }
            if (_lasers.Count > 0) {
                int laserType = ModContent.ProjectileType<CyberPrismLaserProj>();
                List<int> dead = null;
                foreach ((int key, LaserBeatState st) in _lasers) {
                    Projectile p = Main.projectile[key];
                    if (!p.active || p.type != laserType || p.identity != st.Identity) {
                        (dead ??= new List<int>()).Add(key);
                    }
                }
                if (dead != null) foreach (int key in dead) _lasers.Remove(key);
            }
        }

        #endregion
    }

    /// <summary>
    /// 节拍束驻波护层：跟随宿主光束绘制驻波纹理宽束（SHPCModResonanceWave.fx），
    /// ai0=宿主 identity（跨端稳定），ai1=节奏层 0~1；纯视觉无伤害
    /// </summary>
    internal sealed class SHPCResonanceWaveProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int TrailLen = 42;
        private const float PointSpacing = 10f;
        private const int FadeOutFrames = 12;
        /// <summary>驻波时间角频率（帧域），与着色器 uTime(0.045/帧)×6 保持同频</summary>
        private const float WaveOmega = 0.27f;

        private Vector2[] history;   //[0]=最新记录点
        private int historyCount;
        private Vector2[] drawBuffer;
        private int validCount;
        private Trail trail;
        private float fadeIn;
        /// <summary>-1=跟随中；>=0 为剩余淡出帧</summary>
        private int fadeOut = -1;

        private int HostIdentity => (int)Projectile.ai[0];
        private float TempoBoost => Projectile.ai[1];
        private float Alpha => fadeIn * (fadeOut < 0 ? 1f : fadeOut / (float)FadeOutFrames);

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按 identity 定位宿主光束；localAI[1] 缓存槽位+1，失效时重扫描</summary>
        private Projectile ResolveHost() {
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            int cached = (int)Projectile.localAI[1] - 1;
            if (cached >= 0 && cached < Main.maxProjectiles) {
                Projectile p = Main.projectile[cached];
                if (p.active && p.identity == HostIdentity && p.type == beamType && p.owner == Projectile.owner) {
                    return p;
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == beamType && p.identity == HostIdentity) {
                    Projectile.localAI[1] = i + 1;
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            Projectile host = ResolveHost();
            if (host != null) {
                Projectile.Center = host.Center;
                Projectile.timeLeft = 60;
                RecordTrail(host.Center);
                fadeIn = MathF.Min(fadeIn + 0.15f, 1f);

                Lighting.AddLight(Projectile.Center, ResonanceFrameModule.BeatMain.ToVector3() * 0.7f * Alpha);
                //沿护层随机蹦出洋红驻波微粒
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3) && historyCount > 4) {
                    Vector2 pos = history[Main.rand.Next(historyCount)];
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2Circular(1.6f, 1.6f),
                        ResonanceFrameModule.BeatBright, Main.rand.NextFloat(0.4f, 0.9f))
                        .Configure(ResonanceFrameModule.BeatMain, Main.rand.Next(8, 16));
                }
            }
            else if (fadeIn > 0f) {
                //宿主消亡：护层原地驻留淡出
                if (fadeOut < 0) fadeOut = FadeOutFrames;
                else fadeOut--;
                if (fadeOut <= 0) {
                    Projectile.Kill();
                    return;
                }
                Projectile.timeLeft = 2;
            }
            //宿主尚未同步到本端：静默等待 timeLeft 自然耗尽
        }

        /// <summary>以固定间距记录拖尾点，段间插值避免整帧位移导致的折线粗糙</summary>
        private void RecordTrail(Vector2 pos) {
            history ??= new Vector2[TrailLen];
            if (historyCount == 0) {
                history[0] = pos;
                historyCount = 1;
                return;
            }
            int guard = 0;
            while (Vector2.DistanceSquared(pos, history[0]) >= PointSpacing * PointSpacing && guard++ < 8) {
                Vector2 next = history[0] + (pos - history[0]).SafeNormalize(Vector2.Zero) * PointSpacing;
                int copyLen = Math.Min(historyCount, TrailLen - 1);
                Array.Copy(history, 0, history, 1, copyLen);
                history[0] = next;
                if (historyCount < TrailLen) historyCount++;
            }
        }

        private float WidthFunction(float progress) {
            //有效顶点区间内收尾，参考 CyberTraceBeamProj 的断尾处理
            float validRatio = MathF.Max((float)validCount / TrailLen, 0.05f);
            float p = MathHelper.Clamp(progress / validRatio, 0f, 1f);
            float noseRise = MathF.Sin(MathF.Min(p / 0.07f, 1f) * MathHelper.PiOver2);
            float tailTaper = 1f - MathF.Pow(p, 2f);
            return MathF.Max(noseRise * tailTaper, 0f) * (56f + TempoBoost * 14f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (history == null || Alpha < 0.02f) return;
            Effect shader = EffectLoader.SHPCModResonanceWave?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            drawBuffer ??= new Vector2[TrailLen];
            drawBuffer[0] = Projectile.Center;
            for (int i = 1; i < TrailLen; i++) {
                int histIdx = i - 1;
                drawBuffer[i] = histIdx < historyCount ? history[histIdx] : drawBuffer[i - 1];
            }
            validCount = Math.Min(historyCount + 1, TrailLen);
            if (validCount < 3) return;

            trail ??= new Trail(drawBuffer, WidthFunction, ColorFunction);
            trail.TrailPositions = drawBuffer;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.045f);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(Alpha, 0f, 1f));
            shader.Parameters["waveBoost"]?.SetValue(TempoBoost);
            shader.Parameters["beatBright"]?.SetValue(ResonanceFrameModule.BeatBright.ToVector3());
            shader.Parameters["beatMain"]?.SetValue(ResonanceFrameModule.BeatMain.ToVector3());
            shader.Parameters["beatDeep"]?.SetValue(ResonanceFrameModule.BeatDeep.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float a = Alpha;
            if (a < 0.02f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            //头部光球随驻波频率鼓动，与护层着色器同频呼吸
            float osc = MathF.Abs(MathF.Cos((float)Main.timeForVisualEffects * WaveOmega));
            float pulse = 0.85f + 0.35f * osc;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            spriteBatch.Draw(glow, drawPos, null,
                (ResonanceFrameModule.BeatMain * (0.5f * a)) with { A = 0 }, 0f,
                origin, 1.5f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null,
                (ResonanceFrameModule.BeatBright * (0.75f * a)) with { A = 0 }, 0f,
                origin, 0.8f * pulse, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 驻波震荡环：节拍命中掀起的一次性扩张环形 AoE（范围伤害+外推轻击退），
    /// ai0=最大半径（像素），ai1=节奏层 0~1；SHPCModResonanceRing.fx
    /// </summary>
    internal sealed class SHPCResonanceRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 26;
        /// <summary>环前沿判定厚度（像素）</summary>
        private const float HitBand = 44f;

        private float MaxRadius => MathF.Max(Projectile.ai[0], 60f);
        private float WaveBoost => Projectile.ai[1];
        private float Progress => 1f - Projectile.timeLeft / (float)Lifetime;
        private float CurrentRadius => MaxRadius * (1f - MathF.Pow(1f - Progress, 2.6f));

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一环对每个敌人只结算一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    //命中打拍：能量炸响+高音鼓点
                    SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DrumTomHigh with { Volume = 0.45f, Pitch = 0.1f }, Projectile.Center);
                    SpawnBurstFx();
                    //震荡波屏震随本地玩家与波心距离衰减（全局约定：不满幅震旁观者）
                    float falloff = 1f - MathHelper.Clamp(Main.LocalPlayer.Distance(Projectile.Center) / 900f, 0f, 1f);
                    SHPCNaturalFx.Shake(3f * falloff);
                }
            }

            float fade = FadeAlpha();
            Lighting.AddLight(Projectile.Center, ResonanceFrameModule.BeatMain.ToVector3() * 0.8f * fade);

            //扩张前沿蹦驻波火花
            if (Main.netMode != NetmodeID.Server && Progress < 0.7f) {
                for (int i = 0; i < 2; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * CurrentRadius;
                    Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f);
                    PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                        Color.Lerp(ResonanceFrameModule.BeatMain, ResonanceFrameModule.BeatBright, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, Main.rand.Next(8, 16));
                }
            }
        }

        private void SpawnBurstFx() {
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                ResonanceFrameModule.BeatBright with { A = 0 }, 0.05f)
                .Configure(0.05f, MaxRadius / 380f, 16);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5.5f, 5.5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    ResonanceFrameModule.BeatBright, Main.rand.NextFloat(0.6f, 1.2f))
                    .Configure(false, Main.rand.Next(12, 22));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                    ResonanceFrameModule.BeatMain, Main.rand.NextFloat(0.6f, 1.2f))
                    .Configure(ResonanceFrameModule.BeatDeep, Main.rand.Next(14, 24));
            }
        }

        public override bool? CanDamage() => Progress <= 0.7f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //环形前沿判定：目标包围盒与 [r-带宽, r+带宽] 圆环相交
            float r = CurrentRadius;
            Vector2 c = Projectile.Center;
            Vector2 nearest = new(MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom));
            float nearDist = Vector2.Distance(c, nearest);
            float fx = MathF.Max(MathF.Abs(c.X - targetHitbox.Left), MathF.Abs(c.X - targetHitbox.Right));
            float fy = MathF.Max(MathF.Abs(c.Y - targetHitbox.Top), MathF.Abs(c.Y - targetHitbox.Bottom));
            float farDist = MathF.Sqrt(fx * fx + fy * fy);
            float half = HitBand * 0.5f;
            return nearDist <= r + half && farDist >= r - half;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退方向沿环心向外
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
            //蠕虫体节折减：环扫过共血长虫时每节各结算一次，压制多节总伤尖峰（对齐延伸枪托 0.45 口径）
            if (target.realLife >= 0 && target.realLife != target.whoAmI) {
                modifiers.FinalDamage *= 0.45f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                    Main.rand.NextVector2CircularEdge(4f, 4f),
                    ResonanceFrameModule.BeatBright, Main.rand.NextFloat(0.6f, 1.2f))
                    .Configure(ResonanceFrameModule.BeatMain, Main.rand.Next(10, 20));
            }
        }

        private float FadeAlpha() {
            float t = Progress;
            if (t < 0.12f) return t / 0.12f;
            if (t > 0.6f) return 1f - (t - 0.6f) / 0.4f;
            return 1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCModResonanceRing?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            //向外留辉光带，环进度按绘制半径归一
            float drawRadius = MaxRadius * 1.15f;
            float fade = FadeAlpha();

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.045f);
            shader.Parameters["ringProgress"]?.SetValue(CurrentRadius / drawRadius);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["waveBoost"]?.SetValue(WaveBoost);
            shader.Parameters["wavePhase"]?.SetValue(Progress * 9.42f);
            shader.Parameters["beatBright"]?.SetValue(ResonanceFrameModule.BeatBright.ToVector3());
            shader.Parameters["beatMain"]?.SetValue(ResonanceFrameModule.BeatMain.ToVector3());
            shader.Parameters["beatDeep"]?.SetValue(ResonanceFrameModule.BeatDeep.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float diameter = drawRadius * 2f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(diameter, diameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
