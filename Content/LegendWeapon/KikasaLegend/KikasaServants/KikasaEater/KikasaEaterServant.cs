using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEater
{
    /// <summary>
    /// 鬼奴·湖水版世界吞噬怪。单弹幕内部模拟整条短链血蟒（头+18体+尾），
    /// 与毁灭者的机械直线相对：高转向柔性的有机 S 形蜿蜒，链体跟随阻尼更松。
    /// 出场为湖面裂缝：水线先被从中扯开一道横缝，蟒身自缝里挤出、S 形爬升。
    /// 签名机制是空中血水裂隙对：入口撕在自己嘴前、出口撕在猎物身旁，
    /// 蟒从嘴前一节节穿入、自敌旁破门同向直贯冲撞，借门跨越两者间的全部距离
    /// （每次转移都有可见的入口→出口穿行，无瞬移假身）。
    /// 第二攻击为腐蚀血痰齐射（命中或落水爆成滞留腐蚀血雾，见 KikasaEaterCorrosiveSpit）。
    /// 跟随态还有链体"裂开又弥合"的分裂假动作，纯演出错位再咬合，不产生实体。
    /// 联机同克眼契约：状态走 ai[0..2]、owner 转场盖 netUpdate 章、
    /// 裂隙端点是不可归约的一次性裁决量，经 SendExtraAI 随包同步；
    /// 链体各端本地重建，节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaEaterServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>裂隙直贯冲撞接触基伤（召唤加成前）</summary>
        internal const int RamDamage = 450;

        /// <summary>腐蚀血痰基伤（召唤加成前），血痰弹幕消费</summary>
        internal const int SpitDamage = 240;

        //==================== 链体尺寸 ====================

        internal const int SegCount = 20;
        internal const float DrawScale = 1.2f;
        /// <summary>节距 = 原版 EoW 节宽 38 × 缩放，略压紧让弯身连贯</summary>
        internal const float SegSpacing = 42f;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRiftRam = 2;
        private const int StateSpitVolley = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：出水期=起跳横向符号；攻击期=相位号</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //湖面裂缝出水：分水预兆→撕裂拍→S 形爬升→觉醒回首→落定
        private const int OmenEnd = 36;
        private const int RiseEnd = 88;
        private const int EmergeTotal = 112;
        private const int EmergeTimeout = 280;
        private const int SeamCloseFrames = 14;

        //裂隙冲撞：撕隙蓄势→俯冲入口→穿出直贯→收势弥合
        private const int RiftWindupFrames = 30;
        private const int RiftDiveTimeout = 40;
        private const int RiftRamActive = 26;
        private const int RiftRecoverTimeout = 46;
        private const int RiftCloseFrames = 14;

        //血痰齐射：盘身后拉→静默→齐射与余势→回摆
        private const int CoilFrames = 26;
        private const int SpitSilence = 6;
        private const int SalvoHold = 18;
        private const int SpitRecover = 16;
        private const int SalvoCount = 5;

        private const int DissolvePerSegGap = 3;
        private const int DissolveSegFrames = 24;
        private const int DissolveTotal = (SegCount - 1) * DissolvePerSegGap + DissolveSegFrames + 10;

        //分裂假动作：撕开→错位保持→咬合→余韵
        private const int FeintCycle = 220;
        private const int FeintTrigger = 150;
        private const int FeintOpenEnd = 10;
        private const int FeintHoldEnd = 18;
        private const int FeintSnapEnd = 24;
        private const int FeintTotal = 34;
        private const int FeintZoneStart = 7;
        private const int FeintZoneEnd = 12;

        //==================== 链体数据（各端本地重建，头位置由同步纠偏）====================

        //毁灭者鬼奴同源跟随，阻尼加松到 0.24：弯得动才是活蟒
        private readonly Vector2[] spine = new Vector2[SegCount];
        /// <summary>蠕虫约定旋转（指向前节的方向角 + PiOver2）；原版 EoW 贴图头朝上，绘制直用不加翻转</summary>
        private readonly float[] segRot = new float[SegCount];
        /// <summary>节湿度：过水线或穿裂隙拉满、出水后衰减，驱动滴落与材质血水度</summary>
        private readonly float[] wetness = new float[SegCount];
        private readonly bool[] belowWater = new bool[SegCount];
        /// <summary>裂隙穿行标记：该节是否已从入口传到出口侧</summary>
        private readonly bool[] segThrough = new bool[SegCount];
        private bool spineInit;

        //==================== 裂隙端点（owner 一次性裁决，SendExtraAI 随包同步）====================

        private Vector2 riftEntry;
        private Vector2 riftExit;

        /// <summary>穿行方向（入口指向出口）：钻入与穿出同向，直线被虫洞折叠</summary>
        private Vector2 RiftInDir => (riftExit - riftEntry).SafeNormalize(Vector2.UnitX);

        private bool PortalActive => State == StateRiftRam && riftEntry != Vector2.Zero;

        //==================== 本地表现量（不入同步）====================

        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool launchDone;
        private bool awakenDone;
        private bool riftTearSounded;
        private bool ramBurstDone;
        private bool salvoFired;
        private float seamX;
        private bool seamInit;
        private int seamCloseTimer = -1;
        private int feintTimer = -1;
        private int lastFeintCycle = -1;
        /// <summary>齐射期头部朝向锁（NaN=不锁，方向角语义）</summary>
        private float lockedHeadRot = float.NaN;
        //裂隙绘制缓存：状态退出后仍要演完弥合
        private Vector2 riftDrawEntry;
        private Vector2 riftDrawExit;
        private Vector2 riftDrawDir = Vector2.UnitX;
        private float riftVisEntry;
        private float riftVisExit;

        //==================== 血系配色（CoolTint 家族，腐蚀紫只做次要点缀）====================

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>蚀骨紫：世界吞噬怪的专属次要点缀</summary>
        internal static Color CorrodePurple => KikasaDomain.CoolTint(new(142, 86, 170), new(104, 116, 140));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（裂缝撕开点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            //起点在裂缝正下方湖里，蟒身垂在缝下等着往外挤
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 64f), Vector2.Zero,
                ModContent.ProjectileType<KikasaEaterServant>(), damage, 8f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //链体加裂隙对远超 hitbox，头出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在穿出裂隙的直贯窗，与可见的冲撞严格对齐</summary>
        public override bool? CanDamage()
            => State == StateRiftRam && (int)StateParam == 2 && StateTimer <= RiftRamActive
                ? null : false;

        /// <summary>多节命中：相邻脊柱点两两线碰撞；冲撞窗内跨着裂隙口的节对不连线
        /// 那条"线"物理上并不存在，两侧可见的身体照常判</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!spineInit) {
                return false;
            }
            bool ramWindow = State == StateRiftRam && (int)StateParam == 2;
            float _ = 0f;
            for (int i = 1; i < SegCount; i++) {
                if (ramWindow && segThrough[i] != segThrough[i - 1]) {
                    continue;
                }
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    spine[i - 1], spine[i], 26f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override bool? CanCutTiles() => false;

        //==================== 裂隙端点同步 ====================

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(riftEntry.X);
            writer.Write(riftEntry.Y);
            writer.Write(riftExit.X);
            writer.Write(riftExit.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            riftEntry = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            riftExit = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //湖面还没裂开就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < OmenEnd) {
                Projectile.Kill();
                return;
            }
            //出水一半被遣返：缝跟着合口，不许一帧凭空消失
            if (State == StateEmerge && seamCloseTimer < 0) {
                seamCloseTimer = 0;
            }
            //裂隙穿行到一半被打断：把链体收拢回头侧，别让残链横跨半屏
            if (PortalActive && !AllThrough()) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
            }
            riftEntry = Vector2.Zero;
            riftExit = Vector2.Zero;
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

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约），别处判会当场误杀
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);

            //换场清闩：远端可能靠收包换场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                bool leftPortal = lastSeenState == StateRiftRam;
                lastSeenState = State;
                riftTearSounded = false;
                ramBurstDone = false;
                salvoFired = false;
                lockedHeadRot = float.NaN;
                feintTimer = -1;
                lastFeintCycle = -1;
                if (State == StateRiftRam) {
                    Array.Clear(segThrough, 0, SegCount);
                }
                //穿行没走完就被包拽出攻击态：收拢链体防跨屏拉丝
                else if (leftPortal && !AllThrough()) {
                    RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                }
            }

            if (!spineInit) {
                RebuildChain(-Vector2.UnitY);
            }
            if (!seamInit) {
                seamInit = true;
                seamX = Projectile.Center.X;
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateRiftRam: UpdateRiftRam(owner, authority); break;
                case StateSpitVolley: UpdateSpitVolley(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateChain(domain);
            UpdateSeam(domain);
            UpdateRiftVisual();
            UpdateFeint();
            UpdateDrips();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //沿链补光：血红里掺一丝蚀紫
            for (int i = 0; i < SegCount; i += 4) {
                Lighting.AddLight(spine[i], 0.22f, 0.06f, 0.10f);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        private bool AllThrough() {
            for (int i = 0; i < SegCount; i++) {
                if (!segThrough[i]) {
                    return false;
                }
            }
            return true;
        }

        //==================== 湖面裂缝出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            float dir = MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);

            if (t < OmenEnd) {
                //分水预兆：两列涟漪自缝心向两侧推开，水面被从中扯开的前奏
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 5 == 2) {
                        float sep = 10f + t * 1.3f;
                        float scale = 0.30f + t / (float)OmenEnd * 0.45f;
                        KikasaDomainDeco.RippleAt(new Vector2(seamX - sep, lakeY), scale);
                        KikasaDomainDeco.RippleAt(new Vector2(seamX + sep, lakeY), scale);
                    }
                    if (t == 8 || t == 24) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.5f,
                            Pitch = t == 8 ? -0.6f : -0.25f,
                            MaxInstances = 2
                        }, new Vector2(seamX, lakeY));
                        ShakeViewer(t == 8 ? 0.8f : 1.2f);
                    }
                }
                return;
            }

            if (!launchDone) {
                //撕裂拍：缝一帧扯开，蟒头带着仰角一帧起速往外挤
                launchDone = true;
                Projectile.velocity = new Vector2(dir * 3.4f, -19.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    SeamTearBurst(new Vector2(seamX, lakeY));
                }
            }

            if (t <= RiseEnd) {
                //S 形爬升：纵向指数衰减不做匀速，横向正弦蜿蜒随爬升渐入
                float riseT = t - OmenEnd;
                float weaveIn = MathHelper.Clamp(riseT / 12f, 0f, 1f);
                Projectile.velocity.Y = -19.5f * MathF.Exp(-0.045f * riseT);
                Projectile.velocity.X = dir * 3.4f * MathF.Exp(-0.03f * riseT)
                    + MathF.Sin(riseT * 0.16f + Seed) * 4.3f * weaveIn;
            }
            else {
                Vector2 anchor = owner.Center + new Vector2(-owner.direction * 150f, -120f);
                if (!awakenDone) {
                    //觉醒回首：升势散尽的瞬间一帧甩头奔悬点，吐一声高频嘶吼
                    awakenDone = true;
                    Projectile.velocity = (anchor - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 9f;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        ShakeViewer(1.6f);
                    }
                }
                //落定：弯向主人侧上方的悬点
                Vector2 want = (anchor - Projectile.Center) * 0.06f;
                if (want.Length() > 12f) {
                    want = want.SafeNormalize(Vector2.Zero) * 12f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }

            if (t >= EmergeTotal || t > EmergeTimeout) {
                //缝还没开始合口就要离场（超时兜底路径）：现在合
                if (seamCloseTimer < 0) {
                    seamCloseTimer = 0;
                }
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>撕裂拍浪冠：两翼斜抛血珠扇，缝是被"扯开"的，水往两边翻</summary>
        private void SeamTearBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(64f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(64f, 0f), 1.1f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-30f, 0f), 11);
            KikasaDomainDeco.SplashAt(hit + new Vector2(30f, 0f), 11);

            //两翼各一扇向外上翻的血珠
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i < 12; i++) {
                    float angle = -MathHelper.PiOver2 + side * MathHelper.Lerp(0.22f, 1.05f, i / 11f);
                    float speed = Main.rand.NextFloat(3.5f, 8f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        hit + new Vector2(side * Main.rand.NextFloat(6f, 40f), -4f),
                        angle.ToRotationVector2() * speed,
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
                }
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-50f, 50f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.8f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.8f, 1.1f))
                    ?.Configure(Main.rand.Next(70, 110));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.4f, 1f), -MathHelper.PiOver2, 0.42f, 12);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        /// <summary>出水裂缝的生命周期：撕开→保持到尾节离水→咬合归零</summary>
        private void UpdateSeam(KikasaDomainPlayer domain) {
            if (State != StateEmerge && seamCloseTimer < 0) {
                return;
            }
            int t = (int)StateTimer;
            float lakeY = domain.LakeWorldY;

            if (State == StateEmerge && seamCloseTimer < 0) {
                //尾节全部离水或超时：缝开始咬合
                bool tailClear = true;
                for (int i = 0; i < SegCount; i++) {
                    if (spine[i].Y >= lakeY - 8f) {
                        tailClear = false;
                        break;
                    }
                }
                if ((t > OmenEnd + 20 && tailClear) || t > EmergeTotal - 6) {
                    seamCloseTimer = 0;
                    if (ViewedOwner) {
                        Vector2 hit = new(seamX, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 8);
                        KikasaDomainDeco.RippleAt(hit, 1.2f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, hit);
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, hit);
                    }
                }
                //缝口持续淌血沸腾
                if (ViewedOwner && t > OmenEnd && t % 6 == 1) {
                    float off = Main.rand.NextFloat(-120f, 120f) * SeamOpen(t);
                    KikasaDomainDeco.RippleAt(new Vector2(seamX + off, lakeY), Main.rand.NextFloat(0.25f, 0.45f));
                }
            }
            else if (seamCloseTimer >= 0 && seamCloseTimer <= SeamCloseFrames) {
                seamCloseTimer++;
            }
        }

        /// <summary>出水缝开度：预兆末端发丝缝→撕裂过冲→稳持→咬合</summary>
        private float SeamOpen(float t) {
            if (t < 20f) {
                return 0f;
            }
            if (t < OmenEnd) {
                return MathHelper.Lerp(0.04f, 0.14f, (t - 20f) / (OmenEnd - 20f));
            }
            float sinceTear = t - OmenEnd;
            float open = 1f + 0.22f * MathF.Exp(-sinceTear * 0.14f) * MathF.Cos(sinceTear * 0.5f);
            if (seamCloseTimer >= 0) {
                open *= 1f - MathHelper.Clamp(seamCloseTimer / (float)SeamCloseFrames, 0f, 1f);
            }
            return MathF.Max(open, 0f);
        }

        //==================== 蜿蜒跟随 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            //8 字巡游锚：横长利萨如轨迹，头沿弯道游、链体自然摆出 S
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 150f, -120f);
            float w = (float)StateTimer * 0.030f + Seed;
            anchor += new Vector2(MathF.Sin(w) * 170f, MathF.Sin(w * 2f + Seed * 2f) * 58f);

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildChain(Vector2.UnitX * owner.direction);
                Projectile.netUpdate = authority;
                return;
            }
            float maxSpeed = to.Length() > 1400f ? 24f : 15f;
            Vector2 desired = to * 0.07f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.10f);
            //高频小摆蜿蜒叠在大 8 字上：旋速度不改速率，纯改弯
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin((float)StateTimer * 0.17f + Seed * 3f) * 0.055f);
            //蟒行不许死停
            if (Projectile.velocity.Length() < 2.6f) {
                Projectile.velocity += (w * 2.3f).ToRotationVector2() * 0.5f;
            }

            //分裂假动作定期上演（确定性节拍，纯演出）
            int cycle = (int)StateTimer / FeintCycle;
            if (feintTimer < 0 && cycle > lastFeintCycle
                && (int)StateTimer % FeintCycle >= FeintTrigger && StateTimer > 90) {
                lastFeintCycle = cycle;
                feintTimer = 0;
            }

            //出手裁决：裂隙冲撞与血痰齐射交替，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 40) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateRiftRam : StateSpitVolley;
                StateTimer = 0;
                StateParam = 0;
                riftEntry = Vector2.Zero;
                riftExit = Vector2.Zero;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 裂隙对直贯冲撞 ====================

        private void UpdateRiftRam(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            //远端可能先收到相位包、后收到裂隙端点：原地缓一拍等包，别奔世界原点
            if (phase > 0 && riftEntry == Vector2.Zero) {
                Projectile.velocity *= 0.9f;
                if (t > RiftDiveTimeout * 2) {
                    EndAttack(authority, 60);
                }
                return;
            }

            if (phase == 0) {
                //撕隙蓄势：owner 首帧一次性裁决裂隙端点（入口撕在自己嘴前、出口撕在猎物身旁，
                //两点共线于奔袭直线：从嘴前扎进去、敌旁破门直贯，穿行进出同向）
                if (riftEntry == Vector2.Zero) {
                    if (target < 0) {
                        EndAttack(authority, 45);
                        return;
                    }
                    if (authority) {
                        NPC npc = Main.npc[target];
                        Vector2 aim = npc.Center + npc.velocity * 10f;
                        Vector2 dirToAim = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        float dist = Vector2.Distance(Projectile.Center, aim);
                        riftEntry = Projectile.Center + dirToAim * MathF.Min(240f, dist * 0.25f);
                        riftExit = aim - dirToAim * MathF.Min(360f, dist * 0.30f);
                        //门距下限：两门贴得太近读不出传送，入口沿线回拉
                        if (Vector2.Dot(riftExit - riftEntry, dirToAim) < 200f) {
                            riftEntry = riftExit - dirToAim * 200f;
                        }
                        Projectile.netUpdate = true;
                    }
                    return;
                }

                Vector2 inDir = RiftInDir;
                if (!riftTearSounded) {
                    riftTearSounded = true;
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, riftEntry);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = 0.25f, MaxInstances = 2 }, riftEntry);
                    if (ViewedOwner) {
                        RiftTearFX(riftEntry, inDir);
                        ShakeViewer(2f);
                    }
                }
                //出口撕开滞后几拍，先嘴前后敌旁，撕裂有先后因果
                if (t == 9 && ViewedOwner) {
                    RiftTearFX(riftExit, inDir);
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.45f, Pitch = -0.45f, MaxInstances = 2 }, riftExit);
                }

                //预备迟发后拉：pow(6) 憋到最后猛吸一口气，72% 后蓄势粒子静默
                float k = MathF.Pow(MathHelper.Clamp(t / (float)RiftWindupFrames, 0f, 1f), 6f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -inDir * (2f + 15f * k), 0.3f);
                if (!Main.dedServ && t < RiftWindupFrames * 0.72f && t % 3 == 1 && ViewedOwner) {
                    Vector2 from = riftEntry + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 150f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (riftEntry - from) * 0.12f,
                        BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(10);
                }

                if (t >= RiftWindupFrames) {
                    //起跳一帧定速扎向入口
                    Projectile.velocity = (riftEntry - Projectile.Center).SafeNormalize(inDir) * 26f;
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }
                return;
            }

            if (phase == 1) {
                //俯冲入口：直线复利续力，直才快；穿口裁决在链体推进里做
                Vector2 toEntry = (riftEntry - Projectile.Center).SafeNormalize(RiftInDir);
                float speed = MathF.Min(Projectile.velocity.Length() * 1.03f, 36f);
                Projectile.velocity = toEntry * speed;

                //头已越过入口平面：整头传送到出口，同向直贯发动
                float overshoot = Vector2.Dot(Projectile.Center + Projectile.velocity - riftEntry, RiftInDir);
                if (overshoot > 0f) {
                    Projectile.Center = riftExit + RiftInDir * overshoot;
                    Projectile.velocity = RiftInDir * 34f;
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                    return;
                }
                if (t > RiftDiveTimeout) {
                    EndAttack(authority, 60);
                }
                return;
            }

            if (phase == 2) {
                //穿出直贯：激活窗内复利续力直撞，后链还在一节节从入口涌进来
                Projectile.velocity *= 1.012f;
                if (t > RiftRamActive) {
                    StateParam = 3;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //收势弥合：硬刹读出分量，等尾节全部穿完再合口
            Projectile.velocity *= t <= 5 ? 0.68f : 0.9f;
            if ((AllThrough() && t >= RiftCloseFrames) || t > RiftRecoverTimeout) {
                EndAttack(authority, 130);
            }
        }

        /// <summary>撕开裂隙的开幕演出：竖直水面被扯开，边缘飞血 + 扩散环</summary>
        private void RiftTearFX(Vector2 center, Vector2 inDir) {
            Vector2 longAxis = RiftLongAxis(inDir);
            for (int i = 0; i < 14; i++) {
                float along = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = center + longAxis * along * 110f;
                Vector2 vel = longAxis.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1f : -1f))
                    * Main.rand.NextFloat(1.5f, 4f) + new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f));
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos, vel,
                    Main.rand.NextBool(4) ? CorrodePurple : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_DWave>(center, Vector2.Zero, BloodDeep, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), inDir.ToRotation(), 0.34f, 10);
            PRTLoader.NewParticle<PRT_GhostRainMist>(center,
                new Vector2(0f, -0.3f), MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(Main.rand.Next(50, 80));
        }

        /// <summary>裂隙长轴：垂直于穿行方向，偏向让"下端"朝世界下方（缘口淌血用）</summary>
        private static Vector2 RiftLongAxis(Vector2 inDir) {
            Vector2 axis = inDir.RotatedBy(MathHelper.PiOver2);
            if (axis.Y < 0f) {
                axis = -axis;
            }
            return axis;
        }

        //==================== 腐蚀血痰齐射 ====================

        private void UpdateSpitVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                : Projectile.Center + (float.IsNaN(lockedHeadRot) ? Vector2.UnitX : lockedHeadRot.ToRotationVector2()) * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //盘身后拉：头锁猎物、身向后坐出一个蓄势的 S 弯
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float wantAngle = aimDir.ToRotation();
                lockedHeadRot = float.IsNaN(lockedHeadRot) ? wantAngle
                    : lockedHeadRot.AngleTowards(wantAngle, MathHelper.Lerp(0.3f, 0.12f, t / (float)CoilFrames));
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aimDir * 2.6f, 0.12f);
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin(t * 0.42f + Seed) * 0.10f);

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄势血珠向口器汇聚，72% 后静默，喷吐前的吸气
                if (!Main.dedServ && t < CoilFrames * 0.72f && t % 3 == 0) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(46f, 100f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (mouth - from) * 0.15f,
                        Main.rand.NextBool(4) ? CorrodePurple : BloodMain * 0.55f,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9);
                }
                if (t >= CoilFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //静默：喉底涌动骤停，只剩头在极小幅打颤
                Projectile.velocity *= 0.72f;
                if (!float.IsNaN(lockedHeadRot)) {
                    lockedHeadRot += MathF.Sin(t * 3.7f + Seed) * 0.006f;
                }
                if (t >= SpitSilence) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                if (!salvoFired) {
                    //齐射一帧五连扇：后坐鞭甩顺着链体传下去
                    salvoFired = true;
                    Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    lockedHeadRot = aimDir.ToRotation();
                    Projectile.velocity = -aimDir * 9f;
                    Projectile.netUpdate = authority;

                    Vector2 mouth = MouthPos();
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 3 }, mouth);
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 3 }, mouth);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 2 }, mouth);
                    if (ViewedOwner) {
                        ShakeViewer(2.4f);
                    }
                    if (!Main.dedServ) {
                        for (int i = 0; i < 8; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth + Main.rand.NextVector2Circular(4f, 4f),
                                aimDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 9f),
                                Main.rand.NextBool(4) ? CorrodePurple : BloodMain,
                                Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
                        }
                        PRTLoader.NewParticle<PRT_DWave>(mouth + aimDir * 10f, Vector2.Zero,
                            BloodDeep, 0.08f)?.Configure(new Vector2(0.55f, 1f), aimDir.ToRotation(), 0.26f, 9);
                    }
                    if (authority) {
                        int damage = (int)Owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SpitDamage);
                        for (int k = 0; k < SalvoCount; k++) {
                            float off = (k - SalvoCount / 2) * 0.19f + Main.rand.NextFloat(-0.03f, 0.03f);
                            float speed = 13.5f - MathF.Abs(k - SalvoCount / 2) * 0.8f;
                            Vector2 vel = aimDir.RotatedBy(off) * speed;
                            //痰是抛出去的：上抛偏置配合弹体重力走弧线
                            vel.Y -= 1.6f;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                                ModContent.ProjectileType<KikasaEaterCorrosiveSpit>(), damage, 3f, Projectile.owner);
                        }
                    }
                }
                //余势：吐完顺着后坐晃两拍再稳住
                Projectile.velocity *= 0.9f;
                if (t >= SalvoHold) {
                    NextPhase(3);
                }
                return;
            }

            //回摆
            Projectile.velocity *= 0.92f;
            if (t >= SpitRecover) {
                EndAttack(authority, 100);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            //穿行没走完就要收场（超时兜底）：先收拢链体再撤裂隙，防一帧跨屏拉丝
            if (PortalActive && !AllThrough()) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
            }
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            riftEntry = Vector2.Zero;
            riftExit = Vector2.Zero;
            lockedHeadRot = float.NaN;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解遣返 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            if (lakeAlive) {
                //头先沉，链体跟着一节节穿回水里
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //化水残珠沿链错拍
            if (!Main.dedServ && t % 3 == 0) {
                int i = Main.rand.Next(SegCount);
                if (SegDissolve(i) is > 0.1f and < 0.9f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        spine[i] + Main.rand.NextVector2Circular(18f, 18f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.2f, 2.6f)),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24));
                }
            }

            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>逐节溶解进度：尾先化、头最后</summary>
        private float SegDissolve(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float start = (SegCount - 1 - i) * DissolvePerSegGap;
            return MathHelper.Clamp((StateTimer - start) / DissolveSegFrames, 0f, 1f);
        }

        //==================== 链体推进（阻尼追踪 + 裂隙穿行映射）====================

        /// <summary>头位硬纠或初始化时沿指定方向直线重建，防链体抽搐</summary>
        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 head = Projectile.Center;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            float wormRot = headDir.ToRotation() + MathHelper.PiOver2;
            //按真实水线初始化水下标记，空中硬贴回不放幻影水花
            float lakeY = Owner?.active == true && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                ? domain.LakeWorldY : float.MaxValue;
            for (int i = 0; i < SegCount; i++) {
                spine[i] = head + back * (i * SegSpacing);
                segRot[i] = wormRot;
                belowWater[i] = spine[i].Y >= lakeY;
                wetness[i] = 1f;
                segThrough[i] = true;
            }
        }

        private void UpdateChain(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;
            bool portal = PortalActive;

            if (portal) {
                //头的穿口状态是路径依赖量，位置分不清"没出发"和"已冲远"
                //相位即真相：2 起头必在出口侧（本地传送或同步包都会把相位推到 2），
                //不做硬纠重建
                bool headWasThrough = segThrough[0];
                segThrough[0] = (int)StateParam >= 2;
                if (!headWasThrough && segThrough[0]) {
                    OnHeadBreachRift();
                }
            }
            else if (Vector2.Distance(spine[0], head) > 140f) {
                //硬纠检测：同步包把头拽走半屏，直线重建
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }

            spine[0] = head;
            if (!float.IsNaN(lockedHeadRot)) {
                segRot[0] = lockedHeadRot + MathHelper.PiOver2;
            }
            else if (Projectile.velocity.Length() > 0.5f) {
                segRot[0] = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //每节独立追前节：目标向量先按转差做阻尼旋转再贴位；
            //阻尼比毁灭者松（0.24），弯得更快，有机蜿蜒的手感来源
            const float dampingInertia = 0.24f;
            Vector2 inDir = portal ? RiftInDir : Vector2.UnitX;
            int crossBudget = 2;
            for (int i = 1; i < SegCount; i++) {
                Vector2 front = spine[i - 1];
                bool chasingPortalMouth = portal && segThrough[i - 1] && !segThrough[i];
                if (chasingPortalMouth) {
                    //前节已在出口侧：本节先奔入口，把前节"越过出口多远"平移回入口延长线上（穿行同向）
                    float beyond = MathF.Max(Vector2.Dot(front - riftExit, inDir), 0f);
                    front = riftEntry + inDir * beyond;
                }

                Vector2 segmentTarget = front - spine[i];
                if (segRot[i - 1] != segRot[i]) {
                    segmentTarget = segmentTarget.RotatedBy(
                        MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * dampingInertia);
                    segmentTarget = segmentTarget.MoveTowards(
                        (segRot[i - 1] - segRot[i]).ToRotationVector2(), 1f);
                }
                segRot[i] = segmentTarget.ToRotation() + MathHelper.PiOver2;
                spine[i] = front - segmentTarget.SafeNormalize(Vector2.Zero) * SegSpacing;

                if (chasingPortalMouth) {
                    //本节也越线了：穿到出口侧，浑身重新浸满血水
                    float overshoot = Vector2.Dot(spine[i] - riftEntry, inDir);
                    if (overshoot > 0f) {
                        spine[i] = riftExit + inDir * overshoot;
                        segRot[i] = inDir.ToRotation() + MathHelper.PiOver2;
                        segThrough[i] = true;
                        wetness[i] = 1f;
                        if (crossBudget > 0 && ViewedOwner) {
                            crossBudget--;
                            SegRiftCrossFX(i, inDir);
                        }
                    }
                }
            }

            UpdateSegmentCrossings(domain);
        }

        /// <summary>头破出口拍：血水炸开、吼声、震屏，直贯的第一口</summary>
        private void OnHeadBreachRift() {
            if (ramBurstDone) {
                return;
            }
            ramBurstDone = true;
            Vector2 outDir = RiftInDir;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 2 }, riftExit);
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.05f, MaxInstances = 2 }, riftExit);
            if (!ViewedOwner) {
                return;
            }
            ShakeViewer(4f);
            for (int i = 0; i < 16; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    riftExit + Main.rand.NextVector2Circular(20f, 20f),
                    outDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 10f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(18, 32));
            }
            PRTLoader.NewParticle<PRT_DWave>(riftExit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), outDir.ToRotation(), 0.4f, 10);
        }

        /// <summary>体节过裂隙口的水花：入口吸走一撮、出口喷出一撮，帧内限量</summary>
        private void SegRiftCrossFX(int i, Vector2 outDir) {
            for (int k = 0; k < 2; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    riftExit + Main.rand.NextVector2Circular(12f, 12f),
                    outDir.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20));
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    riftEntry + Main.rand.NextVector2Circular(24f, 24f),
                    Main.rand.NextVector2Circular(1.8f, 1.8f),
                    BloodDeep * 0.55f, Main.rand.NextFloat(0.25f, 0.45f))
                    ?.Configure(Main.rand.Next(10, 16));
            }
            if (i % 4 == 1) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.3f,
                    Pitch = 0.1f + i * 0.012f,
                    MaxInstances = 3
                }, riftExit);
            }
        }

        /// <summary>逐节过水线（双向）：水花帧内限量、音效只给第一个；出水节湿度拉满</summary>
        private void UpdateSegmentCrossings(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool viewed = ViewedOwner;
            int fxBudget = 2;
            bool soundLeft = true;

            for (int i = 0; i < SegCount; i++) {
                bool below = spine[i].Y >= lakeY;
                if (below != belowWater[i]) {
                    belowWater[i] = below;
                    wetness[i] = 1f;
                    if (lakeAlive && viewed && fxBudget > 0) {
                        fxBudget--;
                        Vector2 hit = new(spine[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, i == 0 ? 0.85f : 0.5f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 4.5f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(14, 26));
                        }
                        if (soundLeft) {
                            soundLeft = false;
                            SoundEngine.PlaySound(SoundID.SplashWeak with {
                                Volume = 0.4f,
                                Pitch = -0.3f + i * 0.015f,
                                MaxInstances = 3
                            }, hit);
                        }
                    }
                }
                //水下恒湿，出水后慢慢淌干
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.011f);
            }
        }

        //==================== 裂隙视觉包络（本地推演，确定性）====================

        private void UpdateRiftVisual() {
            if (PortalActive) {
                riftDrawEntry = riftEntry;
                riftDrawExit = riftExit;
                riftDrawDir = RiftInDir;
                int t = (int)StateTimer;
                int phase = (int)StateParam;
                if (phase == 0) {
                    //撕开带过冲的弹性开口，出口滞后 8 帧
                    riftVisEntry = TearOpen(t);
                    riftVisExit = TearOpen(t - 9);
                }
                else if (phase == 3) {
                    float close = MathHelper.Clamp(t / (float)RiftCloseFrames, 0f, 1f);
                    //尾没穿完不许合口
                    if (!AllThrough()) {
                        close = 0f;
                    }
                    riftVisEntry = 1f - close;
                    riftVisExit = 1f - close;
                }
                else {
                    riftVisEntry = 1f;
                    riftVisExit = 1f;
                }

                //缘口淌血：开着的裂隙沿长轴滴血珠
                if (!Main.dedServ && ViewedOwner && riftVisEntry > 0.5f && (int)StateTimer % 4 == 1) {
                    Vector2 longAxis = RiftLongAxis(riftDrawDir);
                    Vector2 pos = riftDrawEntry + longAxis * Main.rand.NextFloat(-0.85f, 0.85f) * 108f;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.8f, 1.8f)),
                        Main.rand.NextBool(5) ? CorrodePurple : BloodDeep * 0.7f,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 34), 0.28f);
                    if (riftVisExit > 0.5f) {
                        Vector2 pos2 = riftDrawExit + longAxis * Main.rand.NextFloat(-0.85f, 0.85f) * 108f;
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos2,
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.8f, 1.8f)),
                            BloodDeep * 0.7f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(20, 34), 0.28f);
                    }
                }
            }
            else {
                //离场余韵：向零弥合
                riftVisEntry = MathF.Max(0f, riftVisEntry - 1f / RiftCloseFrames);
                riftVisExit = MathF.Max(0f, riftVisExit - 1f / RiftCloseFrames);
            }
        }

        /// <summary>撕开包络：快速拉开 + 弹性过冲回稳</summary>
        private static float TearOpen(float t) {
            if (t <= 0f) {
                return 0f;
            }
            float e = MathHelper.Clamp(t / 22f, 0f, 1f);
            float baseOpen = 1f - (1f - e) * (1f - e) * (1f - e);
            float overshoot = t > 22f ? 0.16f * MathF.Exp(-(t - 22f) * 0.18f) * MathF.Cos((t - 22f) * 0.55f) : 0f;
            return baseOpen + overshoot;
        }

        //==================== 分裂假动作（纯演出：错位再咬合，不产生实体）====================

        private void UpdateFeint() {
            if (feintTimer < 0) {
                return;
            }
            //只在跟随态演；被打断（转攻击/溶解）立刻收
            if (State != StateFollow) {
                feintTimer = -1;
                return;
            }
            feintTimer++;
            bool viewed = ViewedOwner;

            if (feintTimer == 1) {
                //湿撕声：链腹被撑开的那口气
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.4f, Pitch = -0.55f, MaxInstances = 2 }, FeintCenter());
                if (viewed && !Main.dedServ) {
                    for (int k = 0; k < 5; k++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            FeintCenter() + Main.rand.NextVector2Circular(20f, 20f),
                            Main.rand.NextVector2Circular(1.6f, 1.6f),
                            BloodDeep * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
                    }
                }
            }
            if (feintTimer == FeintHoldEnd) {
                //咬合拍：错位一口咬回原位
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 2 }, FeintCenter());
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 2 }, FeintCenter());
                if (viewed) {
                    ShakeViewer(1.3f);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 4; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                FeintCenter() + Main.rand.NextVector2Circular(14f, 14f),
                                new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 20));
                        }
                    }
                }
            }
            if (feintTimer >= FeintTotal) {
                feintTimer = -1;
            }
        }

        private Vector2 FeintCenter() => spine[(FeintZoneStart + FeintZoneEnd) / 2];

        /// <summary>分裂错位的绘制偏移：相邻节反向横错，开→持→咬合弹回；只改画不改碰撞</summary>
        private Vector2 FeintOffset(int i) {
            if (feintTimer < 0 || i < FeintZoneStart || i > FeintZoneEnd) {
                return Vector2.Zero;
            }
            float env;
            int t = feintTimer;
            if (t <= FeintOpenEnd) {
                float e = t / (float)FeintOpenEnd;
                env = 1f - (1f - e) * (1f - e);
            }
            else if (t <= FeintHoldEnd) {
                env = 1f + MathF.Sin((t - FeintOpenEnd) * 0.9f + Seed) * 0.06f;
            }
            else if (t <= FeintSnapEnd) {
                float e = (t - FeintHoldEnd) / (float)(FeintSnapEnd - FeintHoldEnd);
                //poly(3) 咬合 + 轻微过咬
                env = (1f - e * e * e) * (1f - e) - 0.12f * MathF.Sin(e * MathHelper.Pi);
            }
            else {
                env = 0f;
            }
            float zone = MathF.Sin(MathHelper.Pi * (i - FeintZoneStart) / (FeintZoneEnd - FeintZoneStart));
            float side = i % 2 == 0 ? 1f : -1f;
            //segRot 是行进方向 +PiOver2，其方向向量天然垂直于链轴
            Vector2 perp = segRot[i].ToRotationVector2();
            return perp * (side * zone * 15f * env);
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1600f) {
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

        private Vector2 MouthPos()
            => spine[0] + (segRot[0] - MathHelper.PiOver2).ToRotationVector2() * 26f;

        /// <summary>湿度驱动滴落：全身预算内错拍，刚出水/刚穿隙的节淌得最凶</summary>
        private void UpdateDrips() {
            if (Main.dedServ) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(SegCount);
                if (belowWater[i] || wetness[i] < 0.1f) {
                    continue;
                }
                if (Main.rand.NextFloat() > wetness[i] * 0.45f) {
                    continue;
                }
                budget--;
                Vector2 pos = spine[i] + Main.rand.NextVector2Circular(22f, 16f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 32), 0.3f);
            }
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 绘制 ====================

        private static int SegNpcType(int i)
            => i == 0 ? NPCID.EaterofWorldsHead
            : i == SegCount - 1 ? NPCID.EaterofWorldsTail
            : NPCID.EaterofWorldsBody;

        private static void GetSegDraw(int i, out Texture2D tex, out Rectangle frame) {
            int type = SegNpcType(i);
            Main.instance.LoadNPC(type);
            tex = TextureAssets.Npc[type].Value;
            int frames = Math.Max(1, Main.npcFrameCount[type]);
            frame = new Rectangle(0, 0, tex.Width, tex.Height / frames);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //裂隙对与出水缝：压在蟒身之下，身要从口里钻出来
            DrawRifts(sb);

            //本体：血湖材质逐节
            DrawChain(sb, lightColor);

            //辉光层：预兆水下血光 / 湿面反光 / 蓄势口光
            DrawGlowLayer(sb);

            return false;
        }

        private void DrawChain(SpriteBatch sb, Color lightColor) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uScanMode"]?.SetValue(0f);
            }

            int emergeT = State == StateEmerge ? (int)StateTimer : int.MaxValue;

            //尾→头，头压顶层
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                Vector2 pos = spine[i] + FeintOffset(i) - Main.screenPosition;
                //原版 EoW 贴图头朝上，蠕虫约定旋转直用
                float rot = segRot[i];

                Color color;
                if (shaderOk) {
                    //出水期从全血水错拍凝实：尾节比头晚醒
                    float steady = MathHelper.Clamp(0.30f + wetness[i] * 0.16f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + i * 0.8f) * 0.04f, 0f, 0.6f);
                    float segForm = steady;
                    if (emergeT != int.MaxValue) {
                        float condense = MathHelper.Clamp((emergeT - OmenEnd - i * 2f) / 40f, 0f, 1f);
                        segForm = MathHelper.Lerp(0.9f, steady, condense * condense * (3f - 2f * condense));
                    }
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 1.7f);
                    form.Parameters["uForm"]?.SetValue(segForm);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                        frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                    form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = Color.White;
                }
                else {
                    color = Color.Lerp(lightColor, BloodMain, 0.55f) * (1f - dissolve);
                }

                sb.Draw(tex, pos, frame, color, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 裂隙绘制 ====================

        private void DrawRifts(SpriteBatch sb) {
            //收集本帧要画的裂口：出水横缝 + 攻击竖隙对
            Span<(Vector2 center, Vector2 longAxis, float halfLen, float open, float drip)> rifts =
                stackalloc (Vector2, Vector2, float, float, float)[3];
            int count = 0;

            if (Owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                float seamOpen = State == StateEmerge ? SeamOpen(StateTimer)
                    : seamCloseTimer >= 0 && seamCloseTimer <= SeamCloseFrames
                        ? SeamOpen(EmergeTotal) : 0f;
                if (seamOpen > 0.01f && (State == StateEmerge || seamCloseTimer <= SeamCloseFrames)) {
                    rifts[count++] = (new Vector2(seamX, domain.LakeWorldY),
                        Vector2.UnitX, 150f, seamOpen, 0.35f);
                }
            }
            if (riftVisEntry > 0.01f && riftDrawEntry != Vector2.Zero) {
                rifts[count++] = (riftDrawEntry, RiftLongAxis(riftDrawDir), 128f, riftVisEntry, 1f);
            }
            if (riftVisExit > 0.01f && riftDrawExit != Vector2.Zero) {
                rifts[count++] = (riftDrawExit, RiftLongAxis(riftDrawDir), 128f, riftVisExit, 1f);
            }
            if (count == 0) {
                return;
            }

            Effect fx = EffectLoader.KikasaEaterRift?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            sb.End();

            if (fx != null && noise != null) {
                GraphicsDevice device = Main.graphics.GraphicsDevice;
                BlendState origBlend = device.BlendState;
                RasterizerState origRaster = device.RasterizerState;
                device.BlendState = BlendState.AlphaBlend;
                device.RasterizerState = RasterizerState.CullNone;

                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
                fx.Parameters["uColDark"]?.SetValue(BloodDark.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(BloodDeep.ToVector3());
                fx.Parameters["uColMain"]?.SetValue(BloodMain.ToVector3());
                fx.Parameters["uColBright"]?.SetValue(BloodBright.ToVector3());
                fx.Parameters["uColAccent"]?.SetValue(CorrodePurple.ToVector3());

                VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
                for (int r = 0; r < count; r++) {
                    (Vector2 center, Vector2 longAxis, float halfLen, float open, float drip) = rifts[r];
                    fx.Parameters["uSeed"]?.SetValue(Seed + r * 2.31f);
                    fx.Parameters["uOpen"]?.SetValue(open);
                    //低开度也要读得见：发丝缝靠透明度撑住，闭合读数交给宽度
                    fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(open * 6f, 0f, 1f));
                    fx.Parameters["uDrip"]?.SetValue(drip);

                    //quad 世界坐标（GetTransfromMatrix 自带世界→屏幕平移，绝不减 screenPosition）
                    Vector2 axisL = longAxis * halfLen;
                    Vector2 axisW = new Vector2(-longAxis.Y, longAxis.X) * 72f;
                    verts[0] = new VertexPositionColorTexture((center - axisW - axisL).ToVector3(), Color.White, new Vector2(0f, 0f));
                    verts[1] = new VertexPositionColorTexture((center + axisW - axisL).ToVector3(), Color.White, new Vector2(1f, 0f));
                    verts[2] = new VertexPositionColorTexture((center - axisW + axisL).ToVector3(), Color.White, new Vector2(0f, 1f));
                    verts[3] = new VertexPositionColorTexture((center + axisW + axisL).ToVector3(), Color.White, new Vector2(1f, 1f));
                    foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                        pass.Apply();
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
                    }
                }

                device.BlendState = origBlend;
                device.RasterizerState = origRaster;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //着色器缺失回退：暗渊拉长椭圆 + 加色缘光
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                Texture2D blob = CWRAsset.Extra_98?.Value;
                Texture2D ring = CWRAsset.DiffusionCircle?.Value;
                if (blob != null) {
                    for (int r = 0; r < count; r++) {
                        (Vector2 center, Vector2 longAxis, float halfLen, float open, float _) = rifts[r];
                        float rot = longAxis.ToRotation() + MathHelper.PiOver2;
                        Vector2 pos = center - Main.screenPosition;
                        Vector2 scaleDark = new(open * 0.7f, halfLen / blob.Height * 2.4f);
                        sb.Draw(blob, pos, null, BloodDark * (0.85f * MathHelper.Clamp(open, 0f, 1f)),
                            rot, blob.Size() * 0.5f, scaleDark, SpriteEffects.None, 0f);
                        if (ring != null) {
                            Color rim = (BloodMain with { A = 0 }) * (0.55f * MathHelper.Clamp(open, 0f, 1f));
                            sb.Draw(ring, pos, null, rim, rot, ring.Size() * 0.5f,
                                new Vector2(open * 60f / ring.Width, halfLen * 2.3f / ring.Height), SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }

        /// <summary>辉光层：出水预兆水下血光 + 湿节水膜反光 + 齐射蓄势口光</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //湿节水膜：刚出水/刚穿隙的节泛一层薄反光，读作液体不是贴纸
            Color sheen = BloodBright with { A = 0 };
            for (int i = SegCount - 1; i >= 0; i--) {
                if (wetness[i] < 0.45f || SegDissolve(i) >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                float a = 0.11f * wetness[i] * (1f - SegDissolve(i));
                sb.Draw(tex, spine[i] + FeintOffset(i) - Main.screenPosition, frame, sheen * a,
                    segRot[i], frame.Size() * 0.5f, DrawScale * 1.03f, SpriteEffects.None, 0f);
            }

            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow != null && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                //分水预兆：缝下血光自深处贴上来
                if (State == StateEmerge && StateTimer < OmenEnd && ViewedOwner) {
                    float ot = MathHelper.Clamp(StateTimer / (float)OmenEnd, 0f, 1f);
                    float ease = 1f - (1f - ot) * (1f - ot);
                    Vector2 pos = new(seamX, domain.LakeWorldY + MathHelper.Lerp(46f, 6f, ease));
                    float r = 40f + 30f * ease;
                    Color glowC = KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
                    sb.Draw(softGlow, pos - Main.screenPosition, null, glowC * (0.4f * ease), 0f,
                        softGlow.Size() * 0.5f,
                        new Vector2(r * 3.4f / softGlow.Width, r * 0.8f / softGlow.Height), SpriteEffects.None, 0f);
                }
                //齐射蓄势：口器积光，静默段骤缩，爆发前先收一口
                if (State == StateSpitVolley && (int)StateParam <= 1) {
                    float charge = (int)StateParam == 1
                        ? 0.45f
                        : MathHelper.Clamp(StateTimer / (float)CoilFrames, 0f, 1f);
                    Vector2 mouth = MouthPos();
                    float r = 8f + 18f * charge;
                    Color mix = Color.Lerp(BloodBright, CorrodePurple, 0.4f) with { A = 0 };
                    sb.Draw(softGlow, mouth - Main.screenPosition, null, mix * (0.5f * charge), 0f,
                        softGlow.Size() * 0.5f, new Vector2(r * 2f / softGlow.Width), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //直贯冲撞的穿体溅血，掺一缕蚀紫
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Projectile.velocity * 0.22f + Main.rand.NextVector2Circular(2.8f, 2.8f),
                    Main.rand.NextBool(4) ? CorrodePurple : BloodMain * 0.6f,
                    Main.rand.NextFloat(0.45f, 0.75f))?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
            if (ViewedOwner) {
                ShakeViewer(2f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spineInit) {
                return;
            }
            //谢幕残珠沿链散
            for (int i = 0; i < SegCount; i += 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[i] + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(spine[SegCount / 2],
                new Vector2(0f, -0.2f), MistBlood * 0.7f, Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
