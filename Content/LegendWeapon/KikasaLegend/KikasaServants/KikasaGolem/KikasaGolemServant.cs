using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaGolem
{
    /// <summary>
    /// 鬼奴·石首浮屠。只有一颗石巨人头，没有身体没有拳头
    /// 材质反向读数：其他鬼奴是血水凝成的形，它是石头本身（uForm 压到最低），
    /// 裂缝渗血、眼窝里蓄着晃动的血水。重量感贯穿一切：加速慢、停不住、转向像船，
    /// 低空贴水面滑浮并犁出持续水痕。签名攻击为升天砸水（窄高双水柱+全场最大单击震屏）、
    /// 岩浆血火球水面打水漂一跳、眼窝血珠喷吐；主动放弃原版眼部激光。
    /// 遣返不化水：裂缝蔓延→整颗头碎成石块坠湖→血水从水面收干。
    /// 联机同克眼契约：owner 裁决转场盖 netUpdate 章，节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaGolemServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>砸水接触基伤（召唤加成前）</summary>
        internal const int SlamDamage = 700;

        /// <summary>岩浆血火球基伤（召唤加成前），由火球弹幕消费</summary>
        internal const int MagmaDamage = 380;

        /// <summary>眼窝血珠基伤（召唤加成前），由血珠弹幕消费</summary>
        internal const int SpitDamage = 240;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateSlam = 2;
        private const int StateMagma = 3;
        private const int StateEyeSpit = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：砸水=相位号（0升1悬2坠3没4浮），其余状态未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：石屑蒸汽预兆→破水抬升→倾泻淌干→血光觉醒
        private const int OmenFrames = 36;
        private const int RiseEnd = 92;
        private const int AwakenFrame = 96;
        private const int EmergeTotal = 118;

        //砸水：升天(限时)→悬停一拍→自由落体→没水→缓浮复位
        private const int SlamRiseTimeout = 100;
        private const int SlamHoverFrames = 30;
        private const int SlamDropTimeout = 90;
        private const int SlamSinkFrames = 26;
        private const int SlamResurfaceTimeout = 80;

        //火球：刹停→张口蓄热(72%后静默)→喷吐→合口回摆
        private const int MagmaBrakeEnd = 8;
        private const int MagmaFireFrame = 34;
        private const int MagmaRecoverEnd = 56;

        //眼吐：血光扫烁 tell→三对小弹→收势
        private const int SpitTellEnd = 18;
        private const int SpitPairGap = 6;
        private const int SpitPairCount = 3;
        private const int SpitRecoverEnd = 48;

        //碎裂溶解：裂缝蔓延→整头碎块坠湖→血水收干
        private const int CrackFrames = 44;
        private const int DrainFrame = CrackFrames + 50;
        private const int DissolveTotal = CrackFrames + 66;

        //==================== 体格 ====================

        /// <summary>原版飞头以 0.5 缩放绘制，这里略放大压场</summary>
        private const float DrawScale = 0.62f;

        /// <summary>滑浮时头心距湖面的高度</summary>
        private const float HoverAbove = 36f;

        //==================== 石块碎屑（纯本地表现，各端独立模拟）====================

        private class StoneShard
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Rot;
            public float RotVel;
            public Rectangle Src;
            public int Life;
            public int MaxLife;
            public bool InWater;
        }

        private readonly List<StoneShard> shards = new();
        private const int ShardCap = 44;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private bool mouthOpen;
        private int eyeAnimTick;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private int lastSeenSlamPhase = -1;
        private bool breachDone;
        private bool awakenDone;
        private bool dropWhooshed;
        private bool impactDone;
        private bool resurfaceSplashed;
        private bool magmaFired;
        private int lastSpitFired = -1;
        private bool crumbled;
        private bool drainDone;
        /// <summary>湿度：过水线拉满、出水后淌干，驱动裂缝倾泻与材质血水度</summary>
        private float wetness;
        /// <summary>眼窝积血的晃动滞后（重物急动时血水甩向反侧）</summary>
        private Vector2 eyeSlosh;
        /// <summary>砸水落点（各端同规则自算，owner 盖章纠偏）</summary>
        private float slamX;
        /// <summary>双水柱演出锚：X 与起爆帧计数（本地表现）</summary>
        private float columnX;
        private int columnAge = 999;

        //血系配色随观看域鬼雨异化冷化；岩浆橙只做次要点缀层
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color EyeEmber => KikasaDomain.CoolTint(new(255, 128, 58), new(168, 186, 188));
        private static Color SteamPale => KikasaDomain.CoolTint(new(216, 196, 190), new(188, 198, 202));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面，头从湖下浮起</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlamDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 64f), Vector2.Zero,
                ModContent.ProjectileType<KikasaGolemServant>(), damage, 9f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //升天砸水会飞出屏外，碎块也会散开
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 84;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>砸击窗：自由落体全程 + 没水头几帧的水下贯穿，与可见的坠砸严格对齐</summary>
        private bool SlamActive
            => State == StateSlam
            && ((int)StateParam == 2 || (int)StateParam == 3 && StateTimer <= 10);

        public override bool? CanDamage() => SlamActive ? null : false;

        /// <summary>砸落速度极快，补一段扫掠线碰撞防穿帧漏判</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (SlamActive) {
                float _ = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, Projectile.Center + Projectile.velocity, 46f, ref _)) {
                    return true;
                }
            }
            return null;
        }

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //还没破水就要收场：什么都没露出来，不演碎裂
            if (State == StateEmerge && StateTimer < OmenFrames) {
                Projectile.Kill();
                return;
            }
            State = StateDissolve;
            StateTimer = 0;
            StateParam = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        //==================== 推进 ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();

            //生命线：只有 owner 裁决，服务器无领域状态（恒 Closed 是既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlamDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场
            if (State != lastSeenState) {
                lastSeenState = State;
                lastSeenSlamPhase = -1;
                dropWhooshed = false;
                impactDone = false;
                resurfaceSplashed = false;
                magmaFired = false;
                lastSpitFired = -1;
                if (State == StateDissolve) {
                    crumbled = false;
                    drainDone = false;
                }
            }
            //砸水相位闩：同一状态内分相推进，远端也可能靠收包跳相
            if (State == StateSlam && (int)StateParam != lastSeenSlamPhase) {
                lastSeenSlamPhase = (int)StateParam;
                if (lastSeenSlamPhase == 3 && !impactDone) {
                    //远端先收到没水包再补起爆：在同步过来的位置补拍
                    ImpactBeat(domain);
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateSlam: UpdateSlam(owner, domain, authority); break;
                case StateMagma: UpdateMagma(owner, domain, authority); break;
                case StateEyeSpit: UpdateEyeSpit(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateWetness(domain);
            UpdateShards(domain);
            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (columnAge < 999) {
                columnAge++;
            }

            //眼窝积血的晃动滞后：反向甩、缓回中
            eyeSlosh = Vector2.Lerp(eyeSlosh, -Projectile.velocity * 0.6f, 0.1f);
            if (eyeSlosh.Length() > 6f) {
                eyeSlosh = eyeSlosh.SafeNormalize(Vector2.Zero) * 6f;
            }

            float glow = CurrentAlpha() * EyeAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.44f * glow, 0.18f * glow, 0.07f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：石首浮屠破水抬升 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //预兆：湖面浮起一圈石屑与蒸汽，水下暖光渐醒
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 4 == 1) {
                        //石屑自两侧收拢着往上蹦，落回去各自一声轻响
                        float converge = 1f - t / (float)OmenFrames;
                        float side = t / 4 % 2 == 0 ? 1f : -1f;
                        SpawnChip(new Vector2(Projectile.Center.X + side * (20f + converge * 46f), lakeY - 2f),
                            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.6f, 3.4f)));
                    }
                    if (t % 6 == 2) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(
                            new Vector2(Projectile.Center.X + Main.rand.NextFloat(-40f, 40f), lakeY - Main.rand.NextFloat(2f, 10f)),
                            new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.5f, 1.1f)),
                            SteamPale * 0.45f, Main.rand.NextFloat(0.5f, 0.8f))
                            ?.Configure(Main.rand.Next(40, 70));
                    }
                    if (t % 6 == 4) {
                        float converge = 1f - t / (float)OmenFrames;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(Projectile.Center.X + (t / 6 % 2 == 0 ? 1f : -1f) * converge * 48f, lakeY),
                            0.35f + (1f - converge) * 0.4f);
                    }
                    if (t == 6 || t == 24) {
                        //湖底的闷滚声一声比一声近
                        SoundEngine.PlaySound(SoundID.WormDig with {
                            Volume = t == 6 ? 0.5f : 0.7f,
                            Pitch = t == 6 ? -0.6f : -0.4f,
                            MaxInstances = 2
                        }, new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速，不是跃出，是石头被湖捧出来
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -7.8f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.9f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //抬升：起速后指数衰减，尾段弹簧扶到滑浮高度，禁匀速
            Projectile.velocity.Y *= 0.945f;
            Projectile.velocity.X = 0f;
            if (t > 64) {
                float hoverY = lakeY - HoverAbove;
                Projectile.velocity.Y += MathHelper.Clamp((hoverY - Projectile.Center.Y) * 0.012f, -0.5f, 0.5f);
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
            wetness = 1f;

            //眼窝与裂缝倾泻由 UpdateWetness 常驻承担；这里补湖面落水的连环圈
            if (viewed && t < RiseEnd && t % 6 == 3) {
                KikasaDomainDeco.RippleAt(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-24f, 24f), lakeY), 0.35f);
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：眼窝血光涌起
                awakenDone = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.7f);
                    ShakeViewer(2.2f);
                }
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠：重物顶破水面，宽环涟漪 + 低抛血珠 + 石屑迸散 + 蒸汽</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.8f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 12);

            //浪冠血珠：低平的重扇，石头顶出来的水没有轻盈可言
            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.16f + 0.68f * i / 23f);
                float speed = Main.rand.NextFloat(2.8f, 6.6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-32f, 32f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38));
            }
            //石屑随浪迸散
            for (int i = 0; i < 8; i++) {
                SpawnChip(hit + new Vector2(Main.rand.NextFloat(-26f, 26f), -6f),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(3f, 6.5f)));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-36f, 36f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.9f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.75f, 1.1f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.36f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        //==================== 跟随：贴水滑浮 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            //重物几乎不呼吸：极小慢正弦
            float hoverY = lakeY - HoverAbove + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + Seed) * 2.5f;
            float anchorX = owner.Center.X - owner.direction * 150f;

            Vector2 anchor = new(anchorX, hoverY);
            if (Vector2.Distance(Projectile.Center, anchor) > 2400f) {
                //跟丢硬贴回，别在半个地图外犁水
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }

            //横向像船：想去的速度慢慢兑现，停不住是特性不是缺陷
            float dx = anchorX - Projectile.Center.X;
            float wantVx = MathHelper.Clamp(dx * 0.028f, -8.5f, 8.5f);
            if (MathF.Abs(dx) > 900f) {
                wantVx *= 1.6f;
            }
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, wantVx, 0.05f);
            //纵向极稳：重物的笃定
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp((hoverY - Projectile.Center.Y) * 0.09f, -4.5f, 4.5f), 0.3f);

            //行进侧倾，转向像船
            float tilt = MathHelper.Clamp(Projectile.velocity.X * 0.014f, -0.13f, 0.13f);
            Projectile.rotation = Projectile.rotation.AngleLerp(tilt, 0.08f);

            //底面犁水：持续的行进水痕
            PlowWake(lakeY);

            //出手裁决：砸水低频压轴，火球为主、眼吐点缀；规则各端一致，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                int sel = attackIndex % 4;
                State = sel == 0 ? StateSlam : sel == 2 ? StateEyeSpit : StateMagma;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        /// <summary>底面拖出犁水痕迹：涟漪压在身后 + 反向踢起的碎水</summary>
        private void PlowWake(float lakeY) {
            if (!ViewedOwner || MathF.Abs(Projectile.velocity.X) < 1.4f
                || Projectile.Center.Y + 46f < lakeY - 8f) {
                return;
            }
            int t = (int)StateTimer;
            float speedK = MathF.Abs(Projectile.velocity.X);
            if (t % 4 == 0) {
                //小圈常态犁痕（scale<0.3 不占行波槽），偶发一道大些的浪
                float scale = t % 24 == 0 ? 0.36f : MathHelper.Clamp(0.18f + speedK * 0.014f, 0.18f, 0.29f);
                KikasaDomainDeco.RippleAt(
                    new Vector2(Projectile.Center.X - MathF.Sign(Projectile.velocity.X) * 30f, lakeY), scale);
            }
            if (t % 3 == 1) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    new Vector2(Projectile.Center.X - MathF.Sign(Projectile.velocity.X) * 38f, lakeY - 2f),
                    new Vector2(-MathF.Sign(Projectile.velocity.X) * Main.rand.NextFloat(0.6f, 1.6f),
                        -Main.rand.NextFloat(1.2f, 2.6f) * MathHelper.Clamp(speedK * 0.16f, 0.4f, 1.2f)),
                    SteamPale * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }
        }

        //==================== 升天砸水 ====================

        private void UpdateSlam(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            float lakeY = domain.LakeWorldY;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //升天：缓慢升到目标上空高处，重物的爬升没有干脆可言
                if (target < 0 && t <= 10) {
                    EndAttack(authority, 45);
                    return;
                }
                Vector2 tgt = target >= 0 ? Main.npc[target].Center : owner.Center;
                float apexY = MathF.Min(tgt.Y - 420f, lakeY - 520f);
                float apexX = tgt.X + (target >= 0 ? Main.npc[target].velocity.X * 18f : 0f);
                slamX = apexX;

                Vector2 desired = new Vector2(apexX, apexY) - Projectile.Center;
                Vector2 want = desired * 0.045f;
                if (want.Length() > 12.5f) {
                    want = want.SafeNormalize(Vector2.Zero) * 12.5f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.055f);
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.1f);

                if (desired.Length() < 70f || t >= SlamRiseTimeout) {
                    //锁落点：取新鲜目标带提前量
                    slamX = target >= 0
                        ? Main.npc[target].Center.X + Main.npc[target].velocity.X * 26f
                        : Projectile.Center.X;
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //悬停一拍：眼窝血光亮起，72% 后静默，末几帧微微上提，落锤前的吸气
                //迟入场的远端没跑过升天段，落点兜底取当前头位，等 owner 的坠落包纠偏
                if (slamX == 0f) {
                    slamX = Projectile.Center.X;
                }
                Projectile.velocity *= 0.86f;
                if (t >= SlamHoverFrames - 6) {
                    Projectile.velocity.Y -= 0.4f;
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄势收拢的血火星，静默截断
                if (!Main.dedServ && t < SlamHoverFrames * 0.72f && t % 3 == 1) {
                    Vector2 eye = EyePos(Main.rand.NextBool() ? -1 : 1);
                    Vector2 from = eye + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (eye - from) * 0.13f,
                        EyeEmber, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 12);
                }
                if (t >= SlamHoverFrames) {
                    //坠落一帧定速：X 由落点反解，落体本身不再转向
                    float h = MathF.Max(lakeY - Projectile.Center.Y, 60f);
                    float fall = MathF.Max(MathF.Sqrt(2f * h / 1.4f), 10f);
                    Projectile.velocity = new Vector2(
                        MathHelper.Clamp((slamX - Projectile.Center.X) / fall, -11f, 11f), 5f);
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //自由落体：重力逐帧压满，直坠，石头不拐弯
                if (!dropWhooshed) {
                    dropWhooshed = true;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = -0.65f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(1.5f);
                    }
                }
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 1.4f, 40f);
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.3f);
                //坠速剪切风：贴身甩出的速度拉伸水丝
                if (!Main.dedServ && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-34f, 34f), -30f),
                        Projectile.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0f),
                        SteamPale * 0.4f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(8, 14), 0f);
                }

                if (Projectile.Center.Y >= lakeY - 6f || t > SlamDropTimeout) {
                    ImpactBeat(domain);
                    Projectile.velocity.X *= 0.3f;
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y * 0.25f, 8f);
                    NextPhase(3);
                }
                return;
            }

            if (phase == 3) {
                //没水：水下急刹，湖面还在翻涌
                Projectile.velocity *= 0.85f;
                wetness = 1f;
                if (ViewedOwner && t % 5 == 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.5f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-16f, 16f), lakeY - 2f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.2f, 2.2f)),
                        SteamPale * 0.4f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(10, 18), 0f);
                }
                if (t >= SlamSinkFrames) {
                    Projectile.velocity.Y = -3.6f;
                    NextPhase(4);
                }
                return;
            }

            //缓浮复位：石头自己浮回来，水从裂缝里继续淌
            float hoverY2 = lakeY - HoverAbove;
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp((hoverY2 - Projectile.Center.Y) * 0.06f, -3.4f, 0f), 0.12f);
            Projectile.velocity.X *= 0.94f;
            if (!resurfaceSplashed && Projectile.Center.Y < lakeY) {
                resurfaceSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 6);
                    KikasaDomainDeco.RippleAt(hit, 1.0f);
                }
            }
            if (MathF.Abs(Projectile.Center.Y - hoverY2) < 24f || t > SlamResurfaceTimeout) {
                EndAttack(authority, 170);
            }
        }

        /// <summary>砸水起爆：神庙鼓闷响 + 全场最大单击震屏 + 两根又高又窄的水柱</summary>
        private void ImpactBeat(KikasaDomainPlayer domain) {
            if (impactDone) {
                return;
            }
            impactDone = true;
            float lakeY = domain.LakeWorldY;
            Vector2 hit = new(Projectile.Center.X, lakeY);
            columnX = hit.X;
            columnAge = 0;

            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Volume = 1f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 2 }, hit);

            //震屏只给看得见这片湖的人；音效靠位置衰减自理
            if (!ViewedOwner) {
                return;
            }
            ShakeViewer(9.5f);
            KikasaDomainDeco.RippleAt(hit, 2.9f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(64f, 0f), 1.2f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(64f, 0f), 1.2f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-20f, 0f), 10);
            KikasaDomainDeco.SplashAt(hit + new Vector2(20f, 0f), 10);

            //双水柱：砸点两侧轰起，又高又窄，与史莱姆王的宽矮横推浪划清界限
            for (int side = -1; side <= 1; side += 2) {
                float baseX = hit.X + side * 46f;
                for (int i = 0; i < 20; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        new Vector2(baseX + Main.rand.NextFloat(-5f, 5f), lakeY - 4f),
                        new Vector2(Main.rand.NextFloat(-0.55f, 0.55f), -Main.rand.NextFloat(12.5f, 18.5f)),
                        SteamPale * Main.rand.NextFloat(0.45f, 0.65f),
                        Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(40, 62), 0f);
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        new Vector2(baseX + Main.rand.NextFloat(-4f, 4f), lakeY - 4f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(9f, 13.5f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(36, 52));
                }
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(baseX, lakeY - 8f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.1f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.rand.Next(50, 80));
                PRTLoader.NewParticle<PRT_DWave>(new Vector2(baseX, lakeY - 10f), Vector2.Zero,
                    BloodDeep, 0.08f)
                    ?.Configure(new Vector2(0.4f, 1f), -MathHelper.PiOver2, 0.3f, 10);
            }
        }

        //==================== 岩浆血火球 ====================

        private void UpdateMagma(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= 30 && !magmaFired) {
                EndAttack(authority, 45);
                return;
            }

            if (t <= MagmaBrakeEnd) {
                Projectile.velocity *= 0.88f;
                return;
            }

            if (t < MagmaFireFrame) {
                //张口蓄热：火星向口部收拢，72% 后静默，喷发前的吸气
                float charge = (t - MagmaBrakeEnd) / (float)(MagmaFireFrame - MagmaBrakeEnd);
                Projectile.velocity *= 0.92f;
                if (t == MagmaBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(46f, 100f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (mouth - from) * 0.12f,
                        Color.Lerp(EyeEmber, BloodMain, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, 13);
                }
                return;
            }

            if (!magmaFired) {
                //喷吐一帧：后坐退步 + 微微仰头，知重量者的答话
                magmaFired = true;
                int freshTarget = FindTarget(owner);
                Vector2 mouth = MouthPos();
                Vector2 tgt = freshTarget >= 0 ? Main.npc[freshTarget].Center
                    : mouth + Vector2.UnitX * (Projectile.velocity.X >= 0f ? 300f : -300f);
                float dx = tgt.X - mouth.X;
                //低平抛射：让重力把它按到水面上，跳那一下是弹道自己找的
                Vector2 vel = new(MathF.Sign(dx) * MathHelper.Clamp(MathF.Abs(dx) / 34f, 9f, 15f), -3.4f);
                //目标已在水线之下：打水漂弹不过去，直接压弹道
                bool noSkip = tgt.Y > domain.LakeWorldY + 20f;
                if (noSkip) {
                    vel = (tgt - mouth).SafeNormalize(Vector2.UnitX) * 13.5f;
                }

                Projectile.velocity -= vel.SafeNormalize(Vector2.Zero) * 4.2f;
                Projectile.velocity.Y -= 0.8f;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.85f, Pitch = -0.25f, MaxInstances = 2 }, mouth);
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 2 }, mouth);
                if (!Main.dedServ) {
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(mouth + Main.rand.NextVector2Circular(4f, 4f),
                            vel.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.3f) * Main.rand.NextFloat(2.5f, 7f),
                            EyeEmber, Main.rand.NextFloat(0.7f, 1.3f))?.Configure(true, Main.rand.Next(14, 24));
                    }
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth,
                            vel.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f),
                            BloodDeep, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 24));
                    }
                    PRTLoader.NewParticle<PRT_GhostRainMist>(mouth, vel * 0.05f,
                        MistBlood * 0.7f, 0.6f)?.Configure(40);
                }
                if (ViewedOwner) {
                    ShakeViewer(2f);
                }
                //弹体只在 owner 端生成，spawn 参数自带全部初值
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(MagmaDamage);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                        ModContent.ProjectileType<KikasaGolemMagmaBall>(), damage, 4f,
                        Projectile.owner, noSkip ? 1f : 0f, freshTarget);
                }
                return;
            }

            //合口回摆
            Projectile.velocity *= 0.94f;
            if (t >= MagmaRecoverEnd) {
                EndAttack(authority, 110);
            }
        }

        //==================== 眼窝血珠喷吐 ====================

        private void UpdateEyeSpit(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= SpitTellEnd) {
                EndAttack(authority, 45);
                return;
            }

            if (t <= SpitTellEnd) {
                //tell：眼窝血光扫烁（绘制层承担），身体稳住
                Projectile.velocity *= 0.9f;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                }
                return;
            }

            if (t <= SpitTellEnd + SpitPairGap * SpitPairCount) {
                int pairIndex = (t - SpitTellEnd - 1) / SpitPairGap;
                if ((t - SpitTellEnd - 1) % SpitPairGap == 0 && pairIndex < SpitPairCount
                    && lastSpitFired < pairIndex) {
                    lastSpitFired = pairIndex;
                    FireEyeSpit(owner, target, pairIndex, authority);
                }
                Projectile.velocity *= 0.92f;
                return;
            }

            if (t >= SpitRecoverEnd) {
                EndAttack(authority, 80);
            }
            else {
                Projectile.velocity *= 0.94f;
            }
        }

        private void FireEyeSpit(Player owner, int target, int pairIndex, bool authority) {
            //每对小弹的微后坐：石头也会点一下头
            Projectile.velocity.Y += 0.9f;
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = -0.2f + pairIndex * 0.06f, MaxInstances = 3 }, Projectile.Center);

            Vector2 tgt = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                : Projectile.Center + Vector2.UnitX * Projectile.velocity.X * 30f;

            for (int side = -1; side <= 1; side += 2) {
                Vector2 eye = EyePos(side);
                Vector2 aim = (tgt - eye).SafeNormalize(Vector2.UnitX);
                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(eye,
                            aim.RotatedByRandom(0.35f) * Main.rand.NextFloat(1.5f, 4f),
                            Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                    }
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SpitDamage);
                    Vector2 vel = aim.RotatedBy(side * 0.05f + (pairIndex - 1) * 0.03f) * 12f;
                    vel.Y -= 0.6f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), eye, vel,
                        ModContent.ProjectileType<KikasaGolemEyeSpit>(), damage, 2.5f, Projectile.owner);
                }
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 碎裂溶解：裂缝蔓延→碎块坠湖→血水收干 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (t < CrackFrames) {
                //裂缝蔓延：身体死住、微沉，裂响一声声逼近，石屑与血一起掉
                Projectile.velocity *= 0.9f;
                Projectile.velocity.Y += 0.05f;
                if (t == 6 || t == 18 || t == 30 || t == 40) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.45f, MaxInstances = 3 }, Projectile.Center);
                }
                if (!Main.dedServ) {
                    if (t % 3 == 0) {
                        SpawnChip(Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                            new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.2f, 1.2f)));
                    }
                    if (t % 2 == 0) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(28f, 28f),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                            ?.Configure(Main.rand.Next(14, 24));
                    }
                }
                return;
            }

            if (!crumbled) {
                //碎裂拍：整颗头碎成石块，从原位散开坠湖
                crumbled = true;
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 1f, Pitch = -0.45f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.7f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(3.5f);
                }
                if (!Main.dedServ) {
                    SpawnShatterChunks();
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(
                            Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                            new Vector2(0f, -0.3f), MistBlood * 0.7f,
                            Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(40, 70));
                    }
                }
                Projectile.velocity = Vector2.Zero;
            }

            //碎块沉完，血水从水面裂缝里收干
            if (!drainDone && t >= DrainFrame) {
                drainDone = true;
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, new Vector2(Projectile.Center.X, lakeY));
                if (lakeAlive && ViewedOwner) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.9f);
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + 34f, lakeY), 0.55f);
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X - 30f, lakeY), 0.45f);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(Projectile.Center.X, lakeY - 8f),
                        new Vector2(0f, -0.4f), MistBlood * 0.6f, 0.8f)?.Configure(50);
                }
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        //==================== 石块碎屑模拟 ====================

        /// <summary>小石屑：从头部贴图上随机撕一小块，纯本地表现</summary>
        private void SpawnChip(Vector2 pos, Vector2 vel) {
            if (Main.dedServ || shards.Count >= ShardCap) {
                return;
            }
            Main.instance.LoadNPC(NPCID.GolemHeadFree);
            Texture2D tex = TextureAssets.Npc[NPCID.GolemHeadFree]?.Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.GolemHeadFree];
            int size = Main.rand.Next(8, 15);
            shards.Add(new StoneShard {
                Pos = pos,
                Vel = vel,
                Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                RotVel = Main.rand.NextFloat(-0.14f, 0.14f),
                Src = new Rectangle(Main.rand.Next(8, tex.Width - size - 8),
                    Main.rand.Next(8, frameH - size - 8), size, size),
                MaxLife = Main.rand.Next(60, 100),
            });
        }

        /// <summary>碎裂拍：闭口帧切成 3×4 块，各块从原位带角速度散开，头是碎的，不是化的</summary>
        private void SpawnShatterChunks() {
            Main.instance.LoadNPC(NPCID.GolemHeadFree);
            Texture2D tex = TextureAssets.Npc[NPCID.GolemHeadFree]?.Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.GolemHeadFree];
            int pw = tex.Width / 3;
            int ph = frameH / 4;
            for (int cx = 0; cx < 3; cx++) {
                for (int cy = 0; cy < 4; cy++) {
                    if (shards.Count >= ShardCap) {
                        return;
                    }
                    Rectangle src = new(cx * pw, cy * ph, pw, ph);
                    Vector2 offset = (src.Center() - new Vector2(tex.Width, frameH) * 0.5f) * DrawScale;
                    Vector2 outward = offset.SafeNormalize(Main.rand.NextVector2Unit());
                    shards.Add(new StoneShard {
                        Pos = Projectile.Center + offset.RotatedBy(Projectile.rotation),
                        Vel = outward * Main.rand.NextFloat(0.6f, 1.9f) + new Vector2(0f, 0.5f),
                        Rot = Projectile.rotation,
                        RotVel = Main.rand.NextFloat(-0.14f, 0.14f),
                        Src = src,
                        MaxLife = 100,
                    });
                }
            }
        }

        /// <summary>碎块推进：重物下坠，入水各自一朵水花，水下拖惰渐隐</summary>
        private void UpdateShards(KikasaDomainPlayer domain) {
            if (shards.Count == 0) {
                return;
            }
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;
            bool viewed = ViewedOwner;
            int splashBudget = 2;
            bool soundLeft = true;

            for (int i = shards.Count - 1; i >= 0; i--) {
                StoneShard s = shards[i];
                s.Life++;
                s.Vel.Y = MathF.Min(s.Vel.Y + 0.42f, 13f);
                s.Vel.X *= 0.99f;
                if (s.InWater) {
                    s.Vel *= 0.86f;
                    s.RotVel *= 0.9f;
                }
                s.Pos += s.Vel;
                s.Rot += s.RotVel;

                if (lakeAlive && !s.InWater && s.Pos.Y >= lakeY) {
                    s.InWater = true;
                    if (viewed && splashBudget > 0) {
                        splashBudget--;
                        Vector2 hit = new(s.Pos.X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, 0.45f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                hit + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                                new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.8f, 3.6f)),
                                SteamPale * Main.rand.NextFloat(0.4f, 0.55f),
                                Main.rand.NextFloat(0.35f, 0.55f))
                                ?.Configure(Main.rand.Next(14, 24), 0f);
                        }
                        if (soundLeft) {
                            soundLeft = false;
                            SoundEngine.PlaySound(SoundID.SplashWeak with {
                                Volume = 0.5f,
                                Pitch = -0.2f + i % 6 * 0.03f,
                                MaxInstances = 3
                            }, hit);
                        }
                    }
                }

                if (s.Life >= s.MaxLife || s.Pos.Y > lakeY + 110f
                    || s.Pos.Y > Main.screenPosition.Y + Main.screenHeight + 200f) {
                    shards.RemoveAt(i);
                }
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1500f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1100f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, owner.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>眼窝世界位（side=-1 左 +1 右），随头体旋转</summary>
        private Vector2 EyePos(int side)
            => Projectile.Center + new Vector2(side * 19f, -8f).RotatedBy(Projectile.rotation);

        private Vector2 MouthPos()
            => Projectile.Center + new Vector2(0f, 16f).RotatedBy(Projectile.rotation);

        /// <summary>裂缝渗血锚点（贴体固定三处），随头体旋转</summary>
        private Vector2 CrackPos(int i) {
            Vector2 offset = i switch {
                0 => new Vector2(-26f, 10f),
                1 => new Vector2(14f, 22f),
                _ => new Vector2(30f, -18f),
            };
            return Projectile.Center + offset.RotatedBy(Projectile.rotation);
        }

        /// <summary>湿度收支：水下拉满、出水淌干；湿度高时眼窝与裂缝持续倾泻</summary>
        private void UpdateWetness(KikasaDomainPlayer domain) {
            if (Projectile.Center.Y >= domain.LakeWorldY - 4f) {
                wetness = 1f;
            }
            else {
                wetness = MathF.Max(0f, wetness - 0.008f);
            }
            if (Main.dedServ || crumbled || wetness < 0.5f
                || Projectile.Center.Y >= domain.LakeWorldY || CurrentAlpha() < 0.5f) {
                return;
            }
            //倾泻强度随湿度：刚出水像开了闸，淌干后只剩裂缝渗珠
            int t = (int)StateTimer;
            for (int side = -1; side <= 1; side += 2) {
                if (Main.rand.NextFloat() < wetness * 0.6f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        EyePos(side) + new Vector2(Main.rand.NextFloat(-3f, 3f), 4f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(2.6f, 4.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.4f, 0.62f))
                        ?.Configure(Main.rand.Next(16, 28), 0f);
                }
            }
            if (t % 3 == 0 && Main.rand.NextFloat() < wetness) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    CrackPos(Main.rand.Next(3)) + Main.rand.NextVector2Circular(3f, 3f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.8f, 3.4f)),
                    Color.Lerp(BloodMain, SteamPale, wetness * 0.5f) * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(14, 24), 0f);
            }
        }

        private void UpdateFrames() {
            int t = (int)StateTimer;
            //张口：火球蓄热喷吐期 + 出水倾泻期（水从口里灌出来）
            mouthOpen = State == StateMagma && t > MagmaBrakeEnd && t < MagmaRecoverEnd - 10
                || State == StateEmerge && t >= OmenFrames && t < AwakenFrame;
            eyeAnimTick = (eyeAnimTick + 1) % 16;
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 5f, 0f, 1f),
                StateDissolve => crumbled ? 0f : 1f,
                _ => 1f,
            };
        }

        /// <summary>uForm 反向读数：几乎不血水化，石头本身，湿度只借走一点水光</summary>
        private float CurrentForm() {
            float steady = 0.055f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 0.02f
                + wetness * 0.34f;
            if (State == StateEmerge) {
                int t = (int)StateTimer;
                float riseK = MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f);
                return MathHelper.Lerp(0.6f, steady, SmoothStep01(riseK));
            }
            return MathHelper.Clamp(steady, 0f, 0.75f);
        }

        /// <summary>眼窝血光强度：觉醒前 0，觉醒闪燃，碎裂前挣扎熄灭</summary>
        private float EyeAlpha() {
            int t = (int)StateTimer;
            if (State == StateEmerge) {
                if (t < AwakenFrame) {
                    return 0f;
                }
                //闪燃：两次扑闪后立稳
                float f = (t - AwakenFrame) / (float)(EmergeTotal - AwakenFrame);
                float flicker = MathF.Sin(f * 26f) * 0.35f * (1f - f);
                return MathHelper.Clamp(f * 1.6f + flicker, 0f, 1f);
            }
            if (State == StateDissolve) {
                if (crumbled) {
                    return 0f;
                }
                float dying = 1f - StateTimer / (float)(CrackFrames - 4);
                float flicker = MathF.Sin(StateTimer * 1.7f + Seed) * 0.3f * dying;
                return MathHelper.Clamp(dying + flicker, 0f, 1f);
            }
            return 1f;
        }

        /// <summary>蓄势进度 0~1：砸水悬停 / 火球蓄热 / 眼吐 tell 的眼光增压共用</summary>
        private float ChargeLevel() {
            int t = (int)StateTimer;
            switch (State) {
                case StateSlam when (int)StateParam == 1:
                    return MathHelper.Clamp(t / (float)SlamHoverFrames, 0f, 1f);
                case StateMagma when t > MagmaBrakeEnd && t < MagmaFireFrame + 6:
                    return MathHelper.Clamp((t - MagmaBrakeEnd) / (float)(MagmaFireFrame - MagmaBrakeEnd), 0f, 1f);
                case StateEyeSpit when t <= SpitTellEnd + SpitPairGap * SpitPairCount:
                    //tell 期扫烁：锯齿闪，不是平滑增压
                    float k = MathHelper.Clamp(t / (float)SpitTellEnd, 0f, 1f);
                    return k * (0.6f + 0.4f * MathF.Sin(t * 1.2f + Seed));
                default:
                    return 0f;
            }
        }

        /// <summary>碎裂前的濒死震颤偏移（纯绘制层）</summary>
        private Vector2 ShudderOffset() {
            if (State != StateDissolve || crumbled) {
                return Vector2.Zero;
            }
            float amp = StateTimer / (float)CrackFrames * 2.2f;
            return new Vector2(
                MathF.Sin(StateTimer * 2.9f + Seed) * amp,
                MathF.Sin(StateTimer * 3.7f + Seed * 2f) * amp * 0.6f);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.GolemHeadFree);
            Texture2D tex = TextureAssets.Npc[NPCID.GolemHeadFree]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.GolemHeadFree];
            Rectangle frame = new(0, mouthOpen ? frameH : 0, tex.Width, frameH);
            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();

            //砸水预告：落点水面暗斑（湖面上的头影）
            DrawSlamShadow(sb, domain);

            //本体：石头本身，血水只是浸润
            if (!crumbled && alpha > 0.01f) {
                DrawBody(sb, tex, frame, alpha);
            }

            //石块碎屑：主批直接画，水下渐隐
            DrawShards(sb, tex, lightColor, domain);

            //加色层：水下预兆暖光 / 眼窝血光与积血晃动 / 蓄势增压 / 双水柱余辉
            DrawGlow(sb, tex, frame, alpha, domain);

            return false;
        }

        private void DrawSlamShadow(SpriteBatch sb, KikasaDomainPlayer domain) {
            if (State != StateSlam || (int)StateParam > 1 || domain == null) {
                return;
            }
            //暗色落点影必须用真 alpha 的 Extra_98：黑底 SoftGlow 在 AlphaBlend 里会糊出黑块
            Texture2D shadow = CWRAsset.Extra_98?.Value;
            if (shadow == null) {
                return;
            }
            int t = (int)StateTimer;
            //升天期渐显，悬停期收拢脉动，暗斑越攥越实
            float strength = (int)StateParam == 0
                ? MathHelper.Clamp(t / 40f, 0f, 0.55f)
                : 0.55f + 0.25f * MathF.Sin(t * 0.42f + Seed);
            float width = (int)StateParam == 0 ? 130f : 110f - t * 0.5f;
            Vector2 pos = new(slamX, domain.LakeWorldY + 2f);
            //×2 补偿 Extra_98 相对 SoftGlow 更紧的径向衰减，视觉尺寸对齐原稿
            sb.Draw(shadow, pos - Main.screenPosition, null,
                new Color(16, 5, 7) * (0.5f * strength), 0f, shadow.Size() * 0.5f,
                new Vector2(width * 2f / shadow.Width, 18f / shadow.Height) * 2f, SpriteEffects.None, 0f);
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(CurrentForm());
                form.Parameters["uDissolve"]?.SetValue(0f);
                form.Parameters["uScanMode"]?.SetValue(0f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                //无着色器回退：CPU 石色微血染
                color = Color.Lerp(Color.White, BloodMain, 0.2f) * alpha;
            }

            sb.Draw(tex, Projectile.Center + ShudderOffset() - Main.screenPosition, frame, color,
                Projectile.rotation, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawShards(SpriteBatch sb, Texture2D tex, Color lightColor, KikasaDomainPlayer domain) {
            if (shards.Count == 0) {
                return;
            }
            float lakeY = domain?.LakeWorldY ?? float.MaxValue;
            foreach (StoneShard s in shards) {
                float fade = 1f - MathHelper.Clamp((s.Life - s.MaxLife + 24f) / 24f, 0f, 1f);
                if (s.Pos.Y > lakeY) {
                    fade *= 1f - MathHelper.Clamp((s.Pos.Y - lakeY) / 90f, 0f, 0.8f);
                }
                if (fade <= 0.02f) {
                    continue;
                }
                Color tint = Color.Lerp(lightColor, BloodDeep, 0.22f) * fade;
                sb.Draw(tex, s.Pos - Main.screenPosition, s.Src, tint, s.Rot,
                    s.Src.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawGlow(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha, KikasaDomainPlayer domain) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (domain == null) {
                return;
            }

            bool begun = false;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            int t = (int)StateTimer;

            //预兆：水下暖光自深处苏醒
            if (glow != null && State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(46f, 10f, ease));
                float r = 30f + 26f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, EyeEmber * (0.38f * ease), 0f,
                    glow.Size() * 0.5f, new Vector2(r * 2.8f / glow.Width, r * 1.0f / glow.Height), SpriteEffects.None, 0f);
            }

            //眼部发光层：原版帧布局，Extra106 四步眼焰动画 + Extra107 辉光罩
            float eyeA = EyeAlpha() * alpha;
            float charge = ChargeLevel();
            if (!crumbled && eyeA > 0.02f) {
                EnsureBegin();
                Vector2 bodyPos = Projectile.Center + ShudderOffset() - Main.screenPosition;
                Color eyeColor = EyeEmber * (eyeA * (0.55f + charge * 0.45f));

                Texture2D eyeAnim = TextureAssets.Extra[106]?.Value;
                if (eyeAnim != null) {
                    int fh = eyeAnim.Height / 8;
                    Rectangle eyeFrame = new(0, (eyeAnimTick / 4 * 2 + (mouthOpen ? 1 : 0)) * fh,
                        eyeAnim.Width, fh);
                    sb.Draw(eyeAnim, bodyPos, eyeFrame, eyeColor, Projectile.rotation,
                        eyeFrame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                }
                Texture2D eyeMask = TextureAssets.Extra[107]?.Value;
                if (eyeMask != null) {
                    sb.Draw(eyeMask, bodyPos, frame, eyeColor * 0.8f, Projectile.rotation,
                        frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                }

                //眼窝积血：两粒滞后晃动的血光，石头急动时血水在窝里甩
                if (glow != null) {
                    for (int side = -1; side <= 1; side += 2) {
                        Vector2 pool = EyePos(side) + eyeSlosh * 0.6f;
                        float r = 6.5f + charge * 5f;
                        sb.Draw(glow, pool - Main.screenPosition, null,
                            BloodMain * (0.42f * eyeA), 0f, glow.Size() * 0.5f,
                            new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }

                //口部蓄热：火球张口期的炉光
                if (glow != null && State == StateMagma && charge > 0.05f) {
                    Vector2 mouth = MouthPos();
                    float r = 8f + 16f * charge;
                    sb.Draw(glow, mouth - Main.screenPosition, null,
                        EyeEmber * (0.5f * charge * alpha), 0f, glow.Size() * 0.5f,
                        new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //双水柱余辉：起爆后 30 帧内两道渐熄的窄高光带把柱身连起来
            if (glow != null && columnAge < 30) {
                EnsureBegin();
                float k = 1f - columnAge / 30f;
                float height = MathHelper.Lerp(120f, 210f, 1f - k * k);
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 basePos = new(columnX + side * 46f, domain.LakeWorldY - height * 0.5f);
                    sb.Draw(glow, basePos - Main.screenPosition, null,
                        SteamPale * (0.34f * k), 0f, glow.Size() * 0.5f,
                        new Vector2(20f / glow.Width * 2f, height * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //砸中的碾压感（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(3f, 3f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            for (int i = 0; i < 4; i++) {
                SpawnChip(target.Center + Main.rand.NextVector2Circular(18f, 18f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 3.5f)));
            }
            SoundEngine.PlaySound(SoundID.NPCHit41 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残迹：异常移除也留一摊血与石屑
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
