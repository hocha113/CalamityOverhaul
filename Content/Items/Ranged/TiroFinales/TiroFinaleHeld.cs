using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 终焉圆舞曲持握。左键实弹+魔力双消耗开火，每发以金丝带在身周织出一支幻影燧发枪，
    /// 至多八支组成 3D 环绕枪阵；按住左键时枪阵轮转瞄准光标齐射
    /// (<see cref="FinaleMagicBolt"/>)，每支只鸣一发即散解。右键收束全阵奏响终曲：
    /// 幻影枪螺旋汇聚成巨炮，蓄势后轰出 <see cref="TiroFinaleBlast"/>。<br/>
    /// 3D 姿态数学在 <see cref="TiroFinaleRig"/>，绘制装配在 <see cref="TiroFinaleRenderer"/>
    /// </summary>
    internal class TiroFinaleHeld : BaseHeldGun, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Ranged + "TiroFinale";
        public override int TargetID => ModContent.ItemType<TiroFinale>();
        public override bool CanRightClick => true;

        //——贴图锚点(290x80，炮口朝右)——
        internal const float HeldScale = 0.55f;
        internal static readonly Vector2 GripPx = new(48, 44);
        internal static readonly Vector2 MuzzlePx = new(288, 30);
        /// <summary>贴图中心到枪口的前向距离(px)</summary>
        internal const float MuzzleFromCenter = 143f;
        /// <summary>枪管中线相对贴图中心的法向偏移(px，负=偏上)</summary>
        internal const float BarrelFromCenter = -10f;

        //——枪阵槽位相位——
        internal const byte PhaseEmpty = 0;
        internal const byte PhaseForming = 1;
        internal const byte PhaseReady = 2;
        internal const byte PhaseAiming = 3;
        internal const byte PhaseFading = 4;
        internal const float FormTime = 16f;
        internal const float AimTime = 7f;
        internal const float FadeTime = 18f;
        /// <summary>齐射轮转间隔(帧)</summary>
        private const float VolleyGap = 8f;
        /// <summary>幻影枪基准缩放</summary>
        internal const float MusketScale = 0.38f;
        /// <summary>枪口魔法阵余辉帧数</summary>
        internal const float CircleLife = 22f;

        //——终曲相位——
        internal const byte FinaleNone = 0;
        internal const byte FinaleGather = 1;
        internal const byte FinaleManifest = 2;
        internal const byte FinaleCharge = 3;
        internal const byte FinaleBlast = 4;
        internal const byte FinaleFade = 5;
        internal const float GatherTime = 20f;
        internal const float ManifestTime = 24f;
        internal const float ChargeTime = 18f;
        internal const float BlastTime = 2f;
        internal const float FinaleFadeTime = 26f;
        /// <summary>奏响终曲所需的最少在阵枪数</summary>
        private const int FinaleMinCount = 4;
        /// <summary>终曲额外魔力</summary>
        private const int FinaleMana = 20;
        /// <summary>终曲巨炮缩放</summary>
        internal const float GiantScale = 1.6f;

        internal readonly byte[] slotPhase = new byte[TiroFinaleRig.SlotCount];
        internal readonly float[] slotTimer = new float[TiroFinaleRig.SlotCount];
        /// <summary>逐槽枪口阵余辉(纯视觉，不入网络包)</summary>
        internal readonly float[] slotCircle = new float[TiroFinaleRig.SlotCount];
        private float volleyTimer;
        private int volleyCursor;
        /// <summary>手中枪的枪口阵余辉(纯视觉)</summary>
        internal float handCircle;
        internal byte finalePhase;
        internal float finaleTimer;
        /// <summary>终曲收束时吃掉的枪数，决定巨弹伤害</summary>
        internal int finaleCount;

        /// <summary>环心:玩家胸口略上，枪阵绕此点进动</summary>
        internal Vector2 RingCenter => Owner.GetPlayerStabilityCenter() + new Vector2(0f, -8f);

        /// <summary>阵内仍算"活着"的枪数(成形中/就绪/瞄准中)</summary>
        internal int LiveMusketCount() {
            int n = 0;
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (slotPhase[i] == PhaseForming || slotPhase[i] == PhaseReady || slotPhase[i] == PhaseAiming) {
                    n++;
                }
            }
            return n;
        }

        private bool AnySlotAlive() {
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (slotPhase[i] != PhaseEmpty) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>枪阵未散、终曲未毕就别收枪</summary>
        public override bool StayAlive() => finalePhase != FinaleNone || AnySlotAlive() || handCircle > 0f;

        public override SoundStyle? ShootSound => SoundID.Item36 with {
            Volume = 0.5f,
            Pitch = -0.12f,
            PitchVariance = 0.06f,
            MaxInstances = 5,
        };

        public override void SetGunProperty() {
            HandIdleDistanceX = 16;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 17;
            HandFireDistanceY = -4;
            MuzzleForwardOffset = (MuzzlePx.X - GripPx.X) * HeldScale;
            MuzzleNormalOffset = (MuzzlePx.Y - GripPx.Y) * HeldScale;
            GunPressure = 0.09f;
            ControlForce = 0.032f;
            RecoilOffsetRecoverValue = 0.82f;
            FireLight = 0;//火光自己画，走金色
        }

        public override void NetHeldSend(BinaryWriter writer) {
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                writer.Write(slotPhase[i]);
                writer.Write((byte)MathHelper.Clamp(slotTimer[i], 0f, 250f));
            }
            writer.Write((byte)volleyCursor);
            writer.Write(finalePhase);
            writer.Write((byte)MathHelper.Clamp(finaleTimer, 0f, 250f));
            writer.Write((byte)finaleCount);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                slotPhase[i] = reader.ReadByte();
                slotTimer[i] = reader.ReadByte();
            }
            volleyCursor = reader.ReadByte();
            finalePhase = reader.ReadByte();
            finaleTimer = reader.ReadByte();
            finaleCount = reader.ReadByte();
        }

        /// <summary>在场帧戳：AI 与绘制各盖一次（时停中 AI 停摆靠绘制维持），远半环层据此跳过空表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        public override void AI() {
            PresenceStamp.Stamp();
            if (finalePhase != FinaleNone) {
                UpdateHeldPose(true);
                FinaleAI();
                UpdateSlots();
                Time++;
                return;
            }

            UpdateHeldPose(CanFire);

            if (WantsFireLeft && FireCooldown <= 0f && HasAmmo) {
                Fire();
            }
            TryStartFinale();
            UpdateSlots();
            UpdateVolley();

            if (handCircle > 0f) {
                handCircle--;
            }

            int live = LiveMusketCount();
            if (live > 0) {
                Lighting.AddLight(RingCenter, new Vector3(0.85f, 0.68f, 0.3f) * (0.05f + 0.045f * live));
            }
            Time++;
        }

        #region 左键:实弹+织枪
        private void Fire() {
            //魔力:主人端真扣，远端用同步魔力近似判断
            int manaCost = (int)(Item.mana * Owner.manaCost);
            bool manaOk = Projectile.IsOwnedByLocalPlayer() ? PayMana() : Owner.statMana >= manaCost;
            if (!manaOk) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
                }
                FireCooldown = 12f;
                return;
            }

            SnapToAimPose();
            PlayShootSound();
            //金铃轻响垫在枪声底下，标记"这一发也在织魔法"
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.16f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);

            RecoilPitch = MathF.Min(RecoilPitch + 0.055f, GunPressure * 2f);
            RecoilOffset -= UnitToMouseV * 3.4f;

            Vector2 muzzle = ShootPos;
            MuzzleFlashFX(muzzle, UnitToMouseV, 1f);
            handCircle = CircleLife;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vel = ShootVelocity.RotatedByRandom(0.02f);
                if (AmmoTypes == ProjectileID.Bullet) {
                    //普通子弹转化为金色铅弹，特殊弹药保持原样
                    Projectile.NewProjectile(Source, muzzle, vel, ModContent.ProjectileType<FinaleMusketRound>()
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI, Main.rand.NextFloat(9f));
                }
                else {
                    Projectile.NewProjectile(Source, muzzle, vel, AmmoTypes
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI);
                }
                NetUpdate();
            }
            ConsumeAmmo();
            SummonMusket();
            SetFireCooldown();
        }

        /// <summary>从巡位下一格开始找空槽织枪，入环位置更分散</summary>
        private void SummonMusket() {
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                int slot = (volleyCursor + 1 + i) % TiroFinaleRig.SlotCount;
                if (slotPhase[slot] == PhaseEmpty) {
                    slotPhase[slot] = PhaseForming;
                    slotTimer[slot] = 0f;
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.28f, Pitch = 0.5f, MaxInstances = 4 }, RingCenter);
                    return;
                }
            }
        }
        #endregion

        #region 枪阵:相位推进与轮转齐射
        private void UpdateSlots() {
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                switch (slotPhase[i]) {
                    case PhaseForming:
                        if ((slotTimer[i] += 1f) >= FormTime) {
                            slotPhase[i] = PhaseReady;
                            slotTimer[i] = 0f;
                        }
                        break;
                    case PhaseAiming:
                        if ((slotTimer[i] += 1f) >= AimTime) {
                            FireSlot(i);
                        }
                        break;
                    case PhaseFading:
                        if ((slotTimer[i] += 1f) >= FadeTime) {
                            slotPhase[i] = PhaseEmpty;
                            slotTimer[i] = 0f;
                        }
                        break;
                }
                if (slotCircle[i] > 0f) {
                    slotCircle[i]--;
                }
            }
        }

        private void UpdateVolley() {
            if (volleyTimer > 0f) {
                volleyTimer--;
            }
            if (!WantsFireLeft || volleyTimer > 0f || finalePhase != FinaleNone) {
                return;
            }
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                int slot = (volleyCursor + 1 + i) % TiroFinaleRig.SlotCount;
                if (slotPhase[slot] == PhaseReady) {
                    slotPhase[slot] = PhaseAiming;
                    slotTimer[slot] = 0f;
                    volleyCursor = slot;
                    volleyTimer = VolleyGap;
                    return;
                }
            }
        }

        /// <summary>幻影枪鸣响:出弹、口花、散解</summary>
        private void FireSlot(int slot) {
            slotPhase[slot] = PhaseFading;
            slotTimer[slot] = 0f;
            slotCircle[slot] = CircleLife;

            if (!ComputeMusketPose(slot, out MusketPose pose)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item36 with {
                Volume = 0.3f,
                Pitch = 0.16f,
                PitchVariance = 0.09f,
                MaxInstances = 6,
            }, pose.MuzzleWorld);
            MuzzleFlashFX(pose.MuzzleWorld, pose.Rotation.ToRotationVector2(), 0.7f);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vel = (InMousePos - pose.MuzzleWorld).SafeNormalize(UnitToMouseV) * (AmmoState.ShootSpeed * 1.05f);
                Projectile.NewProjectile(Source, pose.MuzzleWorld, vel, ModContent.ProjectileType<FinaleMagicBolt>()
                    , (int)(WeaponDamage * 0.7f), WeaponKnockback * 0.6f, Owner.whoAmI, Main.rand.NextFloat(9f));
            }
        }
        #endregion

        #region 终曲
        /// <summary>右键奏终曲:主人端裁定，远端靠快照跟进</summary>
        private void TryStartFinale() {
            if (!Projectile.IsOwnedByLocalPlayer() || finalePhase != FinaleNone) {
                return;
            }
            if (!WantsFireRight || FireCooldown > 0f) {
                return;
            }
            int live = LiveMusketCount();
            if (live < FinaleMinCount) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = -0.2f }, Projectile.Center);
                FireCooldown = 16f;
                return;
            }
            if (!TryConsumeMana(FinaleMana)) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
                FireCooldown = 16f;
                return;
            }

            finalePhase = FinaleGather;
            finaleTimer = 0f;
            finaleCount = live;
            //整段终曲期间锁普通开火与切枪
            FireCooldown = GatherTime + ManifestTime + ChargeTime + BlastTime + FinaleFadeTime + 8f;
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.15f }, Owner.Center);
            NetUpdate();
        }

        private void FinaleAI() {
            finaleTimer++;
            switch (finalePhase) {
                case FinaleGather:
                    //收束:金屑自槽位涌向汇聚点
                    GatherSparkFX();
                    if (finaleTimer >= GatherTime) {
                        for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                            slotPhase[i] = PhaseEmpty;
                            slotTimer[i] = 0f;
                        }
                        StepFinale(FinaleManifest);
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.55f, Pitch = 0.35f }, Owner.Center);
                    }
                    break;
                case FinaleManifest:
                    if (finaleTimer >= ManifestTime) {
                        StepFinale(FinaleCharge);
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.4f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.5f }, Owner.Center);
                    }
                    break;
                case FinaleCharge:
                    ChargeIntakeFX();
                    if (finaleTimer >= ChargeTime) {
                        StepFinale(FinaleBlast);
                    }
                    break;
                case FinaleBlast:
                    if (finaleTimer == 1f) {
                        FireFinale();
                    }
                    if (finaleTimer >= BlastTime) {
                        StepFinale(FinaleFade);
                    }
                    break;
                case FinaleFade:
                    if (finaleTimer >= FinaleFadeTime) {
                        StepFinale(FinaleNone);
                        finaleCount = 0;
                    }
                    break;
            }
        }

        private void StepFinale(byte next) {
            finalePhase = next;
            finaleTimer = 0f;
        }

        /// <summary>终曲巨炮的世界姿态；reveal=显现包络 0~1</summary>
        internal void ComputeGiantPose(out Vector2 world, out float rotation, out float scale, out float reveal, out Vector2 muzzle, out bool facingRight) {
            world = Owner.GetPlayerStabilityCenter() + new Vector2(0f, -46f);
            Vector2 toAim = InMousePos - world;
            rotation = toAim.ToRotation();
            facingRight = toAim.X >= 0f;

            reveal = finalePhase switch {
                FinaleManifest => finaleTimer / ManifestTime,
                FinaleCharge or FinaleBlast => 1f,
                FinaleFade => 1f - finaleTimer / FinaleFadeTime,
                _ => 0f,
            };
            //显现末端带一点过冲弹性
            float pop = finalePhase == FinaleManifest ? EaseOutBack(MathHelper.Clamp(reveal, 0f, 1f)) : 1f;
            scale = GiantScale * (0.86f + 0.14f * pop);

            //发射帧巨炮猛地后坐
            if (finalePhase == FinaleBlast || (finalePhase == FinaleFade && finaleTimer < 8f)) {
                float kick = finalePhase == FinaleBlast ? 1f : 1f - finaleTimer / 8f;
                world -= rotation.ToRotationVector2() * (18f * kick);
            }

            float flip = facingRight ? 1f : -1f;
            Vector2 fwd = rotation.ToRotationVector2();
            Vector2 perp = (rotation + MathHelper.PiOver2).ToRotationVector2();
            muzzle = world + fwd * (MuzzleFromCenter * scale) + perp * (BarrelFromCenter * scale * flip);
        }

        private void FireFinale() {
            ComputeGiantPose(out _, out float rot, out float scale, out _, out Vector2 muzzle, out _);
            Vector2 dir = rot.ToRotationVector2();

            //三层轰鸣:炮膛爆压+魔力洪流+余响
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.35f }, muzzle);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = -0.2f }, muzzle);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 0.85f, Pitch = -0.4f }, muzzle);

            RecoilPitch = GunPressure * 2f;
            RecoilOffset -= dir * 15f;

            if (!Main.dedServ && Main.LocalPlayer.Distance(muzzle) < 1300f) {
                Main.LocalPlayer.CWR().GetScreenShake(9f);
            }
            MuzzleFlashFX(muzzle, dir, 2.6f);

            if (Projectile.IsOwnedByLocalPlayer()) {
                //巨炮把射手推退一截
                Owner.velocity -= dir * 6.5f;
                int dmg = (int)(WeaponDamage * 2.8f * finaleCount);
                Projectile.NewProjectile(Source, muzzle, dir * 24f, ModContent.ProjectileType<TiroFinaleBlast>()
                    , dmg, WeaponKnockback * 3f, Owner.whoAmI, scale);
                NetUpdate();
            }
        }

        /// <summary>收束期:各活槽向汇聚点洒金屑</summary>
        private void GatherSparkFX() {
            if (VaultUtils.isServer || (int)finaleTimer % 2 != 0) {
                return;
            }
            Vector2 gatherPos = Owner.GetPlayerStabilityCenter() + new Vector2(0f, -46f);
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (slotPhase[i] == PhaseEmpty || !ComputeMusketPose(i, out MusketPose pose)) {
                    continue;
                }
                Vector2 toGather = (gatherPos - pose.World).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(pose.World + Main.rand.NextVector2Circular(10f, 10f), toGather
                    , new Color(255, 218, 130), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>蓄势期:光屑被吸入巨炮口</summary>
        private void ChargeIntakeFX() {
            if (VaultUtils.isServer) {
                return;
            }
            ComputeGiantPose(out _, out _, out _, out _, out Vector2 muzzle, out _);
            for (int i = 0; i < 2; i++) {
                Vector2 from = muzzle + Main.rand.NextVector2CircularEdge(52f, 52f);
                Vector2 vel = (muzzle - from).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(4f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(from, vel, new Color(255, 226, 150), Main.rand.NextFloat(0.32f, 0.5f))
                    ?.Configure(true, Main.rand.Next(7, 11));
            }
            Lighting.AddLight(muzzle, new Vector3(1f, 0.86f, 0.5f) * (0.5f + 0.6f * (finaleTimer / ChargeTime)));
        }
        #endregion

        #region 幻影枪姿态
        internal struct MusketPose
        {
            /// <summary>贴图中心的世界位置(已投影)</summary>
            public Vector2 World;
            /// <summary>枪口世界位置</summary>
            public Vector2 MuzzleWorld;
            /// <summary>屏幕旋转</summary>
            public float Rotation;
            /// <summary>轴向缩短系数(1=贴屏平面)</summary>
            public float AxialK;
            /// <summary>透视缩放</summary>
            public float PScale;
            /// <summary>纵深光照</summary>
            public float Lit;
            /// <summary>排序/分层用 z</summary>
            public float Z;
            /// <summary>相位透明包络</summary>
            public float Alpha;
            /// <summary>显现进度(喂 uForm)</summary>
            public float Form;
            /// <summary>开火过曝(喂 uFire)</summary>
            public float Fire;
            public bool FacingRight;
            /// <summary>最终基准缩放(不含轴向缩短)</summary>
            public float Scale;
        }

        /// <summary>计算某槽幻影枪的完整屏幕姿态；空槽返回 false</summary>
        internal bool ComputeMusketPose(int slot, out MusketPose pose) {
            pose = default;
            byte phase = slotPhase[slot];
            if (phase == PhaseEmpty) {
                return false;
            }

            float time = Time;
            TiroFinaleRig.GetBasis(time, out Vector3 e1, out Vector3 e2);

            //显现期从环外飘入
            float radiusMul = 1f;
            float formT = 1f;
            if (phase == PhaseForming) {
                formT = MathHelper.Clamp(slotTimer[slot] / FormTime, 0f, 1f);
                radiusMul = MathHelper.Lerp(1.22f, 1f, EaseOutCubic(formT));
            }
            Vector3 pos3 = TiroFinaleRig.SlotLocal(slot, time, e1, e2, radiusMul);

            Vector2 ringCenter = RingCenter;

            //终曲收束:螺旋收拢并压向汇聚点
            if (finalePhase == FinaleGather) {
                float g = MathHelper.Clamp(finaleTimer / GatherTime, 0f, 1f);
                float swirl = g * g * 2.6f;
                float shrink = 1f - 0.92f * (g * g);
                //在环平面内加速旋进
                float angle = TiroFinaleRig.SlotAngle(slot, time) + swirl;
                pos3 = (e1 * MathF.Cos(angle) + e2 * MathF.Sin(angle)) * (TiroFinaleRig.Radius * shrink);
                Vector3 gatherLocal = new(0f, -38f, 0f);
                pos3 = Vector3.Lerp(pos3, gatherLocal, g * g);
            }

            Vector2 offset2 = TiroFinaleRig.Project(pos3, out float pscale);

            //瞄准:朝同步鼠标位；瞄准相位期收敛微摆并带预备顿挫
            Vector2 targetLocal = InMousePos - ringCenter;
            TiroFinaleRig.AimScreen(pos3, targetLocal, out float rot, out float axialK);

            float sway = MathF.Sin(time * 0.07f + slot * 2.1f) * 0.055f;
            float thrust = 0f;
            if (phase == PhaseAiming) {
                float t = slotTimer[slot] / AimTime;
                sway *= 1f - t;
                //先后拉再前送，开火帧顶到最前
                thrust = t < 0.6f ? -4f * (t / 0.6f) : MathHelper.Lerp(-4f, 3f, (t - 0.6f) / 0.4f);
            }
            rot += sway;

            bool facingRight = MathF.Cos(rot) >= 0f;
            float flip = facingRight ? 1f : -1f;

            float alpha = 1f;
            float form = 1f;
            float fire = 0f;
            Vector2 drift = Vector2.Zero;
            float tumble = 0f;

            switch (phase) {
                case PhaseForming:
                    alpha = formT;
                    form = formT;
                    //成形中枪身从上扬姿态落回瞄准线
                    tumble = (1f - EaseOutCubic(formT)) * 0.7f * flip;
                    break;
                case PhaseFading: {
                    float t = MathHelper.Clamp(slotTimer[slot] / FadeTime, 0f, 1f);
                    alpha = 1f - t * t;
                    form = 1f - t * 0.9f;
                    fire = MathHelper.Clamp(1f - slotTimer[slot] / 3f, 0f, 1f);
                    //后坐:沿枪轴向后漂退+枪口上跳
                    drift = -rot.ToRotationVector2() * (EaseOutCubic(t) * 30f);
                    tumble = -0.24f * t * flip;
                    break;
                }
            }
            //终曲收束期通体渐亮
            if (finalePhase == FinaleGather) {
                float g = MathHelper.Clamp(finaleTimer / GatherTime, 0f, 1f);
                fire = MathF.Max(fire, MathHelper.Clamp((g - 0.66f) / 0.34f, 0f, 1f) * 0.75f);
            }

            rot -= tumble;

            pose.World = ringCenter + offset2 + drift + rot.ToRotationVector2() * thrust;
            pose.Rotation = rot;
            pose.AxialK = axialK;
            pose.PScale = pscale;
            pose.Lit = TiroFinaleRig.DepthLit(pos3.Z, TiroFinaleRig.Radius);
            pose.Z = pos3.Z;
            pose.Alpha = alpha;
            pose.Form = form;
            pose.Fire = fire;
            pose.FacingRight = facingRight;
            pose.Scale = MusketScale * pscale;
            Vector2 fwd = rot.ToRotationVector2();
            Vector2 perp = (rot + MathHelper.PiOver2).ToRotationVector2();
            pose.MuzzleWorld = pose.World + fwd * (MuzzleFromCenter * pose.Scale * axialK) + perp * (BarrelFromCenter * pose.Scale * flip);
            return true;
        }
        #endregion

        #region 表现
        /// <summary>金色口花:火星+光</summary>
        private void MuzzleFlashFX(Vector2 pos, Vector2 dir, float power) {
            if (VaultUtils.isServer) {
                return;
            }
            int n = (int)(5 * power);
            for (int i = 0; i < n; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos, dir.RotatedBy(Main.rand.NextFloat(-0.32f, 0.32f)) * Main.rand.NextFloat(3f, 9f) * power
                    , new Color(255, 214, 120), Main.rand.NextFloat(0.35f, 0.6f) * MathF.Min(power, 1.4f))
                    ?.Configure(true, Main.rand.Next(7, 12));
            }
            Lighting.AddLight(pos, new Vector3(1f, 0.85f, 0.45f) * (0.55f * power));
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收枪:阵中残余的幻影枪散成金屑
            for (int i = 0; i < TiroFinaleRig.SlotCount; i++) {
                if (slotPhase[i] == PhaseEmpty || !ComputeMusketPose(i, out MusketPose pose)) {
                    continue;
                }
                for (int k = 0; k < 5; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(pose.World + Main.rand.NextVector2Circular(20f, 8f)
                        , Main.rand.NextVector2Circular(2.5f, 2.5f), new Color(255, 220, 140)
                        , Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(9, 15));
                }
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D tex = TextureValue;
            bool facingRight = DirSign > 0;
            Vector2 origin = facingRight ? GripPx : new Vector2(GripPx.X, tex.Height - GripPx.Y);
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation
                , origin, HeldScale * Projectile.scale
                , facingRight ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }

        /// <summary>近半环(z&lt;0)与终曲巨炮，压在实体层之上</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            PresenceStamp.Stamp();
            TiroFinaleRenderer.DrawHeldLayer(this, -1);
        }

        /// <summary>远半环(z&gt;=0)，由 <see cref="TiroFinaleFarRender"/> 在玩家层之前调</summary>
        internal void DrawFarLayer() => TiroFinaleRenderer.DrawHeldLayer(this, 1);
        #endregion

        internal static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        internal static float EaseOutBack(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
