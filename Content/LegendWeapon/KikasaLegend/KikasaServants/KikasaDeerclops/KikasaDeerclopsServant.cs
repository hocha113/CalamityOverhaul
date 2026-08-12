using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDeerclops
{
    /// <summary>
    /// 鬼奴·湖水版鹿角怪。雾行僵蹄，十六鬼奴里唯一在湖面上行走的：
    /// 蹄子踏水而行、步步溅圈，转身笨重（先停、顿一拍、再挪），跟不上时跳步趔趄。
    /// 全员唯一主用 CoolTint 冷端——血雾被冻成灰蓝调，常驻贴身冷雾罩，暖血只做伤口点缀。
    /// 出水演出：湖底闷步预兆→一只蹄子先踏出水面→整个身躯拖着湖水站起→落定披雾。
    /// 攻击一为跺脚血冰刺列（从自己脚下出发、沿水面行进的方向性序列，节奏渐快间距渐大），
    /// 攻击二为吼声冲击环（仰头蓄力→俯身怒吼推开血雾）接冰血雹（重力弹落水成串涟漪）。
    /// 联机同克眼契约：转场规则确定性、owner 盖 netUpdate 章，
    /// 节拍闩防快照回卷，子弹幕只在 owner 端生成且 spawn 参数完整，生命线只有 owner 判。
    /// 帧表对齐 TML 源码 DrawNPCDirect_Deerclops：贴图 5×5 网格按列主序索引，
    /// 0 站立 1 腾空 2..11 行走 12..17 跺脚 18 仰首 19..24 吼叫，锚点在脚底
    /// </summary>
    internal class KikasaDeerclopsServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>接触/跺脚基伤（召唤加成前）</summary>
        internal const int ContactDamage = 530;

        /// <summary>血冰刺基伤（召唤加成前），刺列弹幕消费</summary>
        internal const int SpikeDamage = 290;

        /// <summary>冰血雹基伤（召唤加成前），雹弹幕消费</summary>
        internal const int HailDamage = 290;

        //==================== 冷端配色（家族 CoolTint 冷端常量直取，不发明新色）====================

        internal static readonly Color FrostDark = new(38, 48, 52);
        internal static readonly Color FrostDeep = new(84, 104, 110);
        internal static readonly Color FrostMain = new(126, 158, 164);
        internal static readonly Color FrostBright = new(176, 200, 204);
        internal static readonly Color FrostMist = new(52, 62, 66);

        /// <summary>伤口暖血点缀：常态暖红，域入雨相随全场冷化</summary>
        internal static Color WoundBlood => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateLurch = 2;
        private const int StateStomp = 3;
        private const int StateRoar = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：出水/待机存朝向，跺脚存刺列方向，趔趄存落地相位</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：闷步预兆→蹄子先踏出→整躯拖水站起→落定披雾
        private const int OmenEnd = 24;
        private const int HoofLiftEnd = 34;
        private const int HoofPlantTick = 38;
        private const int BodySurgeTick = 48;
        private const int SettleTick = 100;
        private const int EmergeTotal = 124;

        //跺脚：刹步→抬蹄蓄力（72% 后静默）→重跺→刺列逐节喷发→收蹄
        private const int StompBrakeEnd = 8;
        private const int StompSlamTick = 40;
        private const int StompDamageEnd = 56;
        private const int StompRecoverStart = 92;
        private const int StompEnd = 112;
        private const int SpikeCount = 12;

        //吼环：刹步→仰头蓄力吸雾→静默→怒吼冲击环→冰血雹→收势
        private const int RoarBrakeEnd = 8;
        private const int RoarChargeEnd = 56;
        private const int RoarTick = 64;
        private const int RingSpan = 36;
        private const int HailStart = 70;
        private const int HailEnd = 106;
        private const int RoarEnd = 126;

        //跳步趔趄：蹲身→蹬水腾空→落地趔趄
        private const int LurchCrouchEnd = 10;
        private const int LurchStaggerFrames = 18;

        private const int DissolveFrames = 52;

        //行走
        private const float MaxWalk = 6.4f;
        private const int TurnPauseFrames = 12;
        private const float WalkCycle = 150f;

        /// <summary>刺列喷发节拍：间隔渐短——第一根迟疑，越到后面越急</summary>
        private static readonly int[] SpikeTicks = { 42, 46, 50, 54, 57, 60, 63, 65, 67, 69, 71, 73 };

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameIndex;
        private float walkCounter;
        private int lastWalkFrame = -1;
        private int facing;
        private int turnHold;
        private int lurchCharge;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private int shroudTick;
        //出水演出闩
        private bool hoofPlanted;
        private bool breachDone;
        private bool settleDone;
        private bool settleStep2Done;
        //攻击节拍闩
        private bool slamDone;
        private int lastSpikeFired = -1;
        private bool roarDone;
        private int lastHailBatch = -1;
        private bool inhaleStarted;
        private bool lurchLaunched;
        private bool lurchLanded;
        //溶解闩
        private bool dissolveSplashed;
        private bool dissolveUnraveled;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        private const float DrawScale = 0.9f;

        /// <summary>脚底位置：行走者的一切演出都从蹄下出发</summary>
        private Vector2 Feet => Projectile.Bottom;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（蹄踏点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            //身位略退于蹄踏点，前蹄先出、身躯随后爬上来
            Vector2 spawn = new(emergeAt.X - dir * 24f, emergeAt.Y + 96f);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"), spawn, Vector2.Zero,
                ModContent.ProjectileType<KikasaDeerclopsServant>(), damage, 9f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //身躯高出 hitbox 一截，吼环半径近四百，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 144;
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

        /// <summary>接触伤害只开在重跺窗口，与可见的踩落严格对齐</summary>
        public override bool? CanDamage() {
            if (State != StateStomp) {
                return false;
            }
            int t = (int)StateTimer;
            return t >= StompSlamTick && t <= StompDamageEnd ? null : false;
        }

        /// <summary>跺脚命中区：前蹄落点一圈，不吃整个身板</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (State != StateStomp) {
                return false;
            }
            int dir = StompDir;
            Vector2 foot = Feet + new Vector2(dir * 60f, 0f);
            Rectangle zone = new((int)(foot.X - 105f), (int)(foot.Y - 130f), 210, 145);
            return zone.Intersects(targetHitbox);
        }

        public override bool? CanCutTiles() => false;

        /// <summary>跺脚方向（StateParam 符号，转场时定死）</summary>
        private int StompDir => StateParam >= 0f ? 1 : -1;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //蹄子还没露头就要收场：什么都没出水，不演谢幕
            if (State == StateEmerge && StateTimer < OmenEnd + 2) {
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

            //生命线：湖塌/收域/主人死亡 → 溶解回湖。只有 owner 裁决——
            //服务器没有领域状态（恒 Closed 是既定契约），别处判会当场误杀
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //接触伤随召唤加成逐帧刷新，命中在 owner 端结算
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);

            if (facing == 0) {
                facing = Projectile.ai[2] >= 0f ? 1 : -1;
            }

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场的节拍（跺脚重拍、吼声、落地趔趄）
            if (State != lastSeenState) {
                lastSeenState = State;
                slamDone = false;
                lastSpikeFired = -1;
                roarDone = false;
                lastHailBatch = -1;
                inhaleStarted = false;
                lurchLaunched = false;
                lurchLanded = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                    dissolveUnraveled = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateLurch: UpdateLurch(domain, authority); break;
                case StateStomp: UpdateStomp(owner, domain, authority); break;
                case StateRoar: UpdateRoar(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            UpdateShroud();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = CurrentAlpha() * 0.5f;
            if (glow > 0.02f) {
                //冷光：灰蓝底、微微偏青
                Lighting.AddLight(Projectile.Center, 0.16f * glow, 0.24f * glow, 0.26f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>蹄子锁在湖面上：行走者不悬浮，脚底始终贴水线</summary>
        private void KeepFeetOnLake(float lakeY) {
            Projectile.velocity.Y = 0f;
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
        }

        //==================== 出水：蹄先踏，身后拖 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            int dir = facing;
            float hoofX = Projectile.Center.X + dir * 58f;
            Projectile.velocity = Vector2.Zero;

            if (t < OmenEnd) {
                //湖底闷步走近：涟漪一步一圈从远处逼来，水下冷光渐浮
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY + 176f);
                if (viewed && t % 6 == 2) {
                    float converge = 1f - t / (float)OmenEnd;
                    KikasaDomainDeco.RippleAt(
                        new Vector2(hoofX - dir * converge * 120f, lakeY),
                        0.35f + (1f - converge) * 0.5f);
                }
                //三记越来越近的水下闷蹄
                if (t == 6 || t == 14 || t == 21) {
                    float near = t / 21f;
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with {
                        Volume = 0.3f + near * 0.25f,
                        Pitch = -0.75f + near * 0.2f,
                        MaxInstances = 2
                    }, new Vector2(hoofX, lakeY));
                }
                return;
            }

            //蹄踏拍：一只蹄子先从湖里踏出来，踩在水面上
            if (!hoofPlanted && t >= HoofPlantTick) {
                hoofPlanted = true;
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.9f, Pitch = -0.15f, MaxInstances = 2 },
                    new Vector2(hoofX, lakeY));
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 },
                    new Vector2(hoofX, lakeY));
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(hoofX, lakeY), 1.2f);
                    KikasaDomainDeco.FootSplash(new Vector2(hoofX, lakeY), 1.3f, 0f);
                    ShakeViewer(2f);
                }
            }

            //蹄面滴水：抬起与踏定期间壳面淌珠
            if (!Main.dedServ && t >= OmenEnd + 4 && t < BodySurgeTick && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    new Vector2(hoofX + Main.rand.NextFloat(-16f, 16f), HoofBottomY(lakeY) - Main.rand.NextFloat(4f, 26f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.8f, 3f)),
                    FrostMain * Main.rand.NextFloat(0.35f, 0.55f),
                    Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(10, 18), 0f);
            }

            //破水拍：整个身躯拖着湖水站起来
            if (!breachDone && t >= BodySurgeTick) {
                breachDone = true;
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.65f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            if (t >= BodySurgeTick) {
                //拖身站起：指数衰减，前猛后缓——湖水拽着不肯放
                float rise = 176f * MathF.Pow(0.94f, t - BodySurgeTick);
                if (t >= SettleTick) {
                    rise = 0f;
                }
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY + rise);

                //身上的湖水成帘往下淌，落点连环小涟漪
                if (!Main.dedServ && t < SettleTick && t % 2 == 0) {
                    Vector2 dropPos = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-42f, 42f), Main.rand.NextFloat(-30f, 40f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.4f, 3.8f)),
                        FrostMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
                if (viewed && t < SettleTick && t % 4 == 1) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-40f, 40f), lakeY), 0.4f);
                }
            }

            //落定拍：全身重量交给双蹄，随后把冬天披上身
            if (!settleDone && t >= SettleTick) {
                settleDone = true;
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.85f, Pitch = -0.25f, MaxInstances = 2 }, Feet);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 1 }, Feet);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(Feet + new Vector2(dir * 30f, 0f), 1.4f);
                    KikasaDomainDeco.RippleAt(Feet - new Vector2(dir * 26f, 0f), 1.0f);
                    KikasaDomainDeco.SplashAt(Feet, 8);
                    ShakeViewer(3f);
                }
                //冷雾自四周收拢披上身——雾行僵蹄的觉醒不是亮起，是罩进雾里
                if (!Main.dedServ) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 off = (MathHelper.TwoPi * i / 6f + Seed).ToRotationVector2() * Main.rand.NextFloat(90f, 130f);
                        PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center + off,
                            -off * 0.016f, FrostMist * 0.85f, Main.rand.NextFloat(0.7f, 1f))
                            ?.Configure(Main.rand.Next(50, 80));
                    }
                }
            }
            if (!settleStep2Done && t >= SettleTick + 7) {
                //第二只蹄补一小步，站稳
                settleStep2Done = true;
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.55f, Pitch = -0.05f, MaxInstances = 2 },
                    Feet - new Vector2(dir * 24f, 0f));
                if (viewed) {
                    KikasaDomainDeco.RippleAt(Feet - new Vector2(dir * 24f, 0f), 0.6f);
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

        /// <summary>蹄踏演出期的蹄底世界 Y：升出→悬停→踏定</summary>
        private float HoofBottomY(float lakeY) {
            int t = (int)StateTimer;
            if (t < OmenEnd) {
                return lakeY + 76f;
            }
            if (t < HoofLiftEnd) {
                float u = (t - OmenEnd) / (float)(HoofLiftEnd - OmenEnd - 1);
                float ease = 1f - (1f - u) * (1f - u);
                return MathHelper.Lerp(lakeY + 76f, lakeY - 12f, MathHelper.Clamp(ease, 0f, 1f));
            }
            if (t < HoofPlantTick) {
                //悬停一拍：蹄尖离水一寸，滴水
                return lakeY - 12f + MathF.Sin((t - HoofLiftEnd) * 0.8f + Seed) * 1.5f;
            }
            return lakeY;
        }

        /// <summary>破水浪冠：拖着整面湖水站起来的量级，冷端水色</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 13);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 13);

            //浪冠：扇形冷水珠向外上抛
            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 23f);
                float speed = Main.rand.NextFloat(3.2f, 7.6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(4) ? FrostDeep : FrostMain,
                    Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(22, 36));
            }
            //水柱束：近垂直高抛，回落自然成雨
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13f)),
                    FrostMain * 0.9f, Main.rand.NextFloat(0.55f, 0.9f))
                    ?.Configure(Main.rand.Next(34, 50));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    FrostMist * 0.85f, Main.rand.NextFloat(0.75f, 1.05f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, FrostDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.36f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.75f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        //==================== 跟随：踏水而行 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            KeepFeetOnLake(lakeY);
            int target = FindTarget(owner);
            float feetX = Feet.X;

            //走位目标：有猎物就站进跺脚射程，闲着跟在主人身侧
            float desiredX;
            if (target >= 0) {
                NPC npc = Main.npc[target];
                float side = MathF.Sign(feetX - npc.Center.X);
                if (side == 0f) {
                    side = -facing;
                }
                desiredX = npc.Center.X + side * 250f;
            }
            else {
                desiredX = owner.Center.X - owner.direction * 150f;
            }
            float dx = desiredX - feetX;

            //跟丢硬贴回，别在半个地图外淌雾
            if (MathF.Abs(dx) > 2600f) {
                Projectile.Bottom = new Vector2(owner.Center.X - owner.direction * 240f, lakeY);
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }

            float desiredVx = MathF.Abs(dx) < 16f ? 0f : MathHelper.Clamp(dx * 0.05f, -MaxWalk, MaxWalk);
            int wantDir = desiredVx > 0.3f ? 1 : desiredVx < -0.3f ? -1 : 0;
            if (wantDir == 0 && target >= 0) {
                //站定时也要把脸转向猎物，转身同样要走"先停再挪"
                wantDir = MathF.Sign(Main.npc[target].Center.X - feetX) >= 0 ? 1 : -1;
            }

            if (wantDir != 0 && wantDir != facing) {
                //转身笨重：先刹住，顿一拍，再挪
                Projectile.velocity.X *= 0.8f;
                if (MathF.Abs(Projectile.velocity.X) < 0.35f && ++turnHold >= TurnPauseFrames) {
                    facing = wantDir;
                    turnHold = 0;
                }
            }
            else {
                turnHold = 0;
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, desiredVx, 0.05f);
            }

            //跟不上就攒一次跳步趔趄：长程全速仍差半里地，才肯把僵蹄抬离水面
            if (MathF.Abs(dx) > 760f && MathF.Abs(Projectile.velocity.X) > MaxWalk * 0.85f) {
                lurchCharge++;
            }
            else {
                lurchCharge = Math.Max(0, lurchCharge - 2);
            }
            if (lurchCharge >= 70 && StateTimer > 40) {
                lurchCharge = 0;
                State = StateLurch;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
                return;
            }

            //出手裁决：跺脚要贴面且顺脸，吼环看直线距离；两可时交替
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                NPC npc = Main.npc[target];
                int toDir = MathF.Sign(npc.Center.X - feetX) >= 0 ? 1 : -1;
                bool stompOk = MathF.Abs(npc.Center.X - feetX) < 620f
                    && npc.Center.Y > lakeY - 460f && npc.Center.Y < lakeY + 80f
                    && toDir == facing;
                bool roarOk = Vector2.Distance(npc.Center, Projectile.Center) < 980f;
                if (!stompOk && !roarOk) {
                    return;
                }
                attackIndex++;
                bool useStomp = stompOk && (!roarOk || attackIndex % 2 == 1);
                State = useStomp ? StateStomp : StateRoar;
                StateTimer = 0;
                StateParam = useStomp ? toDir : 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 跳步趔趄 ====================

        private void UpdateLurch(KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if ((int)StateParam == 0) {
                if (t < LurchCrouchEnd) {
                    //蹲身蓄力：僵蹄压水，涟漪先泄底
                    Projectile.velocity.X *= 0.85f;
                    KeepFeetOnLake(lakeY);
                    if (viewed && t == LurchCrouchEnd - 3) {
                        KikasaDomainDeco.RippleAt(Feet, 0.7f);
                    }
                    return;
                }
                if (!lurchLaunched) {
                    //蹬水腾空：一帧定速，不做斜坡
                    lurchLaunched = true;
                    Projectile.velocity = new Vector2(facing * 10.5f, -8.2f);
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.8f, Pitch = 0.05f, MaxInstances = 2 }, Feet);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.15f, MaxInstances = 2 }, Feet);
                    if (viewed) {
                        KikasaDomainDeco.FootSplash(Feet, 1.7f, Projectile.velocity.X);
                        KikasaDomainDeco.RippleAt(Feet, 1.2f);
                        ShakeViewer(1.5f);
                    }
                }
                //腾空：重量很快收回——这不是跳跃见长的怪，是一次不体面的赶路
                Projectile.velocity.Y += 0.46f;
                if (!Main.dedServ && t % 3 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center + Main.rand.NextVector2Circular(30f, 40f),
                        -Projectile.velocity * 0.06f, FrostMist * 0.6f, Main.rand.NextFloat(0.5f, 0.7f))
                        ?.Configure(Main.rand.Next(26, 40));
                }
                if (t > 14 && Projectile.Bottom.Y >= lakeY) {
                    //落地趔趄拍：双蹄砸水，身子前抢半步才稳住
                    StateParam = 1;
                    StateTimer = 0;
                    KeepFeetOnLake(lakeY);
                    lurchLanded = true;
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.95f, Pitch = -0.2f, MaxInstances = 2 }, Feet);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.75f, Pitch = -0.35f, MaxInstances = 2 }, Feet);
                    if (viewed) {
                        KikasaDomainDeco.SplashAt(Feet, 8);
                        KikasaDomainDeco.RippleAt(Feet, 1.5f);
                        KikasaDomainDeco.RippleAt(Feet + new Vector2(facing * 34f, 0f), 0.7f);
                        ShakeViewer(2.4f);
                    }
                    Projectile.netUpdate = authority;
                    return;
                }
                //安全出口：绝不允许挂在半空
                if (t > 150) {
                    EndAttack(authority, 20);
                }
                return;
            }

            //趔趄收势：硬刹 + 补一小步
            Projectile.velocity.X *= 0.78f;
            KeepFeetOnLake(lakeY);
            if (t == 6 && lurchLanded) {
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 2 },
                    Feet - new Vector2(facing * 20f, 0f));
                if (viewed) {
                    KikasaDomainDeco.RippleAt(Feet - new Vector2(facing * 20f, 0f), 0.55f);
                }
            }
            if (t >= LurchStaggerFrames) {
                EndAttack(authority, 20);
            }
        }

        //==================== 跺脚血冰刺列 ====================

        private void UpdateStomp(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int dir = StompDir;
            bool viewed = ViewedOwner;
            KeepFeetOnLake(lakeY);
            facing = dir;

            if (t <= StompBrakeEnd) {
                Projectile.velocity.X *= 0.7f;
                return;
            }
            Projectile.velocity.X *= 0.85f;

            if (t < StompSlamTick) {
                //抬蹄蓄力：身体后坐，冷雾往抬起的蹄上收拢；72% 后静默——踩落前吸气
                float u = (t - StompBrakeEnd) / (float)(StompSlamTick - StompBrakeEnd);
                if (t == StompBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 2 }, Feet);
                }
                if (!Main.dedServ && u < 0.72f && t % 3 == 1) {
                    Vector2 hoof = RaisedHoofPos();
                    Vector2 from = hoof + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (hoof - from) * 0.14f,
                        FrostBright * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(9, 0f);
                }
                if (viewed && t % 8 == 3) {
                    ShakeViewer(0.5f + u * 0.8f);
                }
                return;
            }

            if (!slamDone) {
                //重跺拍：蹄下压一帧到位，震屏向下砸
                slamDone = true;
                Vector2 foot = Feet + new Vector2(dir * 44f, 0f);
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 1f, Pitch = -0.2f, MaxInstances = 2 }, foot);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 1 }, foot);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.3f, MaxInstances = 2 }, foot);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(foot, 2.2f);
                    KikasaDomainDeco.RippleAt(foot + new Vector2(dir * 40f, 0f), 0.9f);
                    KikasaDomainDeco.SplashAt(foot, 12);
                    ShakeViewer(5f);
                }
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_DWave>(foot, Vector2.Zero, FrostBright, 0.08f)
                        ?.Configure(new Vector2(0.45f, 1f), -MathHelper.PiOver2, 0.3f, 9);
                }
            }

            //刺列逐节喷发：从自己脚下出发的方向性行进序列，
            //一根接一根从水里炸出，节奏渐快、间距渐大、个头渐高
            if (authority && lastSpikeFired < SpikeCount - 1) {
                int next = lastSpikeFired + 1;
                if (t >= SpikeTicks[next]) {
                    lastSpikeFired = next;
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SpikeDamage);
                    Vector2 basePos = new(Feet.X + dir * (44f + SpikeDistance(next)), lakeY);
                    Vector2 tilt = (-Vector2.UnitY).RotatedBy(dir * (0.06f + next * 0.03f));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), basePos, tilt,
                        ModContent.ProjectileType<KikasaDeerclopsBloodSpike>(), damage, 4f,
                        Projectile.owner, 0.78f + next * 0.05f);
                }
            }
            else if (!authority && lastSpikeFired < SpikeCount - 1) {
                //远端只推进本地闩，弹幕本体由同步包送达
                int next = lastSpikeFired + 1;
                if (t >= SpikeTicks[next]) {
                    lastSpikeFired = next;
                }
            }

            if (t >= StompEnd) {
                EndAttack(authority, 130);
            }
        }

        /// <summary>刺列第 i 根离前蹄的距离：等比放大的间距，越远越疏</summary>
        private static float SpikeDistance(int i) {
            float d = 52f;
            for (int k = 0; k < i; k++) {
                d += 34f * MathF.Pow(1.09f, k);
            }
            return d;
        }

        /// <summary>抬起的前蹄位置（蓄力汇聚锚）</summary>
        private Vector2 RaisedHoofPos() => Feet + new Vector2(StompDir * 34f, -66f);

        //==================== 吼声冲击环 + 冰血雹 ====================

        private void UpdateRoar(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            KeepFeetOnLake(lakeY);

            if (t <= RoarBrakeEnd) {
                Projectile.velocity.X *= 0.7f;
                return;
            }
            Projectile.velocity.X *= 0.85f;

            if (t < RoarTick) {
                //仰头蓄力：把周身冷雾往胸口吸，低鸣渐强；72% 后静默
                float u = MathHelper.Clamp((t - RoarBrakeEnd) / (float)(RoarChargeEnd - RoarBrakeEnd), 0f, 1f);
                if (!inhaleStarted) {
                    inhaleStarted = true;
                    SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.3f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && u < 0.72f && t % 4 == 1) {
                    Vector2 chest = ChestPos();
                    Vector2 from = chest + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(from,
                        (chest - from) * 0.045f, FrostMist * 0.7f, Main.rand.NextFloat(0.45f, 0.65f))
                        ?.Configure(Main.rand.Next(20, 32));
                }
                if (viewed && t % 7 == 2) {
                    ShakeViewer(0.6f + 1.6f * u * u);
                }
                return;
            }

            if (!roarDone) {
                //怒吼拍：俯身开口，一圈可见的冲击环把血雾推开
                roarDone = true;
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.95f, Pitch = -0.05f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    ShakeViewer(6f);
                }
                //贴身雾罩被吼声整层掀出去
                if (!Main.dedServ) {
                    for (int i = 0; i < 7; i++) {
                        Vector2 out0 = (MathHelper.TwoPi * i / 7f + Seed * 2f).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_GhostRainMist>(ChestPos() + out0 * 26f,
                            out0 * Main.rand.NextFloat(2.2f, 3.2f), FrostMist * 0.8f, Main.rand.NextFloat(0.6f, 0.9f))
                            ?.Configure(Main.rand.Next(40, 60));
                    }
                    PRTLoader.NewParticle<PRT_DWave>(ChestPos(), Vector2.Zero, FrostBright, 0.12f)
                        ?.Configure(new Vector2(1f, 1f), 0f, 0.5f, 12);
                }
            }

            //冲击环的水面脚印：环沿掠过处，湖面双向荡开成串涟漪
            if (viewed && t > RoarTick && t % 3 == 0) {
                float radius = RoarRingRadius();
                if (radius > 40f && radius < 372f) {
                    KikasaDomainDeco.RippleAt(new Vector2(Feet.X + radius, lakeY), 0.55f);
                    KikasaDomainDeco.RippleAt(new Vector2(Feet.X - radius, lakeY), 0.55f);
                }
            }

            //冰血雹：owner 分批投下，重力弹自天而落
            if (t >= HailStart && t <= HailEnd && (t - HailStart) % 3 == 0) {
                int batch = (t - HailStart) / 3;
                if (batch > lastHailBatch) {
                    lastHailBatch = batch;
                    if (authority) {
                        int target = FindTarget(owner);
                        float aimX = target >= 0
                            ? Main.npc[target].Center.X + Main.npc[target].velocity.X * 14f
                            : Feet.X + facing * 240f;
                        int count = Main.rand.NextBool(3) ? 2 : 1;
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(HailDamage);
                        for (int i = 0; i < count; i++) {
                            Vector2 spawn = new(aimX + Main.rand.NextFloat(-280f, 280f),
                                lakeY - 520f - Main.rand.NextFloat(60f));
                            Vector2 vel = new(Main.rand.NextFloat(-0.9f, 0.9f), 3.4f + Main.rand.NextFloat(1.8f));
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, vel,
                                ModContent.ProjectileType<KikasaDeerclopsHail>(), damage, 2f, Projectile.owner);
                        }
                    }
                }
            }

            if (t >= RoarEnd) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>冲击环半径：怒吼帧起 easeOutCubic 扩到 380</summary>
        private float RoarRingRadius() {
            float u = MathHelper.Clamp((StateTimer - RoarTick) / (float)RingSpan, 0f, 1f);
            return 380f * (1f - MathF.Pow(1f - u, 3f));
        }

        /// <summary>胸口位置（吼环圆心/吸气锚）</summary>
        private Vector2 ChestPos() => Feet + new Vector2(facing * 14f, -118f);

        /// <summary>独眼位置（觉醒冷光）</summary>
        private Vector2 EyePos() => Feet + new Vector2(facing * 16f, -170f);

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解回湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;
            Projectile.velocity = Vector2.Zero;

            if (!dissolveUnraveled) {
                //雾罩先散：披着的冬天一层层揭走
                dissolveUnraveled = true;
                SoundEngine.PlaySound(SoundID.DeerclopsDeath with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 out0 = (MathHelper.TwoPi * i / 4f + Seed).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center + out0 * 30f,
                            out0 * 1.1f, FrostMist * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                            ?.Configure(Main.rand.Next(40, 66));
                    }
                }
            }

            if (lakeAlive) {
                //从当前站位缓缓沉进湖里：越沉越快，站着没入——行走者的谢幕不漂浮
                //（趔趄半空被遣返也从半空开始坠，不做硬贴）
                Projectile.position.Y += MathF.Min(0.16f * t, 5.5f);

                //身体跨在水线上时，身侧连环小涟漪
                float bottomY = Projectile.Bottom.Y;
                if (ViewedOwner && t % 3 == 1 && bottomY > lakeY + 4f && bottomY < lakeY + 250f) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-34f, 34f), lakeY), 0.4f);
                }
                //胸口过水线拍（一次）
                if (!dissolveSplashed && Projectile.Center.Y >= lakeY) {
                    dissolveSplashed = true;
                    StateParam = 1f;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 }, new Vector2(Projectile.Center.X, lakeY));
                    if (ViewedOwner) {
                        Vector2 hit = new(Projectile.Center.X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 10);
                        KikasaDomainDeco.RippleAt(hit, 1.4f);
                        ShakeViewer(1.8f);
                    }
                }
            }

            //边沉边化冷珠，偶带一缕伤口暖血
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(36f, 56f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    (Main.rand.NextBool(5) ? WoundBlood : FrostMain) * 0.55f,
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 22));
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
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
            float bestDist = 1050f;
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

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 帧动画与常驻雾罩 ====================

        private void UpdateFrames() {
            if (State == StateFollow) {
                float avx = MathF.Abs(Projectile.velocity.X);
                if (avx > 0.4f) {
                    //行走循环 2..11 由速度驱动（原版 15 计数一帧），步频慢是身份
                    walkCounter += avx;
                    if (walkCounter >= WalkCycle) {
                        walkCounter -= WalkCycle;
                    }
                    int wf = 2 + (int)(walkCounter / 15f) % 10;
                    if (wf != lastWalkFrame) {
                        lastWalkFrame = wf;
                        //原版落蹄帧 4/9：每一步都是演出
                        if ((wf == 4 || wf == 9) && avx > 1f) {
                            FootfallBeat(wf == 4);
                        }
                    }
                    frameIndex = wf;
                    return;
                }
                frameIndex = 0;
                lastWalkFrame = -1;
                return;
            }
            frameIndex = GetPoseFrame();
        }

        /// <summary>落蹄拍：涟漪一圈 + 踏水碎星 + 蹄声 + 一缕蹄边冷雾</summary>
        private void FootfallBeat(bool frontFoot) {
            Vector2 foot = Feet + new Vector2(facing * (frontFoot ? 26f : -18f), 0f);
            //常态行走步声压低一档，把响头留给重跺
            SoundEngine.PlaySound(SoundID.DeerclopsStep with {
                Volume = 0.55f,
                Pitch = frontFoot ? -0.1f : -0.2f,
                MaxInstances = 3
            }, foot);
            if (ViewedOwner) {
                KikasaDomainDeco.RippleAt(foot, 0.55f);
                KikasaDomainDeco.FootSplash(foot, 0.9f, Projectile.velocity.X);
                ShakeViewer(0.3f);
            }
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(foot + new Vector2(0f, -8f),
                    new Vector2(-Projectile.velocity.X * 0.05f, -0.2f),
                    FrostMist * 0.55f, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(26, 40));
            }
        }

        /// <summary>非行走姿态帧：对齐原版帧表（12..17 跺脚，19..24 吼叫，18 仰首）</summary>
        private int GetPoseFrame() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    return t < SettleTick ? 1 : 0;
                case StateLurch:
                    if ((int)StateParam == 1) {
                        return 0;
                    }
                    return t < LurchCrouchEnd ? 0 : 1;
                case StateStomp: {
                    if (t <= StompBrakeEnd) {
                        return 0;
                    }
                    if (t < StompSlamTick) {
                        //抬蹄 12，随后 13/14 交替打颤——僵蹄悬着也在抖
                        return t < StompBrakeEnd + 6 ? 12 : 13 + (t - StompBrakeEnd - 6) / 5 % 2;
                    }
                    if (t < StompSlamTick + 4) {
                        return 15;
                    }
                    if (t < StompSlamTick + 8) {
                        return 16;
                    }
                    if (t < StompRecoverStart) {
                        return 17;
                    }
                    return t < StompRecoverStart + 8 ? 12 : 0;
                }
                case StateRoar: {
                    if (t <= RoarBrakeEnd) {
                        return 0;
                    }
                    if (t < 16) {
                        return 19;
                    }
                    if (t < 24) {
                        return 20;
                    }
                    if (t < RoarTick) {
                        //仰头蓄力颤
                        return 21 + (t - 24) / 5 % 2;
                    }
                    if (t < HailEnd) {
                        //开口怒吼
                        return 23 + (t - RoarTick) / 4 % 2;
                    }
                    return t < RoarEnd - 8 ? 20 : 19;
                }
                default:
                    return 0;
            }
        }

        /// <summary>常驻贴身冷雾罩：把冬天披在身上；吼环窗口暂停（雾被推开了）</summary>
        private void UpdateShroud() {
            if (Main.dedServ || CurrentAlpha() < 0.5f || State == StateDissolve) {
                return;
            }
            if (State == StateRoar && StateTimer >= RoarTick && StateTimer < RoarTick + 46) {
                return;
            }
            if (++shroudTick % 9 != 0) {
                return;
            }
            Vector2 pos = Projectile.Center + new Vector2(
                Main.rand.NextFloat(-52f, 52f), Main.rand.NextFloat(-64f, 56f));
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.05f, 0.2f)),
                FrostMist * Main.rand.NextFloat(0.55f, 0.75f),
                Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(55, 85));
            //伤口偶发滴暖血：冻不透的地方
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 52f),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.2f)),
                    WoundBlood * 0.5f, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Main.rand.Next(18, 30), 0.26f);
            }
        }

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BodySurgeTick ? 0f : MathHelper.Clamp((t - BodySurgeTick + 1) / 3f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全冻血水 0=真身；拖身站起时自上而下凝实，常态半沉呼吸</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.40f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < BodySurgeTick
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - BodySurgeTick) / 62f, 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.35f, 0f, 1f),
                _ => steady,
            };
        }

        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= SettleTick) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - SettleTick) / 12f, 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 44f, 0f, 1f), 0.9f)
                : 0f;

        /// <summary>身体缩放：落定/重跺压缩、蹲身蓄力、吸气鼓胸；锚点在脚底，压缩即坐进腿里</summary>
        private Vector2 BodyScaleVec() {
            float sx = DrawScale, sy = DrawScale;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= SettleTick && t < SettleTick + 12) {
                sy *= 0.955f + 0.045f * SmoothStep01((t - SettleTick) / 12f);
            }
            else if (State == StateStomp && t >= StompSlamTick && t < StompSlamTick + 8) {
                sy *= 0.955f;
            }
            else if (State == StateLurch && (int)StateParam == 0 && t < LurchCrouchEnd) {
                sy *= 1f - 0.06f * (t / (float)LurchCrouchEnd);
            }
            else if (State == StateLurch && (int)StateParam == 1 && t < 10) {
                sy *= 0.94f + 0.06f * (t / 10f);
            }
            else if (State == StateRoar && t > RoarBrakeEnd && t < RoarTick) {
                float u = MathHelper.Clamp((t - RoarBrakeEnd) / (float)(RoarChargeEnd - RoarBrakeEnd), 0f, 1f);
                sx *= 1f + 0.03f * u;
            }
            return new Vector2(sx, sy);
        }

        /// <summary>身体倾角（绕脚底）：行走摇晃、后坐蓄力、仰头、趔趄回摆</summary>
        private float BodyLean() {
            int t = (int)StateTimer;
            switch (State) {
                case StateFollow when MathF.Abs(Projectile.velocity.X) > 0.4f: {
                    float phase = walkCounter / WalkCycle * MathHelper.TwoPi;
                    return MathF.Sin(phase * 2f) * 0.022f;
                }
                case StateStomp when t > StompBrakeEnd && t < StompSlamTick: {
                    float u = (t - StompBrakeEnd) / (float)(StompSlamTick - StompBrakeEnd);
                    return -StompDir * 0.05f * u * u;
                }
                case StateStomp when t >= StompSlamTick && t < StompSlamTick + 10:
                    return StompDir * 0.032f * (1f - (t - StompSlamTick) / 10f);
                case StateRoar when t > 16 && t < RoarTick: {
                    float u = MathHelper.Clamp((t - 16f) / (RoarChargeEnd - 16f), 0f, 1f);
                    return -facing * 0.07f * u;
                }
                case StateRoar when t >= RoarTick && t < RoarTick + 20:
                    return facing * 0.05f * (1f - (t - RoarTick) / 20f);
                case StateLurch when (int)StateParam == 0 && t >= LurchCrouchEnd:
                    return facing * MathF.Min(0.13f, (t - LurchCrouchEnd) * 0.012f);
                case StateLurch when (int)StateParam == 1:
                    return -facing * 0.09f * MathF.Pow(0.85f, t);
                default:
                    return 0f;
            }
        }

        /// <summary>行走起伏：两步一循环，身体随步伐上下颠</summary>
        private float WalkBobY() {
            if (State != StateFollow || MathF.Abs(Projectile.velocity.X) <= 0.4f) {
                return 0f;
            }
            float phase = walkCounter / WalkCycle * MathHelper.TwoPi;
            return -MathF.Abs(MathF.Cos(phase * 2f)) * 3.2f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.Deerclops);
            Texture2D tex = TextureAssets.Npc[NPCID.Deerclops]?.Value;
            if (tex == null) {
                return false;
            }

            //出水演出前段只画先踏出来的那只蹄；破水帧前后蹄与身交叉淡接，不许空一帧
            if (State == StateEmerge) {
                int t = (int)StateTimer;
                if (t < OmenEnd) {
                    DrawGlow();
                    return false;
                }
                if (t < BodySurgeTick + 3) {
                    DrawHoofCrop(tex);
                    if (t >= BodySurgeTick) {
                        DrawBody(tex, CurrentAlpha());
                    }
                    DrawGlow();
                    return false;
                }
            }

            float alpha = CurrentAlpha();
            if (alpha > 0.01f) {
                DrawBody(tex, alpha);
            }
            DrawGlow();
            return false;
        }

        /// <summary>
        /// 本体：对齐原版 DrawNPCDirect_Deerclops 的帧与锚点——
        /// 5×5 网格列主序、脚底锚点、蹄锚横位 106、原生朝右
        /// </summary>
        private void DrawBody(Texture2D tex, float alpha) {
            Rectangle frame = tex.Frame(5, 5, frameIndex / 5, frameIndex % 5, 2, 2);
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            origin.Y -= 4f;
            origin.X = facing == 1 ? 106f : frame.Width - 106f;
            SpriteEffects fx = facing == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Vector2 drawPos = Projectile.Bottom + new Vector2(0f, WalkBobY()) - Main.screenPosition;

            Effect form = EffectLoader.KikasaDeerclopsFrost?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                ApplyFrostShader(form, noise, tex, frame, CurrentForm(), CurrentScanMode(), CurrentDissolve(), Seed);
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                //无着色器回退：CPU 冻灰蓝染
                color = Color.Lerp(Color.White, FrostMain, 0.6f) * alpha;
            }

            sb.Draw(tex, drawPos, frame, color, BodyLean(), origin, BodyScaleVec(), fx, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>先踏出的蹄：站立帧腿部裁片走全血水态，一条水凝的僵蹄踩上湖面</summary>
        private void DrawHoofCrop(Texture2D tex) {
            Player owner = Owner;
            if (owner == null || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return;
            }
            int t = (int)StateTimer;
            float cropAlpha = MathHelper.Clamp((t - OmenEnd) / 4f, 0f, 1f);
            //破水帧后随身躯浮出淡出，交棒给本体的腿
            if (t >= BodySurgeTick) {
                cropAlpha *= MathHelper.Clamp(1f - (t - BodySurgeTick + 1) / 3f, 0f, 1f);
            }
            if (cropAlpha < 0.02f) {
                return;
            }

            //站立帧（索引 0）的腿部：中下 60% 宽 × 底 32% 高
            Rectangle cell = tex.Frame(5, 5, 0, 0, 2, 2);
            Rectangle crop = new(
                cell.X + (int)(cell.Width * 0.20f),
                cell.Y + (int)(cell.Height * 0.68f),
                (int)(cell.Width * 0.60f),
                (int)(cell.Height * 0.32f));

            float hoofX = Projectile.Center.X + facing * 58f;
            Vector2 drawPos = new Vector2(hoofX, HoofBottomY(domain.LakeWorldY)) - Main.screenPosition;
            Vector2 origin = new(crop.Width * 0.5f, crop.Height);
            SpriteEffects fx = facing == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Effect form = EffectLoader.KikasaDeerclopsFrost?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                ApplyFrostShader(form, noise, tex, crop, 1f, 0f, 0f, Seed);
                color = new Color(255, 255, 255, (byte)(cropAlpha * 255f));
            }
            else {
                color = Color.Lerp(Color.White, FrostMain, 0.75f) * cropAlpha;
            }

            sb.Draw(tex, drawPos, crop, color, 0f, origin, DrawScale, fx, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>冻血材质参数上传（本体与蹄裁片共用；刺列弹幕也走同一签名）</summary>
        internal static void ApplyFrostShader(Effect form, Texture2D noise, Texture2D tex,
            Rectangle frame, float uForm, float uScan, float uDissolve, float seed) {
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            form.Parameters["uSeed"]?.SetValue(seed);
            form.Parameters["uForm"]?.SetValue(uForm);
            form.Parameters["uDissolve"]?.SetValue(uDissolve);
            form.Parameters["uScanMode"]?.SetValue(uScan);
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            form.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>加色层：水下冷光预兆 / 抬蹄聚光 / 吼环 / 独眼冷芒</summary>
        private void DrawGlow() {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (glow == null || ring == null) {
                return;
            }
            Player owner = Owner;
            if (owner == null || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            bool begun = false;
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 rOrigin = ring.Size() * 0.5f;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            int t = (int)StateTimer;

            //预兆：湖下一团逼近的冷光——蹄声的形
            if (State == StateEmerge && t < OmenEnd) {
                float ot = MathHelper.Clamp(t / (float)OmenEnd, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                float hoofX = Projectile.Center.X + facing * 58f;
                Vector2 pos = new(hoofX - facing * (1f - ease) * 100f, domain.LakeWorldY + MathHelper.Lerp(48f, 10f, ease));
                float r = 30f + 22f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, FrostBright * (0.38f * ease), 0f,
                    gOrigin, new Vector2(r * 2.6f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
            }

            //落定觉醒：独眼里亮起一点冷芒（不是灼热，是结了霜的注视）
            if (State == StateEmerge && t >= SettleTick + 4) {
                float f = MathHelper.Clamp((t - SettleTick - 4f) / (EmergeTotal - SettleTick - 4f), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi) * 0.7f;
                if (a > 0.02f) {
                    EnsureBegin();
                    float r = 12f + 12f * f;
                    sb.Draw(glow, EyePos() - Main.screenPosition, null, FrostBright * a, 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //抬蹄蓄力：蹄底积一团冷光，72% 后只剩收紧的芯
            if (State == StateStomp && t > StompBrakeEnd && t < StompSlamTick) {
                float u = (t - StompBrakeEnd) / (float)(StompSlamTick - StompBrakeEnd);
                EnsureBegin();
                Vector2 hoof = RaisedHoofPos();
                float r = 10f + 22f * u;
                sb.Draw(glow, hoof - Main.screenPosition, null, FrostBright * (0.5f * u), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //重跺闪：蹄下一道横扁的水光
            if (State == StateStomp && t >= StompSlamTick && t < StompSlamTick + 10) {
                float k = 1f - (t - StompSlamTick) / 10f;
                EnsureBegin();
                Vector2 foot = Feet + new Vector2(StompDir * 44f, -4f);
                sb.Draw(glow, foot - Main.screenPosition, null, FrostBright * (0.55f * k), 0f,
                    gOrigin, new Vector2(150f / glow.Width * (1f + (1f - k)), 26f / glow.Height), SpriteEffects.None, 0f);
            }

            //吼声冲击环：主环 + 滞后内环，把血雾推开的可见形
            if (State == StateRoar && t >= RoarTick && t < RoarTick + RingSpan + 8) {
                float radius = RoarRingRadius();
                float u = MathHelper.Clamp((t - RoarTick) / (float)RingSpan, 0f, 1f);
                float a = MathF.Sin(MathHelper.Clamp(u, 0f, 1f) * MathHelper.Pi) * 0.5f;
                if (a > 0.02f && radius > 8f) {
                    EnsureBegin();
                    Vector2 c = ChestPos() - Main.screenPosition;
                    sb.Draw(ring, c, null, FrostBright * a, 0f, rOrigin,
                        new Vector2(radius * 2f / ring.Width, radius * 1.7f / ring.Height), SpriteEffects.None, 0f);
                    float lag = MathHelper.Clamp((u - 0.2f) / 0.8f, 0f, 1f);
                    if (lag > 0f) {
                        float r2 = radius * 0.62f;
                        sb.Draw(ring, c, null, FrostMain * (a * 0.6f), 0f, rOrigin,
                            new Vector2(r2 * 2f / ring.Width, r2 * 1.7f / ring.Height), SpriteEffects.None, 0f);
                    }
                }
            }

            //仰头蓄力：胸口吸拢的冷光，随蓄力收紧
            if (State == StateRoar && t > RoarBrakeEnd && t < RoarTick) {
                float u = MathHelper.Clamp((t - RoarBrakeEnd) / (float)(RoarChargeEnd - RoarBrakeEnd), 0f, 1f);
                EnsureBegin();
                float r = MathHelper.Lerp(46f, 18f, u);
                sb.Draw(glow, ChestPos() - Main.screenPosition, null, FrostBright * (0.4f * u), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //重跺踩中的冻碎声与冷珠（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1f, 3.4f)),
                    (Main.rand.NextBool(3) ? WoundBlood : FrostMain) * 0.6f,
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(0.2f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.65f, Pitch = -0.45f, MaxInstances = 3 }, target.Center);
            //冰声原生音量基数极低（0.1），不抬音量只调音高做冻裂质感层
            SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Pitch = -0.2f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残迹：冷珠一摊 + 雾一口，异常移除也不空场
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(34f, 50f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    (Main.rand.NextBool(6) ? WoundBlood : FrostMain) * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), FrostMist * 0.7f, Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
