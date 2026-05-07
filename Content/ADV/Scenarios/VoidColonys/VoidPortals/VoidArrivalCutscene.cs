using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.TimeShift;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals
{
    /// <summary>
    /// 虚空聚落首次进入演出，电影级运镜 + 传送门吐出玩家
    /// </summary>
    internal class VoidArrivalCutscene : ModPlayer
    {
        internal enum Stage
        {
            //未激活
            Idle,
            //顶空下降运镜，玩家隐藏
            SkyPan,
            //镜头到位，传送门汇聚开启
            PortalSummon,
            //传送门抛出玩家
            Ejection,
            //收尾，镜头还给玩家
            Settle,
        }

        #region 配置
        //顶部起始位置相对出生点的像素偏移(向上为负Y)
        private const float PanStartOffsetY = -2000f;
        //运镜终点（镜头最终位置）相对出生点的像素偏移
        private const float PanEndOffsetY = -200f;
        //传送门世界坐标相对出生点的偏移
        private const float PortalOffsetY = -140f;
        //阶段时长（帧）
        private const int SkyPanDuration = 330;
        private const int PortalSummonDuration = 60;
        private const int EjectionDuration = 40;
        private const int SettleDuration = 60;
        //镜头位置 Lerp 速度
        private const float CamLerpSpeed = 0.18f;
        //抛出玩家后的初速度
        private const float EjectVelocity = 14f;
        //首次进入虚空聚落后，等待几帧再触发演出，保证 Player/子世界状态稳定
        private const int FirstEntryWaitFrames = 12;

        //过去闪现节拍：在 SkyPan 阶段内按 stageTimer 触发，数据为 (中心帧, 半宽帧, 峰值滤镜)
        //越往后滤镜越强，营造"越接近地面，过去越浓厚"的递进
        private static readonly (int center, int halfWidth, float peak)[] PastFlashes = new[] {
            (70, 18, 0.55f),
            (150, 22, 0.75f),
            (235, 28, 0.95f),
        };
        #endregion

        #region 运行时
        internal Stage CurrentStage { get; private set; }
        private int stageTimer;
        private Vector2 spawnCenter;
        private Vector2 portalCenter;
        private int portalIndex = -1;
        //当前镜头目标位置（世界坐标，左上角）
        private Vector2 smoothedScreenPos;
        private bool cameraInit;
        private float savedZoom;
        //玩家抛出后是否解除隐藏
        private bool playerRevealed;
        //场景回调
        private Action onDone;
        //首次进入虚空聚落后的稳定等待计时
        private int firstEntryWaitTimer;
        #endregion

        #region 公开 API
        /// <summary>
        /// 解析演出锚点（核心浮岛出生点的世界坐标）
        /// 优先级：显式传入 > 虚空聚落中的核心实验室岛屿 > 当前玩家中心 > spawnTileX/Y
        /// 最后再对世界边界做钳制，避免镜头被甩到地图角落
        /// </summary>
        private Vector2 ResolveAnchor(Vector2? explicitSpawn) {
            Vector2 anchor;
            if (explicitSpawn.HasValue) {
                anchor = explicitSpawn.Value;
            }
            else {
                anchor = Vector2.Zero;
                bool resolved = false;
                //子世界内优先查核心浮岛注册表
                if (VoidColony.Active) {
                    var core = IslandRegistry.FindByTag("核心实验室");
                    if (core != null) {
                        int surfY = core.SurfaceY > 0 ? core.SurfaceY : core.CenterY - core.TopThickness;
                        anchor = new Vector2(core.CenterX * 16f + 8f, surfY * 16f);
                        resolved = true;
                    }
                }
                //回退到玩家当前位置（子世界刚进入时即出生点；开发者重播时即玩家位置）
                if (!resolved && Player != null && Player.active) {
                    anchor = Player.Center;
                    resolved = true;
                }
                //最终回退到世界出生点
                if (!resolved) {
                    anchor = new Vector2(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f);
                }
            }

            //钳制到世界安全范围内，保证镜头绝不会跑到地图边缘
            float worldW = Main.maxTilesX * 16f;
            float worldH = Main.maxTilesY * 16f;
            const float edgePad = 1600f;
            //需要为上方 2000px 的起镜位置预留空间
            const float topPad = 2200f;
            anchor.X = MathHelper.Clamp(anchor.X, edgePad, worldW - edgePad);
            anchor.Y = MathHelper.Clamp(anchor.Y, topPad, worldH - edgePad);
            return anchor;
        }

        /// <summary>启动抵达演出；若无坐标，自动从核心浮岛/玩家位置解析</summary>
        public void StartArrival(Vector2? explicitSpawn = null, Action onFinished = null) {
            if (CurrentStage != Stage.Idle) return;

            spawnCenter = ResolveAnchor(explicitSpawn);
            portalCenter = spawnCenter + new Vector2(0f, PortalOffsetY);

            //玩家定格到传送门中心，演出期间隐藏
            Player.velocity = Vector2.Zero;
            Player.Center = portalCenter;
            Player.immune = true;
            Player.immuneTime = 60;

            savedZoom = Main.GameZoomTarget;
            cameraInit = false;
            playerRevealed = false;
            onDone = onFinished;

            //接管时空叠加系统，用于在 SkyPan 中闪现过去
            VoidTimeShiftSystem.BeginExternalDrive();
            VoidTimeShiftSystem.DriveState(0f, 0f);

            TransitionTo(Stage.SkyPan);
        }

        /// <summary>开发者用：强制从头重播演出（即使已看过）</summary>
        public static void ForceReplay(Player player = null) {
            player ??= Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (!player.TryGetModPlayer(out VoidArrivalCutscene cs)) return;
            if (cs.CurrentStage != Stage.Idle) cs.Abort();
            cs.StartArrival();
        }

        /// <summary>开发者用：清除"已看过抵达演出"旗标，使下次进入虚空聚落重新触发</summary>
        public static void ResetSeenFlag(Player player = null) {
            player ??= Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (!player.TryGetADVSave(out var save)) return;
            save.Get<VoidColonyADVData>().HasSeenArrival = false;
            if (player.TryGetModPlayer(out VoidArrivalCutscene cs)) {
                cs.firstEntryWaitTimer = 0;
            }
        }

        /// <summary>检查并首次触发：由虚空聚落进入时调用</summary>
        internal static void TryTriggerFirstEntry(Player player) {
            if (player == null || !player.active) return;
            if (!VoidColony.Active) return;
            if (!player.TryGetModPlayer(out VoidArrivalCutscene cs)) return;
            if (cs.CurrentStage != Stage.Idle) return;

            if (!player.TryGetADVSave(out var save)) return;
            var data = save.Get<VoidColonyADVData>();
            if (data.HasSeenArrival) {
                //已看过抵达演出，重复进入时补充生成返回传送门
                if (!Main.dedServ && player.whoAmI == Main.myPlayer
                    && VoidReturnPortalActor.ValidateActive() == null) {
                    Vector2 anchor = cs.ResolveAnchor(null);
                    ActorLoader.NewActor<VoidReturnPortalActor>(anchor + new Vector2(0f, -100f), Vector2.Zero);
                }
                return;
            }

            //等待进入子世界后玩家稳定再触发，避免生成过程中 spawnTile/Player 位置不稳定
            cs.firstEntryWaitTimer++;
            if (cs.firstEntryWaitTimer < FirstEntryWaitFrames) return;

            //演出启动成功后再置位旗标，避免中途失败导致永久屏蔽
            cs.StartArrival();
            if (cs.CurrentStage != Stage.Idle) {
                data.HasSeenArrival = true;
            }
        }

        /// <summary>强制中断并复位</summary>
        public void Abort() {
            if (CurrentStage == Stage.Idle) return;
            KillPortal();
            Restore();
            CurrentStage = Stage.Idle;
        }
        #endregion

        public override void PostUpdate() {
            //进入虚空聚落后在本地玩家的 PostUpdate 中检测首次触发
            if (Player.whoAmI == Main.myPlayer && CurrentStage == Stage.Idle) {
                if (VoidColony.Active) {
                    TryTriggerFirstEntry(Player);
                }
                else {
                    firstEntryWaitTimer = 0;
                }
            }

            if (CurrentStage == Stage.Idle) return;

            stageTimer++;
            switch (CurrentStage) {
                case Stage.SkyPan: UpdateSkyPan(); break;
                case Stage.PortalSummon: UpdatePortalSummon(); break;
                case Stage.Ejection: UpdateEjection(); break;
                case Stage.Settle: UpdateSettle(); break;
            }
        }

        public override void PreUpdate() {
            if (CurrentStage == Stage.Idle) return;
            //演出期间锁定操作
            if (CurrentStage == Stage.SkyPan || CurrentStage == Stage.PortalSummon) {
                LockAll();
            }
        }

        public override void ModifyScreenPosition() {
            if (CurrentStage == Stage.Idle) return;
            if (Player.whoAmI != Main.myPlayer) return;

            Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

            Vector2 camFocus;
            if (CurrentStage == Stage.SkyPan) {
                float t = MathHelper.Clamp((float)stageTimer / SkyPanDuration, 0f, 1f);
                //使用柔和 ease-in-out 曲线
                float k = t * t * (3f - 2f * t);
                float offsetY = MathHelper.Lerp(PanStartOffsetY, PanEndOffsetY, k);
                camFocus = spawnCenter + new Vector2(0f, offsetY);
            }
            else if (CurrentStage == Stage.PortalSummon || CurrentStage == Stage.Ejection) {
                camFocus = spawnCenter + new Vector2(0f, PanEndOffsetY);
            }
            else {
                //Settle：从演出焦点逐步转回玩家中心
                float t = MathHelper.Clamp((float)stageTimer / SettleDuration, 0f, 1f);
                t = t * t * (3f - 2f * t);
                Vector2 endFocus = spawnCenter + new Vector2(0f, PanEndOffsetY);
                camFocus = Vector2.Lerp(endFocus, Player.Center, t);
            }

            Vector2 desired = camFocus - screenSize * 0.5f;
            if (!cameraInit) {
                smoothedScreenPos = desired;
                cameraInit = true;
            }
            else {
                smoothedScreenPos = Vector2.Lerp(smoothedScreenPos, desired, CamLerpSpeed);
            }
            Main.screenPosition = smoothedScreenPos;
        }

        /// <summary>演出期间隐藏本玩家，PlayerOverride 路由</summary>
        internal bool ShouldHidePlayer {
            get {
                return CurrentStage == Stage.SkyPan
                    || CurrentStage == Stage.PortalSummon
                    || (CurrentStage == Stage.Ejection && !playerRevealed);
            }
        }

        #region 阶段更新
        private void UpdateSkyPan() {
            //运镜期间玩家固定在传送门位置，内部无速度
            Player.velocity = Vector2.Zero;
            Player.Center = portalCenter;
            Player.immune = true;
            Player.immuneTime = 2;

            //每帧驱动过去滤镜：多段三角脉冲叠加
            DrivePastFlashesForSkyPan();

            if (stageTimer >= SkyPanDuration) {
                //落镜前强制把时空叠加拉回现在，避免与传送门演出冲突
                VoidTimeShiftSystem.DriveState(0f, 0f);
                //生成传送门
                if (!Main.dedServ) {
                    portalIndex = VoidArrivalPortal.Spawn(
                        Player.GetSource_Misc("VoidArrival"),
                        portalCenter,
                        sustainFrames: PortalSummonDuration);
                    SoundEngine.PlaySound(SoundID.Zombie93 with { Volume = 0.9f, Pitch = -0.3f }, portalCenter);
                }
                TransitionTo(Stage.PortalSummon);
            }
        }

        /// <summary>
        /// SkyPan 阶段内按 <see cref="PastFlashes"/> 节拍驱动时空叠加滤镜
        /// 每段是一个三角脉冲，在中心帧达到峰值，向两侧线性衰减到零
        /// 过渡闪光使用前 1/3 段的钟形曲线模拟"撕开现实"的瞬间
        /// </summary>
        private void DrivePastFlashesForSkyPan() {
            float filter = 0f;
            float burst = 0f;
            for (int i = 0; i < PastFlashes.Length; i++) {
                var (center, halfWidth, peak) = PastFlashes[i];
                int dt = stageTimer - center;
                int absDt = Math.Abs(dt);
                if (absDt >= halfWidth) continue;

                float tri = 1f - (float)absDt / halfWidth;
                //平方让峰值更尖锐
                float pulse = tri * tri * peak;
                if (pulse > filter) filter = pulse;

                //撕裂闪光：在进入段（dt 接近 -halfWidth/2）集中爆发
                if (dt > -halfWidth && dt < halfWidth / 2) {
                    float bt = (dt + halfWidth) / (float)(halfWidth + halfWidth / 2);
                    float flash = MathF.Sin(MathHelper.Pi * MathHelper.Clamp(bt, 0f, 1f));
                    flash *= flash;
                    if (flash > burst) burst = flash;
                }

                //每段进入/离开瞬间触发一次低频音效
                if (stageTimer == center - halfWidth + 1) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item122 with {
                            Volume = 0.55f,
                            Pitch = -0.6f + 0.15f * i,
                        }, portalCenter);
                    }
                }
                //if (stageTimer == center) {
                //    if (!Main.dedServ) {
                //        SoundEngine.PlaySound(SoundID.Item67 with {
                //            Volume = 0.45f + 0.12f * i,
                //            Pitch = -0.4f,
                //        }, portalCenter);
                //    }
                //}
            }
            VoidTimeShiftSystem.DriveState(filter, burst);
        }

        private void UpdatePortalSummon() {
            Player.velocity = Vector2.Zero;
            Player.Center = portalCenter;
            Player.immune = true;
            Player.immuneTime = 2;

            VoidArrivalPortal portal = GetPortal();
            //等传送门进入 Sustaining 阶段后让它抛出
            if (portal != null && portal.CurrentPhase == VoidArrivalPortal.Phase.Sustaining
                && stageTimer >= 12) {
                portal.BeginEject();
                TransitionTo(Stage.Ejection);
            }
            else if (stageTimer >= PortalSummonDuration + 40) {
                //保底：避免异常卡住
                if (portal != null) portal.BeginEject();
                TransitionTo(Stage.Ejection);
            }
        }

        private void UpdateEjection() {
            if (!playerRevealed && stageTimer >= 4) {
                //展示玩家并给一个向下外抛的初速度
                playerRevealed = true;
                Player.Center = portalCenter;
                //方向略向下加一点随机扰动
                float ang = MathHelper.PiOver2 + Main.rand.NextFloat(-0.25f, 0.25f);
                Player.velocity = ang.ToRotationVector2() * EjectVelocity;
                //小抖
                if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                    Main.screenPosition += Main.rand.NextVector2Circular(8f, 8f);
                }
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f }, portalCenter);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f }, portalCenter);
            }

            //被抛出后逐步接管重力；期间保留无敌
            Player.immune = true;
            Player.immuneTime = 2;

            if (stageTimer >= EjectionDuration) {
                //关闭传送门
                VoidArrivalPortal portal = GetPortal();
                portal?.BeginClose();
                TransitionTo(Stage.Settle);
            }
        }

        private void UpdateSettle() {
            //本阶段不再锁定操作，玩家自然落地
            if (stageTimer >= SettleDuration) {
                KillPortal();
                Restore();
                //传送门演出结束后在出生点上方生成返回传送门
                if (!Main.dedServ && VoidReturnPortalActor.ValidateActive() == null) {
                    ActorLoader.NewActor<VoidReturnPortalActor>(spawnCenter + new Vector2(0f, -100f), Vector2.Zero);
                }
                var cb = onDone;
                onDone = null;
                CurrentStage = Stage.Idle;
                cb?.Invoke();
            }
        }
        #endregion

        #region 辅助
        private void TransitionTo(Stage next) {
            CurrentStage = next;
            stageTimer = 0;
        }

        private VoidArrivalPortal GetPortal() {
            if (portalIndex >= 0 && portalIndex < Main.maxProjectiles) {
                Projectile p = Main.projectile[portalIndex];
                if (p.active && p.ModProjectile is VoidArrivalPortal vp) return vp;
            }
            return VoidArrivalPortal.ActiveInstance;
        }

        private void KillPortal() {
            VoidArrivalPortal p = GetPortal();
            if (p != null && p.Projectile.active) p.BeginClose();
            portalIndex = -1;
        }

        private void Restore() {
            cameraInit = false;
            Main.GameZoomTarget = savedZoom;
            //释放时空叠加系统接管
            VoidTimeShiftSystem.EndExternalDrive();
        }

        private void LockAll() {
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlInv = false;
            Player.controlQuickHeal = false;
            Player.controlQuickMana = false;
            Player.controlSmart = false;
            Player.controlMap = false;
        }
        #endregion
    }
}
