using CalamityOverhaul.Content.HackTimes.Targets;
using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Actors;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.CircuitNodes
{
    /// <summary>
    /// 信号塔：世界里第一种可骇入信号塔 Actor（F14）。<br/>
    /// 本体不攻击，是区域电子战的枢纽，病毒广播的冲击波、电网瘫痪的断电场从它发出，
    /// 信标伪造与提权把它当作广播源。贴图为程序化绘制（桅杆+横担+航标灯），待美术替换；
    /// 跨端身份走 <see cref="CircuitActorKey"/>，为解除单人限制预留
    /// </summary>
    internal class SignalTowerActor : Actor, IHackableSignalTower, IDistressBeaconTower, IPrivilegeUplinkTower
    {
        //病毒冲击波扫完全程的帧数，与 VirusBroadcast 的表现窗口对齐
        private const int VirusWaveFrames = 150;
        //断电场机器同步节流（服务器）
        private const int BlackoutSyncInterval = 45;

        private static readonly Color TowerAccent = new(0, 200, 210);
        private static readonly Color VirusColor = new(200, 80, 255);
        private static readonly Color DeadColor = new(90, 110, 140);
        private static readonly Color BeaconColor = new(255, 120, 60);
        private static readonly Color UplinkColor = new(140, 255, 170);

        #region 状态
        //病毒波当前半径，0 表示无波
        [SyncVar]
        private float virusWaveRadius;
        //剩余断电帧
        [SyncVar]
        private int blackoutFrames;
        //剩余假信标帧
        [SyncVar]
        private int beaconFrames;
        //剩余提权上行帧
        [SyncVar]
        private int uplinkFrames;

        //波前上限参与远端绘制（PreDraw 用它算前沿透明度），不同步的话
        //旁观端 virusWaveRadius > 0 而 Max == 0，整圈冲击波前沿画不出来
        [SyncVar]
        private float virusWaveMax;
        private int virusDisableFrames;
        private int virusCasterIndex = -1;
        private float blackoutRadius;
        private int blackoutSyncCursor;
        private int beaconCasterIndex = -1;
        private int uplinkCasterIndex = -1;
        //波前已经扫过的炮台，别对同一台重复放电
        private readonly HashSet<int> virusProcessed = [];

        private float glowTimer;
        private float timeCarry;
        private int ambientTimer;
        #endregion

        /// <summary>跨端稳定身份，网络化预留</summary>
        internal CircuitActorKey NetKey => new(WhoAmI, Generation, ID);

        /// <summary>信标计数，BeaconForge 写、扫描面板读</summary>
        internal int BeaconLureCount;

        /// <summary>桅杆顶端世界坐标</summary>
        internal Vector2 MastTop => Position + new Vector2(Width / 2f, -2f);

        public override void OnSpawn(params object[] args) {
            Width = 30;
            Height = 96;
            DrawExtendMode = 420;
            DrawLayer = ActorDrawLayer.AfterTiles;
            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        #region 行为
        public override void AI() {
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            if (!Main.dedServ) {
                EmitAmbient();
            }

            if (VaultUtils.isClient) {
                return;
            }

            if (TimeGear.PullFrameAdvance(ref timeCarry) <= 0) {
                return;
            }

            UpdateVirusWave();
            UpdateBlackout();

            if (beaconFrames > 0 && --beaconFrames == 0) {
                EndDistressBeacon();
            }
            if (uplinkFrames > 0 && --uplinkFrames == 0) {
                uplinkCasterIndex = -1;
            }
        }

        private void UpdateVirusWave() {
            if (virusWaveRadius <= 0f || virusWaveMax <= 0f) {
                return;
            }

            virusWaveRadius += virusWaveMax / VirusWaveFrames;

            //波前扫到的炮台逐台放电停摆
            Player caster = ResolvePlayer(virusCasterIndex);
            foreach (Actor actor in ActorLoader.GetActiveActors<Actor>()) {
                if (actor is not IHackableTurret turret || actor.WhoAmI == WhoAmI) {
                    continue;
                }
                if (virusProcessed.Contains(actor.WhoAmI)) {
                    continue;
                }
                if (Vector2.DistanceSquared(actor.Center, Center) > virusWaveRadius * virusWaveRadius) {
                    continue;
                }
                virusProcessed.Add(actor.WhoAmI);
                turret.ApplyCircuitOverload(virusDisableFrames, caster);
                if (!Main.dedServ) {
                    EmitNodeHit(actor.Center, VirusColor);
                }
            }

            if (virusWaveRadius >= virusWaveMax) {
                virusWaveRadius = 0f;
                virusWaveMax = 0f;
                virusProcessed.Clear();
            }
        }

        private void UpdateBlackout() {
            if (blackoutFrames <= 0) {
                return;
            }
            blackoutFrames--;

            //整场断电：范围内机器每帧压零，机器自己发电也顶不回来
            List<TileProcessor> tps = TileProcessorLoader.TP_InWorld;
            float radiusSq = blackoutRadius * blackoutRadius;
            blackoutSyncCursor++;
            for (int i = 0; i < tps.Count; i++) {
                if (tps[i] is not MachineTP machine || !machine.Active) {
                    continue;
                }
                if (Vector2.DistanceSquared(machine.CenterInWorld, Center) > radiusSq) {
                    continue;
                }
                if (machine.MachineData == null || machine.MachineData.UEvalue <= 0f) {
                    continue;
                }
                machine.MachineData.UEvalue = 0f;
                //服务器分帧节流推送，不然一片工厂逐帧刷包
                if (Main.netMode == NetmodeID.Server
                    && (i + blackoutSyncCursor) % BlackoutSyncInterval == 0) {
                    machine.SendData();
                }
            }
        }

        private static Player ResolvePlayer(int index) {
            if (index < 0 || index >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[index];
            return player?.active == true ? player : null;
        }

        private void EmitAmbient() {
            Color light = CurrentAccent();
            Lighting.AddLight(MastTop, light.ToVector3() * 0.3f);

            if (++ambientTimer >= 46) {
                ambientTimer = 0;
                //信标态朝内吸，其余朝外滴答外扩
                if (beaconFrames > 0) {
                    Vector2 spawn = Center + Main.rand.NextVector2CircularEdge(160f, 160f);
                    PRTLoader.NewParticle<PRT_Spark>(spawn, (Center - spawn) * 0.02f,
                        BeaconColor, 0.6f)?.Configure(false, 40);
                }
                else {
                    PRTLoader.NewParticle<PRT_Spark>(MastTop, Main.rand.NextVector2CircularEdge(1.6f, 1.2f),
                        light * 0.9f, 0.5f)?.Configure(false, 30);
                }
            }

            //提权上行：沿塔身向天爬升的数据屑
            if (uplinkFrames > 0 && Main.rand.NextBool(6)) {
                Vector2 spawn = MastTop + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f);
                PRTLoader.NewParticle<PRT_Spark>(spawn, new Vector2(0f, Main.rand.NextFloat(-4.5f, -2.5f)),
                    UplinkColor, 0.55f)?.Configure(false, 26);
            }
        }

        private static void EmitNodeHit(Vector2 center, Color color) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, color, 0.8f)?.Configure(false, 24);
            }
        }
        #endregion

        #region IHackableSignalTower / 扩展口
        public Actor AsActor => this;

        public void BeginVirusBroadcast(float radiusPixels, int disableFrames, Player caster) {
            if (VaultUtils.isClient || radiusPixels <= 0f) {
                return;
            }
            virusWaveMax = radiusPixels;
            virusWaveRadius = 1f;
            virusDisableFrames = disableFrames;
            virusCasterIndex = caster?.whoAmI ?? -1;
            virusProcessed.Clear();
        }

        public void BeginGridBlackout(float radiusPixels, int disableFrames, Player caster) {
            if (VaultUtils.isClient || radiusPixels <= 0f) {
                return;
            }
            blackoutRadius = radiusPixels;
            blackoutFrames = Math.Max(blackoutFrames, disableFrames);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 0.7f }, Center);
            }
        }

        public bool DistressBeaconActive => beaconFrames > 0;

        public void BeginDistressBeacon(int frames, Player caster) {
            if (VaultUtils.isClient || frames <= 0) {
                return;
            }
            beaconFrames = frames;
            beaconCasterIndex = caster?.whoAmI ?? -1;
            BeaconLureCount = 0;
        }

        public void EndDistressBeacon() {
            beaconFrames = 0;
            beaconCasterIndex = -1;
        }

        public void BeginPrivilegeUplink(int frames, Player caster) {
            if (VaultUtils.isClient || frames <= 0) {
                return;
            }
            uplinkFrames = Math.Max(uplinkFrames, frames);
            uplinkCasterIndex = caster?.whoAmI ?? -1;
        }
        #endregion

        #region IHackTarget / IScannable
        public HackTargetType TargetType => HackTargetType.Get<SignalTowerTargetType>();

        public Vector2 WorldCenter => Center;

        public bool IsValid => Active;

        public bool IsHackable => true;

        public Vector2 LockFrameHalfSize => new(Width * 0.6f + 30f, Height * 0.6f + 30f);

        public string LockFrameTitle => CircuitNodeSpawner.TowerName.Value;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            (text, color) = DescribeStatus();
            return true;
        }

        public int ScanRowCount => 5;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            labels[0] = HackTime.TypeLabel.Value;
            values[0] = CircuitNodeSpawner.TowerName.Value;
            colors[0] = HackTheme.TextBright;

            labels[1] = CircuitNodeSpawner.StatusLabel.Value;
            (values[1], colors[1]) = DescribeStatus();

            labels[2] = CircuitNodeSpawner.CoverageLabel.Value;
            values[2] = $"{(int)(6400f / 16f)}";
            colors[2] = HackTheme.TextNormal;

            labels[3] = CircuitNodeSpawner.LinkedLabel.Value;
            values[3] = $"{CountLinkedTurrets()}";
            colors[3] = HackTheme.TextNormal;

            labels[4] = CircuitNodeSpawner.SourceLabel.Value;
            if (beaconFrames > 0) {
                values[4] = CircuitNodeSpawner.BeaconCount.Format(BeaconLureCount);
                colors[4] = BeaconColor;
            }
            else {
                values[4] = "-";
                colors[4] = HackTheme.TextDim;
            }
        }

        private (string, Color) DescribeStatus() {
            if (virusWaveRadius > 0f) {
                return (CircuitNodeSpawner.StatusVirus.Value, VirusColor);
            }
            if (blackoutFrames > 0) {
                return (CircuitNodeSpawner.StatusBlackout.Value, DeadColor);
            }
            if (beaconFrames > 0) {
                return (CircuitNodeSpawner.StatusBeacon.Value, BeaconColor);
            }
            if (uplinkFrames > 0) {
                return (CircuitNodeSpawner.StatusUplink.Value, UplinkColor);
            }
            return (CircuitNodeSpawner.StatusIdle.Value, HackTheme.AccentAlt);
        }

        private int CountLinkedTurrets() {
            int count = 0;
            foreach (Actor actor in ActorLoader.GetActiveActors<Actor>()) {
                if (actor is IHackableTurret
                    && Vector2.DistanceSquared(actor.Center, Center) <= 3000f * 3000f) {
                    count++;
                }
            }
            return count;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            return HackEffectTracker.ApplyAuthorityEffect(hack, this, casterIndex, 0, 0, 0f, 0) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is SignalTowerActor tower
                && tower.WhoAmI == WhoAmI && tower.Generation == Generation;
        }
        #endregion

        #region 绘制
        private Color CurrentAccent() {
            if (blackoutFrames > 0) return DeadColor;
            if (virusWaveRadius > 0f) return VirusColor;
            if (beaconFrames > 0) return BeaconColor;
            if (uplinkFrames > 0) return UplinkColor;
            return TowerAccent;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Texture2D px = HackTheme.Pixel;
            if (px == null) {
                return false;
            }

            Vector2 basePos = Position - Main.screenPosition;
            float centerX = basePos.X + Width / 2f;
            Color frame = Color.Lerp(drawColor, new Color(40, 50, 60), 0.7f);
            Color frameDark = Color.Lerp(drawColor, new Color(22, 28, 36), 0.75f);
            Color accent = CurrentAccent();
            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;

            //基座
            spriteBatch.Draw(px, new Rectangle((int)basePos.X - 4, (int)(basePos.Y + Height - 10), Width + 8, 10), HackTheme.SrcPixel, frameDark);
            //桅杆
            spriteBatch.Draw(px, new Rectangle((int)(centerX - 3f), (int)basePos.Y, 6, Height - 8), HackTheme.SrcPixel, frame);
            //斜撑
            HackTheme.DrawLine(spriteBatch, new Vector2(basePos.X + 2f, basePos.Y + Height - 10f),
                new Vector2(centerX, basePos.Y + Height * 0.45f), 2f, frameDark);
            HackTheme.DrawLine(spriteBatch, new Vector2(basePos.X + Width - 2f, basePos.Y + Height - 10f),
                new Vector2(centerX, basePos.Y + Height * 0.45f), 2f, frameDark);
            //三层横担，向上递减
            for (int i = 0; i < 3; i++) {
                float y = basePos.Y + 14f + i * 18f;
                float arm = 16f - i * 4f;
                HackTheme.DrawLine(spriteBatch, new Vector2(centerX - arm, y), new Vector2(centerX + arm, y), 2f, frame);
            }
            //航标灯
            HackTheme.DrawDiamond(spriteBatch, new Vector2(centerX, basePos.Y + 2f), 5f,
                accent * (0.5f + pulse * 0.5f));

            //广播环：待机时一圈慢脉冲，信标时反向收缩
            float ringPhase = glowTimer / MathHelper.TwoPi;
            float ringR = beaconFrames > 0 ? (1f - ringPhase) * 60f + 10f : ringPhase * 60f + 10f;
            DrawRing(spriteBatch, new Vector2(centerX, basePos.Y + 2f), ringR,
                accent * (0.3f * (1f - ringR / 75f)), 20);

            //病毒冲击波前沿
            if (virusWaveRadius > 0f && virusWaveMax > 0f) {
                float alpha = 0.5f * (1f - virusWaveRadius / virusWaveMax) + 0.12f;
                DrawRing(spriteBatch, Center - Main.screenPosition, virusWaveRadius, VirusColor * alpha, 72);
            }

            //提权上行光柱
            if (uplinkFrames > 0) {
                Vector2 top = new(centerX, basePos.Y + 2f);
                HackTheme.DrawLine(spriteBatch, top, top - new Vector2(0f, 480f), 2f,
                    UplinkColor * (0.16f + pulse * 0.12f));
            }

            //断电遮罩
            if (blackoutFrames > 0) {
                Rectangle box = new((int)basePos.X - 2, (int)basePos.Y, Width + 4, Height);
                HackTheme.DrawHatch(spriteBatch, box, 10f, DeadColor * 0.25f);
            }
            return false;
        }

        private static void DrawRing(SpriteBatch spriteBatch, Vector2 screenCenter, float radius, Color color, int segments) {
            if (color.A <= 2 && color.R <= 2 && color.G <= 2 && color.B <= 2) {
                return;
            }
            Vector2 prev = screenCenter + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 next = screenCenter + angle.ToRotationVector2() * radius;
                HackTheme.DrawLine(spriteBatch, prev, next, 1.2f, color);
                prev = next;
            }
        }
        #endregion
    }
}
