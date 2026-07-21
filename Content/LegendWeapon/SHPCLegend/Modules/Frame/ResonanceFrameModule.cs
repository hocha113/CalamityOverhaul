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
    /// 共振机匣，驻波节拍器，每第 N 发升格节拍束（增粗+护层+增伤），
    /// 首命中掀震荡环；跟稳节奏叠节奏层；激光按周期蓄振染色取拍
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

        //驻波洋红三阶，区别同槽量子紫/谐振绿
        internal static readonly Color BeatBright = new(255, 170, 220);
        internal static readonly Color BeatMain = new(240, 80, 165);
        internal static readonly Color BeatDeep = new(130, 20, 85);

        //每玩家节拍器状态，模块实例即每玩家，不入 static

        /// <summary>自上一节拍射击数，达 BeatEvery 为本拍</summary>
        private int _shotsSinceBeat;
        /// <summary>节奏层 0~MaxTempo，断拍清零</summary>
        private int _tempo;
        /// <summary>距上次射击帧，断拍判定，TimeGear 推进</summary>
        private int _framesSinceShot = 100000;
        private float _frameCarry;
        /// <summary>上次登记射击帧号，同帧多束同扣扳机</summary>
        private uint _lastVolleyTick = uint.MaxValue;

        private struct BeamEntry
        {
            /// <summary>节奏层，-1=平拍</summary>
            public int Tempo;
            /// <summary>identity 校验，防 whoAmI 槽复用</summary>
            public int Identity;
        }
        /// <summary>已登记原生光束 whoAmI→节拍信息</summary>
        private readonly Dictionary<int, BeamEntry> _beams = new();

        private struct LaserBeatState
        {
            public int Identity;
            public int Timer;
            public int Flash;
            public bool Armed;
        }
        /// <summary>激光节拍 whoAmI→计时/染色窗/取拍</summary>
        private readonly Dictionary<int, LaserBeatState> _lasers = new();
        /// <summary>字典周期清扫，防泄漏</summary>
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

            //首见新束＝一次射击，同帧姊妹束同扣扳机
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

        /// <summary>推进节拍器一格，返回是否节拍与 tempoUsed</summary>
        private bool RegisterShot(CyberTraceBeamProj beam, out int tempoUsed) {
            tempoUsed = 0;
            //断拍先于计数，停火久节奏归零，射击计数保留
            if (_framesSinceShot > TempoBreakFrames) {
                _tempo = 0;
            }
            _framesSinceShot = 0;
            _frameCarry = 0f;

            Vector2 muzzle = beam.Projectile.Center;
            _shotsSinceBeat++;
            if (_shotsSinceBeat < BeatEvery) {
                //平拍嘀嗒铺垫
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
                //定音鼓+底鼓
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

        /// <summary>升格节拍束，增伤+穿透+驻波护层</summary>
        private void UpgradeToBeatBeam(CyberTraceBeamProj beam, int beatTempo) {
            float mul = BeatDamageMul + beatTempo * TempoDamageStep;
            beam.Projectile.damage = Math.Max((int)(beam.Projectile.damage * mul), 1);
            beam.Projectile.penetrate += BeatExtraPierce;
            //ai0=identity，ai1=节奏层
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
            //每束仅首命中掀环
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
                if (st.Flash == 0) st.Armed = false; //取拍窗关，节拍作废
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
            //节拍窗内洋红蓄振染色
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
            //窗内首命中取拍
            st.Armed = false;
            _lasers[id] = st;

            int dmg = Math.Max((int)(laser.Projectile.damage * LaserRingDamageRatio), 1);
            SpawnResonanceRing(laser.Projectile, target.Center, dmg, LaserRingRadius, 0.3f);
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            _lasers.Remove(laser.Projectile.whoAmI);
        }

        #endregion

        /// <summary>命中点掀驻波震荡环</summary>
        private static void SpawnResonanceRing(Projectile source, Vector2 center, int damage, float radius, float waveBoost) {
            Projectile.NewProjectile(source.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<SHPCResonanceRingProj>(),
                damage, RingKnockback, source.owner,
                ai0: radius, ai1: waveBoost);
        }

        #region 每帧维护与蓄振预兆

        public override void OnPlayerUpdate(Player player) {
            //清扫全实例，远端 _lasers 也会被 OnLaserAI 写入
            SweepDead();
            if (player.whoAmI != Main.myPlayer) return;
            if (_framesSinceShot < 100000) {
                _framesSinceShot += TickUp(ref _frameCarry);
            }
            if (!player.active || player.dead) return;
            SpawnBuildupFx(player);
        }

        /// <summary>节拍临近枪口蓄振粒子与临界音</summary>
        private void SpawnBuildupFx(Player player) {
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            //激光/蓄力无离散节拍，不给预兆
            if (player.channel) return;
            if (_shotsSinceBeat <= 0 || _framesSinceShot > TempoBreakFrames) return;

            float t = _shotsSinceBeat / (float)(BeatEvery - 1);
            bool primed = _shotsSinceBeat >= BeatEvery - 1;
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 muzzle = player.GetPlayerStabilityCenter() + aim * 52f;

            //蓄振粒子向枪口收束
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
                //临界闪音
                if (Main.rand.NextBool(20)) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.2f, Pitch = 0.4f }, muzzle);
                }
            }
        }

        /// <summary>周期清扫失效项，改件卸下 OnBeamKill 不到达，防泄漏</summary>
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
    /// 节拍束驻波护层，跟宿主画宽束（SHPCModResonanceWave.fx），
    /// ai0=宿主 identity，ai1=节奏层 0~1，纯视觉
    /// </summary>
    internal sealed class SHPCResonanceWaveProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TrailLen = 42;
        private const float PointSpacing = 10f;
        private const int FadeOutFrames = 12;
        /// <summary>驻波角频率，与着色器 uTime(0.045/帧)×6 同频</summary>
        private const float WaveOmega = 0.27f;

        private Vector2[] history;   //[0]=最新记录点
        private int historyCount;
        private Vector2[] drawBuffer;
        private int validCount;
        private Trail trail;
        private float fadeIn;
        /// <summary>-1=跟随中，>=0 剩余淡出帧</summary>
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

        /// <summary>按 identity 定位宿主，localAI[1] 缓存槽+1</summary>
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
                //宿主消亡，护层淡出
                if (fadeOut < 0) fadeOut = FadeOutFrames;
                else fadeOut--;
                if (fadeOut <= 0) {
                    Projectile.Kill();
                    return;
                }
                Projectile.timeLeft = 2;
            }
            //宿主未同步，等 timeLeft 耗尽
        }

        /// <summary>固定间距记拖尾点，段间插值防折线粗糙</summary>
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
            //有效顶点收尾，参考 CyberTraceBeamProj
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
            //头部光球与护层着色器同频
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
    /// 驻波震荡环，节拍命中一次性扩张 AoE，
    /// ai0=最大半径 px，ai1=节奏层 0~1；SHPCModResonanceRing.fx
    /// </summary>
    internal sealed class SHPCResonanceRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 26;
        /// <summary>环前沿判定厚度 px</summary>
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
            Projectile.localNPCHitCooldown = -1; //一环一结算
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    //命中打拍炸响
                    SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DrumTomHigh with { Volume = 0.45f, Pitch = 0.1f }, Projectile.Center);
                    SpawnBurstFx();
                    //屏震随距波心衰减，不满幅震旁观者
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
            //环前沿判定，盒与 [r±带宽] 相交
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
            //击退沿环心外
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
            //蠕虫体节折减 0.45，对齐延伸枪托
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
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            //外辉光带，环进度按绘制半径归一
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
