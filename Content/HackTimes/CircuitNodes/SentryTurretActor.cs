using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.HackTimes.Targets;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Actors;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.CircuitNodes
{
    /// <summary>
    /// 哨戒炮塔：世界里第一种可骇入炮台 Actor（F14）。<br/>
    /// 原生行为是敌对火控，索敌玩家、蓄能、开火；五条存量炮台/信号塔协议
    /// （短路/过载/劫持/病毒广播/电网瘫痪）与本批的弹药置换、炮台联网都打在它身上。<br/>
    /// 贴图为程序化绘制（底座+云台+炮管+状态灯），待美术替换；
    /// 跨端身份走 <see cref="CircuitActorKey"/>（槽位+代+类型），为解除单人限制预留
    /// </summary>
    internal class SentryTurretActor : Actor, IHackableTurret, IMunitionFeedTurret, IMeshFireTurret
    {
        #region 常量
        //敌对火控半径 px
        internal const float PlayerSeekRange = 760f;
        //策反后索敌半径 px
        internal const float NpcSeekRange = 950f;
        //原生射击间隔（帧）
        private const int NativeFireInterval = 78;
        //策反射击间隔
        private const int HijackFireInterval = 58;
        //弹药置换射击间隔
        private const int MunitionFireInterval = 46;
        //开火前的蓄能帧数，给玩家一个可读的预警窗口
        private const int WarmupFrames = 42;

        private static readonly Color HostileLight = new(230, 56, 68);
        private static readonly Color FriendlyLight = new(120, 255, 200);
        private static readonly Color MunitionLight = new(255, 210, 90);
        private static readonly Color MeshLight = new(0, 200, 210);
        private static readonly Color DeadLight = new(90, 110, 140);
        #endregion

        #region 状态
        //剩余停摆帧，>0 时不索敌不开火
        [SyncVar]
        private int disabledFrames;
        //剩余策反帧，期间替施法者打敌怪
        [SyncVar]
        private int hijackFrames;
        //剩余弹药置换帧
        [SyncVar]
        private int munitionFrames;
        //剩余组网帧
        [SyncVar]
        private int meshFrames;

        private int munitionProjType;
        private int munitionDamage;
        private int munitionFeederIndex = -1;
        private int munitionAmmoType;
        private int hijackOwnerIndex = -1;
        private int meshRootSlot = -1;
        private Vector2 meshAimPoint;

        private int fireCooldown;
        private float warmup;
        private float aimRotation = -MathHelper.PiOver2;
        private float muzzleFlash;
        private float glowTimer;
        private int sparkTimer;
        //时停/时缓闸门的跨帧累加器，与 HackEffectTracker 同一节拍
        private float timeCarry;
        #endregion

        /// <summary>跨端稳定身份，网络化预留</summary>
        internal CircuitActorKey NetKey => new(WhoAmI, Generation, ID);

        /// <summary>炮口世界坐标</summary>
        internal Vector2 MuzzlePos => HeadCenter + aimRotation.ToRotationVector2() * 24f;

        //云台中心比几何中心略高，底座占掉下缘
        private Vector2 HeadCenter => Center + new Vector2(0f, -8f);

        public override void OnSpawn(params object[] args) {
            Width = 36;
            Height = 46;
            DrawExtendMode = 320;
            DrawLayer = ActorDrawLayer.AfterTiles;
            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            fireCooldown = NativeFireInterval;
        }

        #region 行为
        public override void AI() {
            glowTimer += 0.045f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }
            muzzleFlash = MathF.Max(0f, muzzleFlash - 0.12f);

            if (!Main.dedServ) {
                EmitAmbient();
            }

            //远端客户端只做表现，行为与计时归权威端；同步字段由 Actor 网络层带回
            if (VaultUtils.isClient) {
                return;
            }

            //时停期间行为与倒计时一并冻结，节拍对齐效果追踪器
            if (TimeGear.PullFrameAdvance(ref timeCarry) <= 0) {
                return;
            }

            TickTimers();

            if (disabledFrames > 0) {
                warmup = 0f;
                return;
            }

            if (meshFrames > 0) {
                //组网期不自主开火，只把炮口对准协议写入的齐射点
                RotateToward(meshAimPoint);
                if (fireCooldown > 0) {
                    fireCooldown--;
                }
                return;
            }

            if (munitionFrames > 0 || hijackFrames > 0) {
                UpdateFriendlyFire();
                return;
            }

            UpdateHostileFire();
        }

        private void TickTimers() {
            if (disabledFrames > 0) {
                disabledFrames--;
            }
            if (hijackFrames > 0 && --hijackFrames == 0) {
                hijackOwnerIndex = -1;
            }
            if (munitionFrames > 0 && --munitionFrames == 0) {
                ClearMunitionOverride();
            }
            if (meshFrames > 0 && --meshFrames == 0) {
                meshRootSlot = -1;
            }
            if (fireCooldown > 0) {
                fireCooldown--;
            }
        }

        private void UpdateHostileFire() {
            Player target = FindPlayerTarget();
            if (target == null) {
                warmup = MathF.Max(0f, warmup - 1.5f / WarmupFrames);
                return;
            }

            RotateToward(target.Center);
            warmup = MathF.Min(1f, warmup + 1f / WarmupFrames);
            if (warmup < 1f || fireCooldown > 0 || !AimSettled(target.Center)) {
                return;
            }

            fireCooldown = NativeFireInterval;
            Vector2 velocity = (target.Center - MuzzlePos).SafeNormalize(Vector2.UnitX) * 9.5f;
            SpawnShot(velocity, ProjectileID.MartianTurretBolt, HostileShotDamage(), hitsPlayers: true);
            PlayMuzzle();
        }

        private void UpdateFriendlyFire() {
            NPC target = FindNpcTarget();
            if (target == null) {
                return;
            }

            RotateToward(target.Center);
            if (fireCooldown > 0 || !AimSettled(target.Center)) {
                return;
            }

            if (munitionFrames > 0) {
                Player feeder = ResolveFeeder();
                if (feeder == null) {
                    ClearMunitionOverride();
                    return;
                }
                //单人本机直接扣；服务端校验镜像背包后发扣弹意图给喂弹者本机结算
                MunitionFeedVerdict verdict = MunitionSwap.RequestFeed(NetKey,
                    feeder, munitionAmmoType);
                if (verdict == MunitionFeedVerdict.Exhausted) {
                    //弹尽或喂弹者离场就立刻回落，协议 OnTick 会察觉并收尾
                    ClearMunitionOverride();
                    return;
                }
                if (verdict == MunitionFeedVerdict.Hold) {
                    //在途扣弹已满，停一拍等镜像回同步，覆写保持
                    return;
                }
                fireCooldown = MunitionFireInterval;
                Vector2 velocity = (target.Center - MuzzlePos).SafeNormalize(Vector2.UnitX) * 11f;
                SpawnShot(velocity, munitionProjType, munitionDamage, hitsPlayers: false);
            }
            else {
                fireCooldown = HijackFireInterval;
                Vector2 velocity = (target.Center - MuzzlePos).SafeNormalize(Vector2.UnitX) * 12f;
                SpawnShot(velocity, ProjectileID.LaserMachinegunLaser, FriendlyShotDamage(), hitsPlayers: false);
            }
            PlayMuzzle();
        }

        private Player ResolveFeeder() {
            if (munitionFeederIndex < 0 || munitionFeederIndex >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[munitionFeederIndex];
            return player?.active == true && !player.dead ? player : null;
        }

        private Player FindPlayerTarget() {
            Player best = null;
            float bestDistSq = PlayerSeekRange * PlayerSeekRange;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true || player.dead || player.ghost) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(player.Center, HeadCenter);
                if (distSq > bestDistSq) {
                    continue;
                }
                if (!Collision.CanHitLine(HeadCenter, 1, 1, player.Center, 1, 1)) {
                    continue;
                }
                bestDistSq = distSq;
                best = player;
            }
            return best;
        }

        private NPC FindNpcTarget() {
            NPC best = null;
            float bestDistSq = NpcSeekRange * NpcSeekRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, HeadCenter);
                if (distSq > bestDistSq) {
                    continue;
                }
                if (!Collision.CanHitLine(HeadCenter, 1, 1, npc.Center, 1, 1)) {
                    continue;
                }
                bestDistSq = distSq;
                best = npc;
            }
            return best;
        }

        private void RotateToward(Vector2 worldTarget) {
            float desired = (worldTarget - HeadCenter).ToRotation();
            aimRotation = aimRotation.AngleLerp(desired, 0.1f);
        }

        private bool AimSettled(Vector2 worldTarget) {
            float desired = (worldTarget - HeadCenter).ToRotation();
            return MathF.Abs(MathHelper.WrapAngle(desired - aimRotation)) < 0.14f;
        }

        private void SpawnShot(Vector2 velocity, int projType, int damage, bool hitsPlayers) {
            if (projType <= 0 || damage <= 0) {
                return;
            }
            int idx = Projectile.NewProjectile(new EntitySource_Misc("CWRSentryTurret"),
                MuzzlePos, velocity, projType, damage, 2f, Main.myPlayer);
            if (idx < 0 || idx >= Main.maxProjectiles) {
                return;
            }
            Projectile proj = Main.projectile[idx];
            if (hitsPlayers) {
                proj.friendly = false;
                proj.hostile = true;
                proj.npcProj = true;
            }
            else {
                //trap 语义：友方、能打敌怪、不吃玩家面板加成，机关弹的原生路径
                proj.friendly = true;
                proj.hostile = false;
                proj.trap = true;
            }
            //生成包在 NewProjectile 内部就已发出，改完旗标要在服务器上补一发全量同步
            if (Main.netMode == NetmodeID.Server) {
                Terraria.NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, idx);
            }
        }

        private void PlayMuzzle() {
            muzzleFlash = 1f;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.55f, Pitch = -0.2f }, MuzzlePos);
            }
        }

        /// <summary>敌对弹按世界进度定伤，命中玩家时原版还会翻倍</summary>
        internal static int HostileShotDamage() {
            if (NPC.downedMoonlord) return 55;
            if (NPC.downedPlantBoss) return 34;
            if (Main.hardMode) return 22;
            return 12;
        }

        /// <summary>策反/组网的友方弹伤害，随进度爬升</summary>
        internal static int FriendlyShotDamage() {
            if (NPC.downedMoonlord) return 160;
            if (NPC.downedPlantBoss) return 90;
            if (Main.hardMode) return 55;
            return 26;
        }

        private void EmitAmbient() {
            Color light = CurrentLight();
            Lighting.AddLight(HeadCenter, light.ToVector3() * 0.24f);

            if (disabledFrames > 0 && ++sparkTimer >= 22) {
                sparkTimer = 0;
                Vector2 offset = Main.rand.NextVector2Circular(14f, 12f);
                PRTLoader.NewParticle<PRT_Spark>(HeadCenter + offset,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.2f, -0.6f)),
                    new Color(120, 200, 255), 0.55f)?.Configure(true, 20);
            }
        }
        #endregion

        #region IHackableTurret
        public Actor AsActor => this;

        public bool IsCircuitDisabled => disabledFrames > 0;

        public int CircuitDisabledFrames => disabledFrames;

        public void ApplyShortCircuit(int frames, Player caster) {
            if (VaultUtils.isClient || frames <= 0) {
                return;
            }
            disabledFrames = Math.Max(disabledFrames, frames);
            hijackFrames = 0;
            hijackOwnerIndex = -1;
            ClearMunitionOverride();
            warmup = 0f;
        }

        public void ApplyCircuitOverload(int frames, Player caster) {
            ApplyShortCircuit(frames, caster);
        }

        public void ApplyHijack(int frames, Player caster) {
            if (VaultUtils.isClient || frames <= 0 || disabledFrames > 0) {
                return;
            }
            hijackFrames = Math.Max(hijackFrames, frames);
            hijackOwnerIndex = caster?.whoAmI ?? -1;
            warmup = 0f;
        }
        #endregion

        #region IMunitionFeedTurret
        public bool MunitionOverrideActive => munitionFrames > 0;

        public int MunitionAmmoType => munitionFrames > 0 ? munitionAmmoType : 0;

        public void ApplyMunitionOverride(int ammoItemType, int projType, int damage, Player feeder, int frames) {
            if (VaultUtils.isClient || frames <= 0 || feeder == null) {
                return;
            }
            munitionFrames = frames;
            munitionAmmoType = ammoItemType;
            munitionProjType = projType;
            munitionDamage = Math.Max(1, damage);
            munitionFeederIndex = feeder.whoAmI;
            //上一次覆写的在途扣弹账不许带进这一次
            MunitionSwap.ForgetPending(NetKey);
            //供弹隐含翻转 IFF，停摆保持，置换不了一台断电的炮
            warmup = 0f;
        }

        public void ClearMunitionOverride() {
            munitionFrames = 0;
            munitionAmmoType = 0;
            munitionProjType = 0;
            munitionDamage = 0;
            munitionFeederIndex = -1;
            MunitionSwap.ForgetPending(NetKey);
        }
        #endregion

        #region IMeshFireTurret
        public void JoinMesh(int rootSlot, int frames) {
            if (VaultUtils.isClient || frames <= 0) {
                return;
            }
            //联网顺手唤醒停摆成员，这是设计明写的
            disabledFrames = 0;
            hijackFrames = 0;
            ClearMunitionOverride();
            meshFrames = frames;
            meshRootSlot = rootSlot;
            meshAimPoint = HeadCenter + aimRotation.ToRotationVector2() * 60f;
        }

        public void LeaveMesh() {
            meshFrames = 0;
            meshRootSlot = -1;
        }

        public void SetMeshAim(Vector2 worldTarget) {
            meshAimPoint = worldTarget;
        }

        public void MeshFire(Vector2 worldTarget, Player caster) {
            if (VaultUtils.isClient || disabledFrames > 0) {
                return;
            }
            Vector2 velocity = (worldTarget - MuzzlePos).SafeNormalize(Vector2.UnitX) * 13f;
            SpawnShot(velocity, ProjectileID.LaserMachinegunLaser, FriendlyShotDamage(), hitsPlayers: false);
            aimRotation = velocity.ToRotation();
            PlayMuzzle();
        }
        #endregion

        #region IHackTarget / IScannable
        public HackTargetType TargetType => HackTargetType.Get<TurretTargetType>();

        public Vector2 WorldCenter => Center;

        public bool IsValid => Active;

        public bool IsHackable => true;

        public Vector2 LockFrameHalfSize => new(Width * 0.6f + 26f, Height * 0.6f + 26f);

        public string LockFrameTitle => CircuitNodeSpawner.TurretName.Value;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            if (disabledFrames > 0) {
                text = FormatSeconds(CircuitNodeSpawner.StatusDisabled.Value, disabledFrames);
                color = HackTheme.TextDim;
                return true;
            }
            if (meshFrames > 0) {
                text = CircuitNodeSpawner.StatusMeshed.Value;
                color = MeshLight;
                return true;
            }
            if (munitionFrames > 0) {
                text = CircuitNodeSpawner.StatusMunition.Value;
                color = MunitionLight;
                return true;
            }
            if (hijackFrames > 0) {
                text = FormatSeconds(CircuitNodeSpawner.StatusHijacked.Value, hijackFrames);
                color = FriendlyLight;
                return true;
            }
            text = CircuitNodeSpawner.StatusOnline.Value;
            color = HackTheme.Danger;
            return true;
        }

        public int ScanRowCount => 6;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            labels[0] = HackTime.TypeLabel.Value;
            values[0] = CircuitNodeSpawner.TurretName.Value;
            colors[0] = HackTheme.TextBright;

            labels[1] = CircuitNodeSpawner.StatusLabel.Value;
            (values[1], colors[1]) = DescribeStatus();

            bool friendlySide = hijackFrames > 0 || munitionFrames > 0 || meshFrames > 0;
            labels[2] = CircuitNodeSpawner.IffLabel.Value;
            values[2] = friendlySide
                ? CircuitNodeSpawner.IffFriendly.Value
                : CircuitNodeSpawner.IffHostile.Value;
            colors[2] = friendlySide ? HackTheme.Accent : HackTheme.Danger;

            int interval = munitionFrames > 0 ? MunitionFireInterval
                : hijackFrames > 0 ? HijackFireInterval : NativeFireInterval;
            labels[3] = CircuitNodeSpawner.RateLabel.Value;
            values[3] = $"{3600 / interval}/min";
            colors[3] = HackTheme.TextBright;

            labels[4] = CircuitNodeSpawner.MunitionLabel.Value;
            values[4] = munitionFrames > 0 && munitionAmmoType > 0
                ? Lang.GetItemNameValue(munitionAmmoType)
                : CircuitNodeSpawner.MunitionKinetic.Value;
            colors[4] = munitionFrames > 0 ? MunitionLight : HackTheme.TextNormal;

            labels[5] = CircuitNodeSpawner.RangeLabel.Value;
            values[5] = $"{(int)(PlayerSeekRange / 16f)}";
            colors[5] = HackTheme.TextNormal;
        }

        private (string, Color) DescribeStatus() {
            if (disabledFrames > 0) {
                return (FormatSeconds(CircuitNodeSpawner.StatusDisabled.Value, disabledFrames), HackTheme.TextDim);
            }
            if (meshFrames > 0) {
                return (CircuitNodeSpawner.StatusMeshed.Value, MeshLight);
            }
            if (munitionFrames > 0) {
                return (CircuitNodeSpawner.StatusMunition.Value, MunitionLight);
            }
            if (hijackFrames > 0) {
                return (CircuitNodeSpawner.StatusHijacked.Value, FriendlyLight);
            }
            return (CircuitNodeSpawner.StatusOnline.Value, HackTheme.Danger);
        }

        private static string FormatSeconds(string label, int frames)
            => $"{label} {MathF.Ceiling(frames / 60f):0}s";

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            return HackEffectTracker.ApplyAuthorityEffect(hack, this, casterIndex, 0, 0, 0f, 0) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is SentryTurretActor turret
                && turret.WhoAmI == WhoAmI && turret.Generation == Generation;
        }
        #endregion

        #region 绘制
        private Color CurrentLight() {
            if (disabledFrames > 0) return DeadLight;
            if (meshFrames > 0) return MeshLight;
            if (munitionFrames > 0) return MunitionLight;
            if (hijackFrames > 0) return FriendlyLight;
            return HostileLight;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Texture2D px = HackTheme.Pixel;
            if (px == null) {
                return false;
            }

            Vector2 basePos = Position - Main.screenPosition;
            Vector2 headPos = HeadCenter - Main.screenPosition;
            Color body = Color.Lerp(drawColor, new Color(46, 56, 66), 0.7f);
            Color bodyDark = Color.Lerp(drawColor, new Color(24, 30, 38), 0.75f);
            Color statusLight = CurrentLight();
            float pulse = MathF.Sin(glowTimer) * 0.5f + 0.5f;

            //底座锚板 + 立柱
            spriteBatch.Draw(px, new Rectangle((int)basePos.X - 4, (int)(basePos.Y + Height - 8), Width + 8, 8), HackTheme.SrcPixel, bodyDark);
            spriteBatch.Draw(px, new Rectangle((int)(basePos.X + Width / 2f - 5), (int)(basePos.Y + Height - 26), 10, 20), HackTheme.SrcPixel, body);

            //云台主体，蓄能时向目标侧微倾读作紧张
            Vector2 headSize = new(28f, 18f);
            spriteBatch.Draw(px, headPos, HackTheme.SrcPixel, body, aimRotation * 0.12f,
                new Vector2(0.5f), headSize, SpriteEffects.None, 0f);
            spriteBatch.Draw(px, headPos, HackTheme.SrcPixel, bodyDark, aimRotation * 0.12f,
                new Vector2(0.5f), headSize - new Vector2(6f, 6f), SpriteEffects.None, 0f);

            //炮管
            Vector2 dir = aimRotation.ToRotationVector2();
            HackTheme.DrawLine(spriteBatch, headPos, headPos + dir * 24f, 5f, bodyDark);
            HackTheme.DrawLine(spriteBatch, headPos, headPos + dir * 22f, 2.4f,
                Color.Lerp(body, statusLight, 0.35f));

            //状态灯：停摆暗、敌对随蓄能加速闪
            float lightAlpha = disabledFrames > 0
                ? 0.25f + pulse * 0.1f
                : 0.55f + pulse * 0.35f + warmup * 0.4f;
            HackTheme.DrawDiamond(spriteBatch, headPos + new Vector2(0f, -14f), 5f, statusLight * lightAlpha);

            //蓄能瞄准线，开火预警
            if (warmup > 0.35f && disabledFrames <= 0 && meshFrames <= 0) {
                Color sight = statusLight * (0.14f + warmup * 0.3f);
                HackTheme.DrawDashedLine(spriteBatch, headPos + dir * 26f, headPos + dir * 320f, 1f, sight, 7f, 9f);
            }

            //炮口闪
            if (muzzleFlash > 0.05f) {
                HackTheme.DrawDiamond(spriteBatch, headPos + dir * 26f, 8f * muzzleFlash,
                    Color.Lerp(statusLight, Color.White, 0.6f) * muzzleFlash);
            }

            //停摆遮罩
            if (disabledFrames > 0) {
                Rectangle box = new((int)basePos.X, (int)basePos.Y, Width, Height);
                HackTheme.DrawHatch(spriteBatch, box, 9f, DeadLight * 0.28f);
            }
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            //组网数据链：成员画向根节点的虚线，穿墙可见
            if (meshFrames <= 0 || meshRootSlot < 0 || meshRootSlot >= ActorLoader.MaxActorCount) {
                return;
            }
            Actor root = ActorLoader.Actors?[meshRootSlot];
            if (root is not SentryTurretActor rootTurret || !root.Active) {
                return;
            }
            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            Vector2 from = HeadCenter - Main.screenPosition;
            Vector2 to = rootTurret.HeadCenter - Main.screenPosition;
            HackTheme.DrawDashedLine(spriteBatch, from, to, 1.2f,
                HackTheme.Accent * (0.35f + pulse * 0.3f), 9f, 7f);
            if (meshRootSlot == WhoAmI) {
                //根节点自己画一枚组网徽标
                HackTheme.DrawDiamondOutline(spriteBatch, from + new Vector2(0f, -26f), 7f, 1.5f,
                    HackTheme.Accent * (0.6f + pulse * 0.4f));
            }
        }
        #endregion
    }
}
