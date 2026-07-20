using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishObsidian : FishSkill
    {
        public override int UnlockFishID => ItemID.Obsidifish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 6;
        public override int ResearchDuration => 60 * 18;

        private static readonly List<int> ActiveObsidianFish = new();
        private static int MaxObsidianFish => 5 + HalibutData.GetDomainLayer() / 2;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActiveObsidianFish.Count < MaxObsidianFish) {
                    int fishProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ObsidianFishOrbit>(),
                        (int)(damage * (1.6f + HalibutData.GetDomainLayer() * 0.55f)),
                        knockback * 0.3f,
                        player.whoAmI,
                        ai0: ActiveObsidianFish.Count
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActiveObsidianFish.Add(fishProj);
                        SpawnSummonEffect(player.Center);
                        SoundEngine.PlaySound(SoundID.Item30 with {
                            Volume = 0.5f,
                            Pitch = -0.3f + ActiveObsidianFish.Count * 0.05f
                        }, player.Center);
                    }
                }
            }

            return null;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (player.immuneTime > 0 && ActiveObsidianFish.Count > 0) {
                ShatterOneFish();
            }
            return true;
        }

        private static void ShatterOneFish() {
            CleanupInactiveFish();

            if (ActiveObsidianFish.Count > 0) {
                int fishID = ActiveObsidianFish[ActiveObsidianFish.Count - 1];

                if (fishID >= 0 && fishID < Main.maxProjectiles && Main.projectile[fishID].active) {
                    Projectile fish = Main.projectile[fishID];
                    if (fish.ModProjectile is ObsidianFishOrbit obsidianFish) {
                        obsidianFish.Shatter();
                    }
                }

                ActiveObsidianFish.RemoveAt(ActiveObsidianFish.Count - 1);
            }
        }

        private static void CleanupInactiveFish() {
            ActiveObsidianFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<ObsidianFishOrbit>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            //黑曜石"淬凝"：暗紫玻璃屑向内汇聚 + 暗烟
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 from = position + angle.ToRotationVector2() * Main.rand.NextFloat(40f, 70f);
                PRTLoader.NewParticle<PRT_Spark>(from, (position - from) * 0.08f
                    , Color.Lerp(ObsidianFishOrbit.SheenPurple, ObsidianFishOrbit.ObsidianDark, Main.rand.NextFloat()), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(false, Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(position + Main.rand.NextVector2Circular(18f, 18f)
                    , Main.rand.NextVector2Circular(2f, 2f), new Color(40, 30, 45), Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(24, 0.7f, 0.04f);
            }
        }
    }

    /// <summary>
    /// 黑曜石鱼：环绕玩家的火山玻璃护卫。倾斜椭圆轨道 + 弹簧滞后的次级运动让阵型"有重量"，
    /// 本体深黑近剪影，窄镜面高光随公转沿轮廓扫动（<see cref="FishObsidianAssets.FishObsidianGloss"/>）。
    /// 受击碎裂 = 英雄时刻：裂纹冻结数帧（慢放感）后爆成贝壳状断口的锐利玻璃片
    /// </summary>
    internal class ObsidianFishOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Obsidifish;

        private enum FishState
        {
            Gathering,
            Orbiting,
            Shattering,
            Dissolving
        }

        private FishState State {
            get => (FishState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }
        private ref float FishIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //轨道
        private float orbitRadius = 152f;
        private float spinAngle;
        private float myAngle;
        private float depth;          //-1 远 .. 1 近
        private float swimPhase;
        private Vector2 gatherStart;
        private Vector2 followVel;    //弹簧速度（次级运动）

        //姿态与玻璃光泽
        private float bodyRotation;
        private float glow;
        private float scaleMul = 1f;
        private float crackPulse;     //余温矿脉的怠速呼吸，幅度极低
        private float crackHeat;      //磕碰应力，受击瞬间抬升后衰减
        private float specBoost;      //镜面扫光脉冲包络
        private int glintTimer;       //距下一次扫光脉冲
        private float flash;          //爆裂过曝，只在爆点存活 1-2 帧

        private readonly List<ShockRing> rings = new();

        public static readonly Color ObsidianDark = new(34, 22, 44);
        public static readonly Color GlassDeep = new(18, 11, 26);
        public static readonly Color SheenPurple = new(150, 100, 210);
        public static readonly Color RingGlow = new(214, 184, 255);
        public static readonly Color EmberWarm = new(255, 110, 45);

        private const int GatherDuration = 20;
        /// <summary>碎裂前的裂纹冻结帧数，读作短暂慢放</summary>
        private const int CrackFreezeFrames = 8;
        /// <summary>爆裂后余迹容器帧数，环与余尘活得比本体久</summary>
        private const int AftermathFrames = 30;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        /// <summary>冻结期 0→1 拉满矿脉，爆裂后作为余温辉光在容器帧里衰减归零</summary>
        private float FreezeRamp {
            get {
                if (State != FishState.Shattering) {
                    return 0f;
                }
                if (StateTimer <= CrackFreezeFrames) {
                    return MathHelper.Clamp(StateTimer / (float)CrackFreezeFrames, 0f, 1f);
                }
                return MathHelper.Clamp(1f - (StateTimer - CrackFreezeFrames) / 14f, 0f, 1f);
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishObsidian>().Active(Owner)
                && State is FishState.Gathering or FishState.Orbiting) {
                StartDissolve();
            }

            StateTimer++;
            crackPulse = 0.08f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f + FishIndex * 1.3f) * 0.05f;
            crackHeat *= 0.95f;
            specBoost *= 0.87f;
            flash *= 0.5f;

            switch (State) {
                case FishState.Gathering:
                    GatheringAI(Owner);
                    break;
                case FishState.Orbiting:
                    OrbitingAI(Owner);
                    break;
                case FishState.Shattering:
                    ShatteringAI();
                    break;
                case FishState.Dissolving:
                    DissolvingAI();
                    break;
            }

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            //玻璃几乎不发光：微弱紫泽 + 应力时的余温橙
            float emberAmt = MathHelper.Clamp(crackPulse + crackHeat + FreezeRamp, 0f, 1f);
            float lit = glow * (0.6f + depth * 0.4f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.10f, 0.06f, 0.16f) * lit
                + new Vector3(1f, 0.42f, 0.15f) * emberAmt * 0.22f);
        }

        /// <summary>阵型成员数，碎裂/消散中的鱼不再占位</summary>
        private int GetTotalActiveFish() {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner
                    && proj.ai[1] < (float)FishState.Shattering) {
                    count++;
                }
            }
            return count;
        }

        private int GetMyRealIndex() {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner
                    && proj.ai[1] < (float)FishState.Shattering) {
                    if (proj.whoAmI == Projectile.whoAmI) {
                        return index;
                    }
                    index++;
                }
            }
            return 0;
        }

        private void GatheringAI(Player owner) {
            float p = MathHelper.Clamp(StateTimer / GatherDuration, 0f, 1f);

            if (StateTimer == 1) {
                gatherStart = Projectile.Center;
                swimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                spinAngle = MathHelper.TwoPi * FishIndex / Math.Max(GetTotalActiveFish(), 1);
                glintTimer = Main.rand.Next(40, 90);
            }

            myAngle = spinAngle;
            Vector2 target = OrbitPoint(owner, myAngle, out depth);
            //EaseOutBack 过冲：玻璃"咔"地嵌入阵位
            Projectile.Center = Vector2.Lerp(gatherStart, target, VaultUtils.EaseOutBack(p));

            Vector2 toCenter = owner.Center - Projectile.Center;
            bodyRotation = toCenter.ToRotation();
            glow = MathHelper.Lerp(0.2f, 1f, p);
            scaleMul = MathHelper.Lerp(0.4f, DepthScale(depth), p);

            //暗玻璃屑向鱼身淬凝
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(26f, 26f);
                PRTLoader.NewParticle<PRT_FishObsidianShard>(from, (Projectile.Center - from) * 0.16f
                    , Color.Lerp(GlassDeep, SheenPurple, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(12, 0f, 1f);
            }

            if (StateTimer >= GatherDuration) {
                State = FishState.Orbiting;
                StateTimer = 0;
                //嵌位瞬间的镜面过冲扫闪 + 细环
                specBoost = 1f;
                if (!Main.dedServ) {
                    rings.Add(new ShockRing(Projectile.Center, 42f, 5f, RingGlow, 1f, 14, 28));
                }
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void OrbitingAI(Player owner) {
            float orbitSpeed = 0.02f * (1f + HalibutData.GetDomainLayer() * 0.12f);
            spinAngle += orbitSpeed;

            int total = Math.Max(GetTotalActiveFish(), 1);
            int realIndex = GetMyRealIndex();
            //平滑收敛到均布理想角，避免数量变化时的瞬跳
            float idealAngle = MathHelper.TwoPi * realIndex / total + spinAngle;
            myAngle = MathHelper.WrapAngle(myAngle);
            myAngle += MathHelper.WrapAngle(idealAngle - myAngle) * 0.12f;

            swimPhase += 0.12f;
            Vector2 target = OrbitPoint(owner, myAngle, out depth);
            //游动摆动
            Vector2 sway = new Vector2((float)Math.Sin(swimPhase), (float)Math.Cos(swimPhase * 1.3f)) * 6f;
            target += sway;

            //弹簧滞后跟随：本体带惯性追轨道点，停不"死板"
            Vector2 toTarget = target - Projectile.Center;
            followVel += toTarget * 0.18f;
            followVel *= 0.74f;
            Projectile.Center += followVel;

            //朝向沿运动方向 + 轻微前后摆
            if (followVel.LengthSquared() > 0.4f) {
                float swayAngle = (float)Math.Sin(swimPhase * 2f) * 0.14f;
                bodyRotation = MathHelper.WrapAngle(MathHelper.Lerp(bodyRotation, followVel.ToRotation() + swayAngle, 0.25f));
            }

            scaleMul = MathHelper.Lerp(scaleMul, DepthScale(depth), 0.15f);
            glow = (0.7f + (float)Math.Sin(StateTimer * 0.12f + FishIndex) * 0.2f) * (0.7f + depth * 0.3f);

            if (Main.dedServ) {
                return;
            }

            //镜面扫光节拍：脉冲抬高 uSpec，并在迎光轮廓处迸一粒紫白星芒
            if (--glintTimer <= 0) {
                glintTimer = Main.rand.Next(70, 150);
                specBoost = 1f;
                Vector2 rim = Projectile.Center + LightWorldAngle().ToRotationVector2() * (11f * scaleMul);
                PRTLoader.NewParticle<PRT_Sparkle>(rim, followVel * 0.4f, new Color(226, 208, 255), 0.3f)
                    ?.Configure(SheenPurple, 16, 0.04f, 0.5f);
            }

            //火山余温：极细热雾上浮，近景才冒
            if (depth > 0.1f && Main.rand.NextBool(46)) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 8f)
                    , new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.7f, -0.3f))
                    , new Color(56, 36, 40), Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(26, 0.3f, 0.02f);
            }
            //偶发的余烬微滴
            if (depth > 0.2f && Main.rand.NextBool(70)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.5f, 1.1f))
                    , EmberWarm * 0.8f, Main.rand.NextFloat(0.25f, 0.4f))
                    ?.Configure(true, 14);
            }
        }

        private void ShatteringAI() {
            if (StateTimer <= CrackFreezeFrames) {
                //裂纹冻结：定住轨道位置细碎抖动，矿脉在 shader 里烧起来，读作时间凝滞
                Projectile.Center += Main.rand.NextVector2Circular(1.4f, 1.4f);
                if (StateTimer == CrackFreezeFrames - 1) {
                    flash = 1f;
                }
                if (StateTimer == CrackFreezeFrames) {
                    Burst();
                }
                return;
            }
            //余迹容器：本体已爆掉不再绘制，环与余尘在这里活完
            glow *= 0.9f;
            if (StateTimer >= CrackFreezeFrames + AftermathFrames) {
                Projectile.Kill();
            }
        }

        private void DissolvingAI() {
            scaleMul *= 0.94f;
            glow *= 0.88f;
            Projectile.alpha += 20;

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_FishObsidianShard>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f) + Vector2.UnitY * 0.6f
                    , GlassDeep, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(16, 24), 0.18f);
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        /// <summary>倾斜椭圆轨道点（俯视压扁形成伪 3D 层次），并输出深度</summary>
        private Vector2 OrbitPoint(Player owner, float angle, out float depthOut) {
            float radiusPulse = orbitRadius * (1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.6f + FishIndex) * 0.04f);
            float x = (float)Math.Cos(angle) * radiusPulse;
            float y = (float)Math.Sin(angle) * radiusPulse * 0.52f;//压扁成倾斜环
            depthOut = (float)Math.Sin(angle);//下半弧在前
            return owner.Center + new Vector2(x, y - 8f);
        }

        private static float DepthScale(float depth) => 0.78f + (depth * 0.5f + 0.5f) * 0.5f;

        /// <summary>世界空间光向角：缓慢漂移的高位光源，配合公转让高光沿轮廓持续移动</summary>
        private float LightWorldAngle()
            => -2.3f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.6f + FishIndex) * 0.3f;

        public void Shatter() {
            if (State is FishState.Shattering or FishState.Dissolving) {
                return;
            }
            State = FishState.Shattering;
            StateTimer = 0;
            Projectile.friendly = false;
            crackHeat = 1f;

            //应力预告：高音玻璃紧绷声先行，爆点声在 Burst 落地
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.55f }, Projectile.Center);
        }

        private void StartDissolve() {
            State = FishState.Dissolving;
            StateTimer = 0;
            Projectile.friendly = false;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.2f, Pitch = 0.3f }, Projectile.Center);
        }

        /// <summary>爆点：镜头冲击 + 双层声 + 贝壳状断口玻璃片四射，余迹交给容器帧</summary>
        private void Burst() {
            Punch(Owner, 5f);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            rings.Add(new ShockRing(Projectile.Center, 150f, 12f, RingGlow, 1f, 20, 40));
            rings.Add(new ShockRing(Projectile.Center, 80f, 8f, EmberWarm * 0.5f, 1f, 26, 32));

            //贝壳状断口主碎片：向轨道外侧偏置迸射，慢放帧急停后再受重力
            Vector2 outDir = (Projectile.Center - Owner.Center).SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < 16; i++) {
                Vector2 vel = outDir.RotatedByRandom(1.1f) * Main.rand.NextFloat(7f, 14f)
                    + Main.rand.NextVector2Circular(3f, 3f);
                PRTLoader.NewParticle<PRT_FishObsidianShard>(Projectile.Center, vel
                    , Color.Lerp(new Color(26, 16, 36), new Color(58, 36, 74), Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(34, 52), 0.26f, 0.988f, 5);
            }
            //细屑
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_FishObsidianShard>(Projectile.Center, Main.rand.NextVector2Circular(9f, 9f)
                    , GlassDeep, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(20, 30), 0.26f, 0.988f, 3);
            }
            //棱面爆闪星芒
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , Main.rand.NextVector2Circular(3f, 3f), new Color(226, 208, 255), Main.rand.NextFloat(0.3f, 0.45f))
                    ?.Configure(SheenPurple, 18, 0.06f, 0.6f);
            }
            //火山余温热雾
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(1.4f, 1.4f) - Vector2.UnitY * 0.6f
                    , new Color(52, 34, 40), Main.rand.NextFloat(0.8f, 1.2f))?.Configure(30, 0.5f, 0.04f);
            }
            //悬浮微尘：碎裂后慢慢飘落的玻璃闪点，比本体活得久
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(26f, 20f)
                    , new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.2f, 0.6f))
                    , SheenPurple, Main.rand.NextFloat(0.18f, 0.28f))
                    ?.Configure(SheenPurple * 0.7f, Main.rand.Next(34, 48), 0.03f, 0.4f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //磕碰应力：矿脉短暂发热
            crackHeat = MathHelper.Min(1f, crackHeat + 0.35f);
            if (Main.dedServ) {
                return;
            }
            Vector2 contact = Vector2.Lerp(Projectile.Center, target.Center, 0.5f);
            //刃缘刮擦崩出的细玻璃屑
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishObsidianShard>(contact, Main.rand.NextVector2Circular(4f, 4f)
                    , GlassDeep, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 22), 0.22f);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(contact, Main.rand.NextVector2Circular(4f, 4f)
                    , RingGlow, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(true, 12);
            }
            rings.Add(new ShockRing(contact, 44f, 6f, RingGlow, 1f, 13, 28));
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //非碎裂路径的死亡（超时/玩家离场）只留一小撮消散屑，英雄演出专属于 Shatter
            if (State != FishState.Shattering && !Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_FishObsidianShard>(Projectile.Center, Main.rand.NextVector2Circular(3f, 3f)
                        , GlassDeep, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(16, 26), 0.2f);
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(1f, 1f)
                        , new Color(44, 30, 38), Main.rand.NextFloat(0.6f, 0.9f))?.Configure(22, 0.4f, 0.03f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ItemID.Obsidifish].Value;
            Rectangle src = tex.Frame();
            Vector2 origin = src.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = (255f - Projectile.alpha) / 255f;
            float rot = bodyRotation + MathHelper.PiOver4;
            float scale = Projectile.scale * scaleMul;
            bool bodyVisible = State != FishState.Shattering || StateTimer <= CrackFreezeFrames;

            if (bodyVisible) {
                //暗色玻璃拖影：残影链只在真的在动时出现，快则浓
                if (State != FishState.Gathering) {
                    float smear = MathHelper.Clamp(followVel.Length() / 6f, 0f, 1f);
                    for (int i = 1; i < 6 && i < Projectile.oldPos.Length; i++) {
                        if (Projectile.oldPos[i] == Vector2.Zero) {
                            continue;
                        }
                        float t = 1f - i / 6f;
                        Vector2 gp = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                        Main.spriteBatch.Draw(tex, gp, src, GlassDeep * (t * 0.30f * fade * smear), rot, origin
                            , scale * MathHelper.Lerp(0.72f, 0.95f, t), SpriteEffects.None, 0f);
                    }
                }

                Effect fx = FishObsidianAssets.FishObsidianGloss;
                Texture2D noise = CWRAsset.PerlinNoise?.Value;
                if (fx != null && noise != null) {
                    //贴图空间光向 = 世界光向抵消精灵旋转，高光随公转/自转沿轮廓扫
                    float texAng = LightWorldAngle() - rot;
                    float baseSpec = State == FishState.Gathering
                        ? MathHelper.Lerp(0.5f, 1f, MathHelper.Clamp(StateTimer / GatherDuration, 0f, 1f)) : 1f;

                    fx.Parameters["uLightDir"]?.SetValue(texAng.ToRotationVector2());
                    fx.Parameters["uTexel"]?.SetValue(Vector2.One / src.Size());
                    fx.Parameters["uSpec"]?.SetValue(baseSpec + specBoost * 2.2f + FreezeRamp * 0.9f);
                    fx.Parameters["uSheenPhase"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.5f + FishIndex * 2.1f + myAngle * 2f);
                    fx.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(crackPulse + crackHeat * 0.6f + FreezeRamp, 0f, 1f));
                    fx.Parameters["uFlash"]?.SetValue(flash);
                    fx.Parameters["uFade"]?.SetValue(fade);
                    fx.Parameters["uSeed"]?.SetValue(FishIndex * 0.173f);
                    fx.Parameters["uDepthDim"]?.SetValue(depth * 0.5f + 0.5f);
                    fx.Parameters["uLightColor"]?.SetValue(lightColor.ToVector3());
                    fx.Parameters["uNoiseTex"]?.SetValue(noise);

                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                        , DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

                    Main.spriteBatch.Draw(tex, drawPos, src, Color.White, rot, origin, scale, SpriteEffects.None, 0f);

                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                        , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
                else {
                    //CPU 回退：压暗剪影 + 单层紫泽 + 应力余温，缺 .fxc 时不至于黑块或裸贴图
                    float emberAmt = MathHelper.Clamp(crackPulse + crackHeat * 0.6f + FreezeRamp, 0f, 1f);
                    Color body = Color.Lerp(lightColor, GlassDeep, 0.72f);
                    body = Color.Lerp(body, Color.Black, (1f - (depth * 0.5f + 0.5f)) * 0.35f);
                    Main.spriteBatch.Draw(tex, drawPos, src, body * fade, rot, origin, scale, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(tex, drawPos, src, (SheenPurple with { A = 0 }) * (glow * 0.14f * fade)
                        , rot, origin, scale * 1.03f, SpriteEffects.None, 0f);
                    if (emberAmt > 0.15f) {
                        Main.spriteBatch.Draw(tex, drawPos, src, (EmberWarm with { A = 0 }) * (emberAmt * 0.3f * fade)
                            , rot, origin, scale * 0.96f, SpriteEffects.None, 0f);
                    }
                    if (flash > 0.05f) {
                        Main.spriteBatch.Draw(tex, drawPos, src, (RingGlow with { A = 0 }) * (flash * fade)
                            , rot, origin, scale, SpriteEffects.None, 0f);
                    }
                }
            }

            //冲击波环（顶点绘制）
            if (rings.Count > 0) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Texture2D ringTex = VaultAsset.placeholder2.Value;
                foreach (ShockRing r in rings) {
                    r.Draw(ringTex);
                }
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        /// <summary>
        /// 取最大值的镜头冲击；仅本地玩家、且服务器配置开启屏幕震动时生效，避免多端各自抖动与配置越权
        /// </summary>
        public static void Punch(Player owner, float amount) {
            if (owner == null || owner.whoAmI != Main.myPlayer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            owner.CWR().ScreenShakeValue = MathHelper.Max(owner.CWR().ScreenShakeValue, amount);
        }

        /// <summary>
        /// 加色三角带圆环（真正的顶点绘制）。须在外部已 Begin 的 Immediate/Additive 批次中调用，
        /// 由该批次为设备绑定精灵着色器；颜色由内/外环顶点插值，<paramref name="squash"/> 做地面透视压扁
        /// </summary>
        public static void DrawShockRing(Texture2D tex, Vector2 screenCenter, float radius, float thickness
            , Color innerColor, Color outerColor, int segments = 72, float squash = 1f, float rot = 0f
            , float jitter = 0f, float jitterPhase = 0f, float jitterFreq = 6f) {
            if (radius <= 1f || thickness <= 0.1f || segments < 3) {
                return;
            }

            int vertCount = (segments + 1) * 2;
            ColoredVertex[] verts = new ColoredVertex[vertCount];
            float half = thickness * 0.5f;

            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                float ang = t * MathHelper.TwoPi + rot;
                Vector2 dir = ang.ToRotationVector2();
                dir.Y *= squash;

                float r = radius;
                if (jitter > 0f) {
                    r += (float)Math.Sin(ang * jitterFreq + jitterPhase) * jitter;
                }

                verts[i * 2] = new ColoredVertex(screenCenter + dir * (r - half), innerColor, new Vector3(t, 0f, 1f));
                verts[i * 2 + 1] = new ColoredVertex(screenCenter + dir * (r + half), outerColor, new Vector3(t, 1f, 1f));
            }

            Main.graphics.GraphicsDevice.Textures[0] = tex;
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
        }

        /// <summary>
        /// 可复用的顶点冲击波环：随生命扩张、变薄、淡出，可压扁成贴地椭圆。
        /// 由弹幕维护实例列表，AI 内 <see cref="Update"/>，绘制时在 Immediate/Additive 批次内 <see cref="Draw"/>。
        /// </summary>
        public sealed class ShockRing
        {
            private readonly Vector2 center;
            private readonly float maxRadius;
            private readonly float baseThickness;
            private readonly Color color;
            private readonly float squash;
            private readonly int segments;
            private readonly float phase;
            private readonly float edgeFade;
            private int life;
            private readonly int maxLife;

            public bool Dead => life >= maxLife;

            public ShockRing(Vector2 center, float maxRadius, float thickness, Color color
                , float squash = 1f, int maxLife = 26, int segments = 72, float edgeFade = 0.15f) {
                this.center = center;
                this.maxRadius = maxRadius;
                baseThickness = thickness;
                this.color = color;
                this.squash = squash;
                this.maxLife = maxLife;
                this.segments = segments;
                this.edgeFade = edgeFade;
                phase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public void Update() => life++;

            public void Draw(Texture2D tex) {
                float p = life / (float)maxLife;
                float radius = VaultUtils.EaseOutCubic(p) * maxRadius;
                float alpha = (float)Math.Sin((1f - p) * MathHelper.PiOver2);
                float thickness = baseThickness * (1.4f - p);
                Color inner = color * alpha;
                inner.A = 0;
                Color outer = color * (alpha * edgeFade);
                outer.A = 0;
                DrawShockRing(tex, center - Main.screenPosition, radius, thickness, inner, outer
                    , segments, squash, 0f, radius * 0.04f, phase + life * 0.2f);
            }
        }
    }
}
