using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足 ModPlayer：蓄力跳状态机与推进窗口
    /// <br/>蓄力由 <see cref="OmniElectricFootSkill"/> 经 RadialDrive/Release/Cancel 驱动；
    /// 二段跳由 <see cref="OmniElectricFootJump"/> 触发后转到 <see cref="OnAirJumpStarted"/>
    /// </summary>
    internal class OmniElectricFootPlayer : ModPlayer
    {
        /// <summary>二段跳后按住跳的推进帧数</summary>
        private const int AirThrustFrames = 18;

        /// <summary>推进期每帧抵抗的重力量，满强度约抵掉一半重力</summary>
        private const float ThrustLift = 0.2f;

        /// <summary>释放后冷却帧</summary>
        private const int ReleaseCooldownFrames = 14;

        /// <summary>蓄力进度 0~1，HUD 读取</summary>
        public float ChargeRatio { get; private set; }

        /// <summary>本帧正被雷达驱动蓄力，HUD 显隐用</summary>
        public bool IsCharging { get; private set; }

        /// <summary>
        /// 蓄力正在进行（贴地、已起蓄、未断电）；跨帧持有，ResetEffects 不会清
        /// <br/>断电后按键还按着也返回 false，否则玩家在空中会被一直锁着跳不了
        /// </summary>
        public bool ChargeLive => driveGrace > 0 && chargeSessionOpen && !chargeBroken;

        /// <summary>释放后冷却帧</summary>
        public int ReleaseCooldown { get; private set; }

        /// <summary>蓄力断电的红闪剩余帧，HUD 用</summary>
        public int BrokenFlash { get; private set; }

        /// <summary>推进窗口剩余帧</summary>
        public int ThrustTimer { get; private set; }

        /// <summary>脚踏地面；每次实测，不受 ModPlayer 更新顺序影响</summary>
        public bool IsOnGround => IsGrounded(Player);

        /// <summary>
        /// 头顶净空允许用满的最大蓄力比例，1=不受限；蓄力期间每帧刷新，HUD 画限高刻度
        /// </summary>
        public float MaxUsableRatio { get; private set; } = 1f;

        private float releaseCooldownCarry;
        //雷达驱动的存活帧，掉到 0 视为本次蓄力会话结束
        private int driveGrace;
        //会话已开（起蓄音、满蓄提示只发一次）
        private bool chargeSessionOpen;
        //本次会话已断电，后续 tick 全部忽略，松键也不起跳
        private bool chargeBroken;
        private bool chargeFullAnnounced;
        private int chargeParticleTick;
        //推进强度 0~1，决定抬升量与排气密度
        private float thrustStrength;
        //推进是否需要按住跳（二段跳要，蓄力跳不要）
        private bool thrustNeedsJump;
        //这趟腾空由义足推起，落地播缓冲并免摔伤
        private bool footPoweredAirborne;
        //推进期上一帧的 Y 速度，用来分辨"到顶"与"撞天花板"
        private float thrustPrevVelY;
        private bool wasGroundedLastFrame;
        //上帧结算后的 Y 速度，落地帧速度已被清零，摔落速度只能从这里取
        private float lastVelocityY;

        /// <summary>
        /// 下探 2px 碰撞探针。<see cref="Entity.velocity"/>.Y==0 在斜坡、悬停翅膀、
        /// 水中与黏液上都会把空中误判成贴地
        /// </summary>
        internal static bool IsGrounded(Player player) {
            if (player == null || !player.active) {
                return false;
            }
            if (player.mount.Active || player.pulley || player.grappling[0] >= 0
                || player.sleeping.isSleeping) {
                return false;
            }
            Vector2 probe = Vector2.UnitY * player.gravDir * 2f;
            Vector2 constrained = Collision.TileCollision(player.position, probe
                , player.width, player.height, false, false, (int)player.gravDir);
            return constrained.Y != probe.Y;
        }

        /// <summary>头顶到第一块实心砖的净空像素，上限 <paramref name="maxTiles"/> 格</summary>
        internal static float HeadroomPixels(Player player, int maxTiles = 48) {
            if (player == null || !player.active) {
                return 0f;
            }
            //反重力时"头顶"在下方
            int step = player.gravDir == 1f ? -1 : 1;
            Vector2 head = player.gravDir == 1f ? player.TopLeft : player.BottomLeft;
            int left = (int)(head.X / 16f);
            int right = (int)((head.X + player.width - 1f) / 16f);
            int startTileY = (int)(head.Y / 16f);
            for (int i = 1; i <= maxTiles; i++) {
                int tileY = startTileY + step * i;
                for (int tileX = left; tileX <= right; tileX++) {
                    if (!WorldGen.InWorld(tileX, tileY, 2)) {
                        return i * 16f;
                    }
                    Tile tile = Framing.GetTileSafely(tileX, tileY);
                    //平台不挡蹬升，只有实心砖算天花板
                    if (tile.HasTile && Main.tileSolid[tile.TileType]
                        && !Main.tileSolidTop[tile.TileType]) {
                        return (i - 1) * 16f;
                    }
                }
            }
            return maxTiles * 16f;
        }

        /// <summary>
        /// 预估某一蓄力比例的上升顶点像素高度；只用于 HUD 限高提示，
        /// 按原版基准重力 0.4 估算，低重力/反重力下会偏
        /// </summary>
        internal float PredictApexPixels(float ratio) {
            const float gravity = 0.4f;
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            float v0 = MathF.Max(Player.jumpSpeed, 5.01f)
                * MathHelper.Lerp(OmniElectricFoot.MinChargeJumpMul
                    , OmniElectricFoot.MaxChargeJumpMul, ratio);
            float strength = MathHelper.Clamp(0.65f + ratio * 0.35f, 0f, 1f);
            int thrustFrames = (int)MathHelper.Lerp(8f, 18f, ratio);
            float gEff = MathF.Max(gravity - ThrustLift * strength, 0.02f);

            //推进段：等减速直到窗口用完或速度归零
            float frames = MathF.Min(thrustFrames, v0 / gEff);
            float vEnd = v0 - gEff * frames;
            float rise = frames * (v0 + vEnd) * 0.5f;
            //自由段：剩余速度按满重力衰减
            return rise + vEnd * vEnd / (2f * gravity);
        }

        /// <summary>净空能吃下的最大蓄力比例，留 8px 余量；不受限返回 1</summary>
        private float ComputeMaxUsableRatio() {
            //满蓄顶点约 20 格，探到 24 格就够
            float headroom = HeadroomPixels(Player, 24) - 8f;
            if (headroom <= 0f) {
                return 0f;
            }
            if (PredictApexPixels(1f) <= headroom) {
                return 1f;
            }
            //从满蓄往下扫 20 档，取第一个塞得进去的
            for (int i = 19; i >= 1; i--) {
                float ratio = i / 20f;
                if (PredictApexPixels(ratio) <= headroom) {
                    return ratio;
                }
            }
            return 0f;
        }

        public override void ResetEffects() {
            int releaseCd = ReleaseCooldown;
            BaseCyberware.TickFrameDown(ref releaseCd, ref releaseCooldownCarry);
            ReleaseCooldown = releaseCd;
            if (BrokenFlash > 0) {
                BrokenFlash--;
            }

            if (driveGrace > 0) {
                driveGrace--;
            }
            //雷达停止驱动 → 会话作废，姿态清干净
            if (driveGrace <= 0) {
                chargeSessionOpen = false;
                chargeBroken = false;
                chargeFullAnnounced = false;
                if (ChargeRatio > 0f) {
                    //HUD 环收回
                    ChargeRatio = MathF.Max(0f, ChargeRatio - 0.06f);
                }
            }
            IsCharging = false;
        }

        public override void UpdateDead() {
            //PostUpdate 死亡期不跑，落地判定的残留状态会在重生帧误触缓冲演出
            ClearState();
            wasGroundedLastFrame = false;
            lastVelocityY = 0f;
        }

        public override void SetControls() {
            //蓄力期锁跳：踩空一下就白蓄 60 帧，不如直接不让跳
            if (ChargeLive && OmniElectricFoot.GetEquipped(Player) != null) {
                Player.controlJump = false;
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (OmniElectricFoot.GetEquipped(Player) == null) {
                return;
            }
            if (ChargeLive) {
                //扎地蓄力：还能挪半步，但跑不起来
                Player.maxRunSpeed *= 0.22f;
                Player.runAcceleration *= 0.5f;
                Player.runSlowdown *= 3f;
            }
            else if (ThrustTimer > 0) {
                //推进期空中转向权，"全向"落在这里
                Player.runAcceleration *= 2.6f;
                Player.maxRunSpeed *= 1.35f;
            }
        }

        public override void PreUpdateMovement() {
            if (OmniElectricFoot.GetEquipped(Player) == null) {
                ClearState();
                return;
            }
            //义足自己推起来的那趟腾空免摔伤，普通坠落照常算。
            //原版摔伤判定在 ResetEffects 之前读上一帧的值，腾空期每帧设即可覆盖着地帧
            if (footPoweredAirborne) {
                Player.noFallDmg = true;
            }
            UpdateThrust();
        }

        public override void PostUpdate() {
            if (OmniElectricFoot.GetEquipped(Player) == null) {
                ClearState();
                wasGroundedLastFrame = false;
                lastVelocityY = Player.velocity.Y;
                return;
            }

            bool grounded = IsGrounded(Player);
            //只在着地沿结算并清标记：起跳那帧脚还在地上，若按"贴地即清"会被
            //先跑的本 Hook 抹掉（蓄力跳由雷达在更靠后的 PostUpdate 触发）
            if (grounded && !wasGroundedLastFrame) {
                float fallSpeed = lastVelocityY * Player.gravDir;
                //义足推起来的那趟腾空落地，凝胶吸收，给一记闷落
                if (footPoweredAirborne && fallSpeed > 7f) {
                    OmniElectricFootVFX.LandingCushion(Player, fallSpeed);
                }
                footPoweredAirborne = false;
            }

            wasGroundedLastFrame = grounded;
            lastVelocityY = Player.velocity.Y;
        }

        /// <summary>雷达蓄力每帧回调，同步比例与粒子</summary>
        public void RadialDriveCharge(float ratio) {
            driveGrace = 2;
            if (chargeBroken) {
                return;
            }
            if (OmniElectricFoot.GetEquipped(Player) == null) {
                BreakCharge(playCue: false);
                return;
            }
            //刚放完，静默等冷却结束再起蓄
            if (ReleaseCooldown > 0) {
                ChargeRatio = 0f;
                return;
            }
            if (!IsGrounded(Player)) {
                BreakCharge(playCue: chargeSessionOpen);
                return;
            }

            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            if (!chargeSessionOpen) {
                chargeSessionOpen = true;
                chargeFullAnnounced = false;
                chargeParticleTick = 0;
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Pitch = 0.35f,
                    Volume = 0.4f,
                    MaxInstances = 2
                }, Player.Center);
            }
            ChargeRatio = ratio;
            IsCharging = true;
            MaxUsableRatio = ComputeMaxUsableRatio();

            if (ratio >= 1f && !chargeFullAnnounced) {
                chargeFullAnnounced = true;
                OmniElectricFootVFX.ChargeFull(Player);
            }

            //高蓄力每帧，低蓄力隔帧
            chargeParticleTick++;
            int interval = ratio > 0.6f ? 1 : (ratio > 0.3f ? 2 : 3);
            if (chargeParticleTick >= interval) {
                chargeParticleTick = 0;
                OmniElectricFootVFX.ChargeConverge(Player, ratio);
            }
        }

        /// <summary>松开技能键释放蓄力跳；断电、离地或 ratio&lt;0.05 都不起跳</summary>
        public void RadialReleaseCharge(float ratio) {
            bool broken = chargeBroken;
            int cooldown = ReleaseCooldown;
            ClearChargeSession();
            if (broken || cooldown > 0) {
                return;
            }
            if (OmniElectricFoot.GetEquipped(Player) == null || !IsGrounded(Player)) {
                return;
            }
            //轻点也起跳：屏蔽跳跃键那几帧不能白吃掉玩家的输入
            ReleaseChargeJump(MathHelper.Clamp(ratio, 0f, 1f));
        }

        /// <summary>切技能/开盘/死亡打断蓄力，不跳跃</summary>
        public void RadialCancelCharge() => ClearChargeSession();

        /// <summary>二段跳蹬出，由 <see cref="OmniElectricFootJump"/> 在各端调用</summary>
        internal void OnAirJumpStarted() {
            Player.velocity.Y = -OmniElectricFoot.DoubleJumpSpeed * Player.gravDir;
            //按住方向键才给横向蹬力，否则保持原有动量
            if (Player.controlLeft) {
                Player.velocity.X -= OmniElectricFoot.DoubleJumpKick;
            }
            else if (Player.controlRight) {
                Player.velocity.X += OmniElectricFoot.DoubleJumpKick;
            }
            //摔落高度从蹬出点重算
            Player.fallStart = (int)(Player.position.Y / 16f);
            StartThrust(AirThrustFrames, 0.9f, requireJumpHeld: true);
            footPoweredAirborne = true;
            OmniElectricFootVFX.AirJumpBurst(Player);
        }

        /// <summary>蓄力跳，倍率插值 + 按方向键的横向推力</summary>
        private void ReleaseChargeJump(float ratio) {
            //Player.jumpSpeed 是随帧重算的静态值（含跳跃加成），兜底原版基准
            float baseJumpSpeed = MathF.Max(Player.jumpSpeed, 5.01f);
            float mul = MathHelper.Lerp(OmniElectricFoot.MinChargeJumpMul
                , OmniElectricFoot.MaxChargeJumpMul, ratio);
            Player.velocity.Y = -baseJumpSpeed * mul * Player.gravDir;

            //站定蓄满就是纯垂直起跳，不再莫名往侧面飞
            float kick = MathHelper.Lerp(1.8f, 6.5f, ratio);
            if (Player.controlLeft) {
                Player.velocity.X -= kick;
            }
            else if (Player.controlRight) {
                Player.velocity.X += kick;
            }

            //清掉原版起跳维持帧，避免 velocity.Y 被钉回 jumpSpeed
            Player.jump = 0;
            Player.fallStart = (int)(Player.position.Y / 16f);
            StartThrust((int)MathHelper.Lerp(8f, 18f, ratio), 0.65f + ratio * 0.35f
                , requireJumpHeld: false);
            footPoweredAirborne = true;
            ReleaseCooldown = ReleaseCooldownFrames;
            releaseCooldownCarry = 0f;

            OmniElectricFootBurst.Fire(Player, ratio);
        }

        private void StartThrust(int frames, float strength, bool requireJumpHeld) {
            ThrustTimer = frames;
            thrustStrength = MathHelper.Clamp(strength, 0f, 1f);
            thrustNeedsJump = requireJumpHeld;
            //以起跳速度开局，撞顶检测下一帧才有可比的基准
            thrustPrevVelY = Player.velocity.Y;
        }

        /// <summary>推进期抵抗重力，读作推进器还在喷；转为下落即熄</summary>
        private void UpdateThrust() {
            if (ThrustTimer <= 0) {
                return;
            }
            //上一帧还在快速上冲、这一帧速度被碰撞清零 = 撞上天花板，
            //白费的蓄力不再罚冷却，否则室内起跳会像坏了
            if (thrustPrevVelY * Player.gravDir < -2f && Player.velocity.Y == 0f) {
                ThrustTimer = 0;
                ReleaseCooldown = 0;
                releaseCooldownCarry = 0f;
                OmniElectricFootVFX.CeilingSlam(Player);
                return;
            }

            bool rising = Player.velocity.Y * Player.gravDir < 0f;
            if (!rising || (thrustNeedsJump && !Player.controlJump)) {
                ThrustTimer = 0;
                return;
            }
            ThrustTimer--;
            Player.velocity.Y -= ThrustLift * thrustStrength * Player.gravDir;
            thrustPrevVelY = Player.velocity.Y;
            OmniElectricFootVFX.ThrustExhaust(Player, thrustStrength);
        }

        private void BreakCharge(bool playCue) {
            chargeBroken = true;
            chargeSessionOpen = false;
            ChargeRatio = 0f;
            IsCharging = false;
            if (playCue) {
                BrokenFlash = 20;
                OmniElectricFootVFX.ChargeFizzle(Player);
            }
        }

        private void ClearChargeSession() {
            driveGrace = 0;
            chargeSessionOpen = false;
            chargeBroken = false;
            chargeFullAnnounced = false;
            ChargeRatio = 0f;
            IsCharging = false;
            MaxUsableRatio = 1f;
        }

        private void ClearState() {
            ClearChargeSession();
            ThrustTimer = 0;
            thrustPrevVelY = 0f;
            footPoweredAirborne = false;
        }
    }
}
