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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFishron
{
    /// <summary>
    /// 鬼奴·湖水版猪龙鱼公爵。全员唯一默认活在水下的鬼奴，渊主归湖：
    /// 跟随态是水面下的鱼雷环游，玩家只看得见割开水面的背鳍与身后的航迹涟漪
    /// （身体经裁剪只画水线以上，水下只留暗影团）。出场为背鳍远来：
    /// 航迹渐近高速逼近→玩家面前全身跃出的亮相回旋（血水在空中凝成真身）→落水入巡游。
    /// 签名攻击为海豚节律破水咬（水下绕位→垂直暴起空中拧咬→立即回潜，2~3 连跳），
    /// 与毁灭者的长弹道穿体严格区分；辅以甩尾拉起的游走血龙卷（≤2 根）与
    /// 绕目标吐环的悬滞血气泡雷。溶解为最后一跃：空中炸成一蓬血雨落回湖里。
    /// 联机同克眼契约：owner 裁决转场盖 netUpdate 章，节拍闩防快照回卷，
    /// 生命线只有 owner 判，子弹幕只在 owner 端生成且 spawn 参数完整
    /// </summary>
    internal class KikasaFishronServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>破水咬接触基伤（召唤加成前）</summary>
        internal const int BiteDamage = 740;

        /// <summary>血龙卷基伤（召唤加成前），由龙卷弹幕消费</summary>
        internal const int NadoDamage = 400;

        /// <summary>血气泡基伤（召唤加成前），由气泡弹幕消费</summary>
        internal const int BubbleDamage = 400;

        internal const float DrawScale = 0.82f;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StatePorpoise = 2;
        private const int StateNado = 3;
        private const int StateBubbleRing = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>
        /// 状态内子参数。出水期编码为 方向×(1+段)：符号=来向，|值|-1=段(0 逼近/1 跃出/2 落定)；
        /// 海豚咬编码为 跳序×4+相(0 潜行/1 跃咬)；龙卷与气泡为普通相位号；
        /// 溶解为谢幕分支(0 湖亡原地化水/1 从水起跳/2 空中直接炸雨)
        /// </summary>
        private ref float StateParam => ref Projectile.ai[2];

        private float ArcDir => MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
        private int EmergePhase => (int)MathF.Abs(StateParam) - 1;

        private int JumpIndex => (int)StateParam / 4;
        private int JumpPhase => (int)StateParam % 4;

        //==================== 时序 ====================

        //背鳍远来：纯计时破水点，几何早退防冲过头
        private const int ApproachFrames = 64;
        private const int ShowLeapFrames = 52;
        private const int SettleFrames = 26;

        //海豚节律：潜行 1~2 秒→跃咬→回潜，2~3 连跳（此处 3）
        private const int PorpoiseJumps = 3;
        private const int StalkMin = 55;
        private const int StalkMax = 105;
        private const int BiteWindow = 44;
        private const int LeapTimeout = 140;
        private const float LeapGravity = 0.78f;

        //血龙卷：水下就位→贴水刻痕甩尾（两记）→回潜
        private const int NadoAlignMax = 90;
        private const int CarveFrames = 30;
        private const int NadoDiveMax = 46;

        //气泡雷环：就位→绕目标一圈吐泡→回潜
        private const int BubblePosMax = 60;
        private const int CircleFrames = 64;
        private const int BubbleCount = 9;
        private const int BubbleDiveMax = 70;
        private const float RingRadius = 185f;

        //溶解：收尾一跃→空中炸成血雨
        private const int DissolveGather = 14;
        private const int DissolveBurstT = 40;
        private const int DissolveLeapTotal = 76;
        private const int DissolveFadeTotal = 48;
        private const int DissolveAirBurstT = 12;
        private const int DissolveAirTotal = 48;

        //巡游深度：鳍尖露出水面的吃水
        private const float FinDepth = 30f;
        private const float StalkDepth = 64f;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool launchDone;
        private bool breachDone;
        private bool apexRoared;
        private int lastJumpLaunched = -1;
        private int lastJumpBitten = -1;
        private int lastWhipFired = -1;
        private int lastBubbleSpit = -1;
        private bool dissolveLaunched;
        private bool dissolveBurstDone;
        /// <summary>过水线滞回：+1 在水上 / -1 在水下，带 ±16px 死区防贴线抖动</summary>
        private int waterSide;
        /// <summary>湿度：入水拉满、出水淌干，驱动滴落与材质血水度</summary>
        private float wetness = 1f;
        private int finFxTick;
        private int sloshTick;
        private float lakeYCache;
        /// <summary>气泡环几何（各端进相首帧自建，晚进场的端从当前位置续圆）</summary>
        private bool ringInit;
        private Vector2 ringCenter;
        private float ringTheta0;
        private float ringDir = 1f;
        /// <summary>绘制姿态缓存：AI 尾拍结算，oldRot 残影与绘制共用</summary>
        private bool drawFlipLeft;
        /// <summary>朝向滞回闩：近垂直机动时横速在零附近抖，不吃小噪声防镜像闪烁</summary>
        private bool faceLeftLatch;

        //血系配色随观看域鬼雨异化冷化；渊青只做鳍缘/气泡的次要点缀层
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color FoamPale => KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        internal static Color AbyssSheen => KikasaDomain.CoolTint(new(96, 176, 150), new(120, 160, 158));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        /// <summary>裁剪水线以上的光栅态：背鳍割水面的关键器件</summary>
        private static readonly RasterizerState scissorOn = new() {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面。
        /// 起点在破水点外侧远处的水线下，背鳍自己切着水面赶过来</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);
            float away = MathF.Sign(emergeAt.X - owner.Center.X);
            if (away == 0f) {
                away = owner.direction;
            }
            //行进方向 = 朝玩家一侧；出生点越远，逼近戏越足
            float travelDir = -away;
            Vector2 spawn = new(emergeAt.X + away * 780f, emergeAt.Y + FinDepth);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"), spawn, Vector2.Zero,
                ModContent.ProjectileType<KikasaFishronServant>(), damage, 8f, owner.whoAmI,
                ai2: travelDir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //跃出与远端逼近都可能离屏，放宽绘制检查
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 76;
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

        /// <summary>接触伤害严格限定在海豚跃出段：出水上升到弧顶咬合为止，
        /// 回潜段（竖速转正走大）立即收口不伤</summary>
        public override bool? CanDamage() {
            if (State != StatePorpoise || JumpPhase != 1) {
                return false;
            }
            return StateTimer > 0 && Projectile.velocity.Y < 6f
                && Projectile.Center.Y < lakeYCache + 10f ? null : false;
        }

        /// <summary>沿身体轴线的胖线碰撞，咬合读数比方框诚实</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - axis * 40f, Projectile.Center + axis * 56f, 34f, ref _);
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
            //还没破水亮过相：鳍缩回水里就算完，不演谢幕
            if (State == StateEmerge && EmergePhase == 0) {
                Projectile.Kill();
                return;
            }
            KikasaDomainPlayer domain = Owner.GetModPlayer<KikasaDomainPlayer>();
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            State = StateDissolve;
            StateTimer = 0;
            //谢幕分支 owner 圈定，远端跟包：湖亡原地化水 / 从水起跳 / 已在空中直接炸雨
            StateParam = !lakeAlive ? 0
                : Projectile.Center.Y > domain.LakeWorldY - 120f ? 1 : 2;
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
            lakeYCache = domain.LakeWorldY;

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);

            //换场清闩：远端可能靠收包切状态，上一场残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                lastJumpLaunched = -1;
                lastJumpBitten = -1;
                lastWhipFired = -1;
                lastBubbleSpit = -1;
                ringInit = false;
                if (State == StateDissolve) {
                    dissolveLaunched = false;
                    dissolveBurstDone = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StatePorpoise: UpdatePorpoise(owner, domain, authority); break;
                case StateNado: UpdateNado(owner, domain, authority); break;
                case StateBubbleRing: UpdateBubbleRing(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateWaterCrossing(domain);
            UpdateFrames();
            UpdateDrips();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //结算绘制姿态：残影 oldRot 要吃到每帧的真实旋转
            GetDrawPose(out float drawRot, out SpriteEffects drawFlip);
            Projectile.rotation = drawRot;
            drawFlipLeft = drawFlip == SpriteEffects.FlipHorizontally;

            //水上照体、水下照面：光斑也说明它在哪
            if (!dissolveBurstDone) {
                Vector2 lampAt = Projectile.Center.Y < lakeYCache
                    ? Projectile.Center : new Vector2(Projectile.Center.X, lakeYCache);
                Lighting.AddLight(lampAt, 0.30f, 0.08f, 0.07f);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：背鳍远来 → 亮相回旋 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            float dir = ArcDir;
            bool viewed = ViewedOwner;

            if (EmergePhase == 0) {
                //逼近：背鳍切水面高速赶来，前慢后快；纯计时破水，几何早退防冲过头
                float a = MathHelper.Clamp(t / (float)ApproachFrames, 0f, 1f);
                float speed = MathHelper.Lerp(6f, 22f, MathF.Pow(a, 1.7f));
                Projectile.velocity = new Vector2(dir * speed,
                    MathHelper.Clamp(lakeY + FinDepth - Projectile.Center.Y, -2f, 2f) * 0.3f);
                FinWakeFX(domain, dense: false);

                if (viewed && (t == 16 || t == 40)) {
                    //两记闷涌：水下有大家伙在赶路
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = t == 16 ? 0.4f : 0.55f,
                        Pitch = -0.85f,
                        MaxInstances = 2
                    }, new Vector2(Projectile.Center.X, lakeY));
                    ShakeViewer(t == 16 ? 0.8f : 1.4f);
                }

                bool nearOwner = MathF.Abs(Projectile.Center.X - owner.Center.X) < 260f;
                if (t >= ApproachFrames || nearOwner && t > 18) {
                    StateParam = dir * 2f;
                    StateTimer = 0;
                    Projectile.netUpdate = Main.myPlayer == Projectile.owner;
                }
                return;
            }

            if (EmergePhase == 1) {
                if (!launchDone) {
                    //破水拍：一帧定弹道，全场最帅的一次跃出
                    launchDone = true;
                    Projectile.velocity = new Vector2(dir * 5.2f, -19f);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                    if (!breachDone) {
                        breachDone = true;
                        //迟入场的端跳过陈旧浪冠，别在半空补一朵水花
                        if (t < 12 && viewed) {
                            BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                        }
                    }
                }

                //弹道：越近弧顶重力越轻，悬拍让回旋读满
                float g = 0.62f * (0.45f + 0.55f * MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) / 19f, 0f, 1f));
                Projectile.velocity.Y += g;

                if (!apexRoared && Projectile.velocity.Y >= -1f) {
                    //弧顶觉醒拍：短吼 + 鳍尖亮相
                    apexRoared = true;
                    SoundEngine.PlaySound(SoundID.NPCHit14 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        ShakeViewer(2f);
                    }
                }

                //回落入水：过水线拍由通用过线器给，这里只管转段
                if (t > 10 && Projectile.Center.Y > lakeY + 24f || t > ShowLeapFrames + 40) {
                    StateParam = dir * 3f;
                    StateTimer = 0;
                    Projectile.netUpdate = Main.myPlayer == Projectile.owner;
                }
                return;
            }

            //落定：水下刹车弯回巡游吃水
            Projectile.velocity.X *= 0.9f;
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp(lakeY + FinDepth - Projectile.Center.Y, -6f, 6f) * 0.2f, 0.25f);
            if (t >= SettleFrames) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 55;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>亮相破水浪冠：渊主级，量级压过克眼</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.8f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(46f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(42f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-16f, 0f), 13);
            KikasaDomainDeco.SplashAt(hit + new Vector2(16f, 0f), 13);

            //浪冠扇 + 近垂直水柱束
            for (int i = 0; i < 26; i++) {
                float angle = -MathHelper.Pi * (0.1f + 0.8f * i / 25f);
                float speed = Main.rand.NextFloat(3.4f, 8.2f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-9f, 9f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13.5f)),
                    BloodMain * 0.9f, Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(34, 52));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-36f, 36f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.8f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.75f, 1.1f))
                    ?.Configure(Main.rand.Next(66, 104));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.38f, 12);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        //==================== 跟随：水下鱼雷环游 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;

            //环游锚：横向在玩家两侧来回穿越，纵向咬死鳍尖吃水 + 呼吸微沉浮
            float sweep = MathF.Sin(StateTimer * 0.011f + Seed) * 240f;
            float targetX = owner.Center.X + sweep;
            float targetY = lakeY + FinDepth + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 4f;

            float dx = targetX - Projectile.Center.X;
            if (MathF.Abs(owner.Center.X - Projectile.Center.X) > 2400f) {
                //跟丢硬贴回：直接回到玩家身侧水下
                Projectile.Center = new Vector2(owner.Center.X - owner.direction * 260f, lakeY + FinDepth);
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            //水是主场：追赶提速上限高过空中任何时刻的巡航
            float far = MathHelper.Clamp(MathF.Abs(dx) / 700f, 0f, 1f);
            float maxV = MathHelper.Lerp(9f, 24f, far);
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X,
                MathHelper.Clamp(dx * 0.06f, -maxV, maxV), 0.12f);
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp((targetY - Projectile.Center.Y) * 0.18f, -5f, 5f), 0.28f);

            FinWakeFX(domain, dense: false);

            //出手裁决：海豚咬为主拍，龙卷与气泡穿插；规则各端一致，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                NPC npc = Main.npc[target];
                bool deep = npc.Center.Y > lakeY + 70f;
                bool high = npc.Center.Y < lakeY - 440f;
                attackIndex++;
                int pick = attackIndex % 4;   //0咬 1卷 2咬 3泡
                if (deep || high) {
                    pick = 3;   //跳不到的目标交给会飞的气泡环
                }
                else if (pick == 1 && (CountNados() >= 2 || npc.Center.Y < lakeY - 300f)) {
                    pick = 0;   //龙卷满编或目标偏高时换咬
                }
                State = pick == 1 ? StateNado : pick == 3 ? StateBubbleRing : StatePorpoise;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        /// <summary>背鳍航迹：涟漪 + 踏水碎星 + 潜行闷涌。dense = 海豚潜行的骤密预告</summary>
        private void FinWakeFX(KikasaDomainPlayer domain, bool dense) {
            if (!ViewedOwner) {
                return;
            }
            float speed = MathF.Abs(Projectile.velocity.X);
            if (speed < 2.5f || Projectile.Center.Y < domain.LakeWorldY) {
                return;
            }
            Vector2 finAt = new(Projectile.Center.X + Projectile.velocity.X * 1.2f, domain.LakeWorldY);

            int interval = dense ? 2 : MathF.Abs(speed) > 14f ? 4 : 7;
            if (++finFxTick >= interval) {
                finFxTick = 0;
                //巡航圈压在行波阈值下只画环，骤密段放开让水面真的隆起来
                float scale = dense ? 0.36f + speed * 0.008f
                    : MathF.Min(0.20f + speed * 0.006f, 0.29f);
                KikasaDomainDeco.RippleAt(finAt, scale);
            }
            if (speed > 7f && (int)StateTimer % 5 == 0) {
                KikasaDomainDeco.FootSplash(finAt, speed * 0.07f, Projectile.velocity.X);
            }
            if (dense && ++sloshTick >= 26) {
                sloshTick = 0;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = -0.9f, MaxInstances = 2 }, finAt);
            }
        }

        //==================== 海豚节律破水咬 ====================

        private void UpdatePorpoise(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int jump = JumpIndex;
            int target = FindTarget(owner);

            void NextJump() {
                int next = jump + 1;
                if (next >= PorpoiseJumps) {
                    EndAttack(authority, 130);
                    return;
                }
                StateParam = next * 4;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (JumpPhase == 0) {
                //潜行绕位：水下加速咬到目标正下方，航迹骤密是唯一预告
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                float underX = npc.Center.X + npc.velocity.X * 12f;
                float dx = underX - Projectile.Center.X;
                Projectile.velocity.X = MathHelper.Clamp(
                    Projectile.velocity.X + MathF.Sign(dx) * 1.7f, -27f, 27f);
                Projectile.velocity.Y = MathHelper.Clamp(
                    (lakeY + StalkDepth - Projectile.Center.Y) * 0.14f, -6f, 6f);

                FinWakeFX(domain, dense: true);

                if (t >= StalkMin && MathF.Abs(dx) < 46f || t >= StalkMax) {
                    //就位/超时：进跃咬相（发力在新相首帧，远端靠包换场也能同拍起跳）
                    StateParam = jump * 4 + 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (lastJumpLaunched < jump) {
                //跃咬拍：一帧定弹道，仰角吃目标高度，咬在弧顶；owner 用新鲜瞄准并盖章
                lastJumpLaunched = jump;
                Vector2 aimPos = target >= 0 ? Main.npc[target].Center
                    : Projectile.Center - Vector2.UnitY * 240f;
                float hAbove = MathF.Max(lakeY - aimPos.Y, 40f);
                float vy = MathHelper.Clamp(MathF.Sqrt(2f * LeapGravity * (hAbove + 130f)), 15f, 25f);
                float vx = MathHelper.Clamp((aimPos.X - Projectile.Center.X) * 0.05f
                    + (target >= 0 ? Main.npc[target].velocity.X * 0.55f : 0f), -9f, 9f);
                Projectile.velocity = new Vector2(vx, -vy);
                Projectile.netUpdate = authority;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.5f, Pitch = 0.1f + jump * 0.06f, MaxInstances = 3 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(1.6f);
                }
            }

            //跃咬段：弹道上升→弧顶拧身一咬→立即头朝下扎回
            bool pastApex = Projectile.velocity.Y >= 0f;
            Projectile.velocity.Y += pastApex ? LeapGravity * 1.5f : LeapGravity;
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y, 27f);

            if (!pastApex && target >= 0) {
                //上升段轻微校向，别转成追踪弹
                float lead = Main.npc[target].Center.X - Projectile.Center.X;
                Projectile.velocity.X = MathHelper.Clamp(
                    Projectile.velocity.X + MathHelper.Clamp(lead * 0.004f, -0.35f, 0.35f), -11f, 11f);
            }

            if (lastJumpBitten < jump && MathF.Abs(Projectile.velocity.Y) < 5f
                && Projectile.Center.Y < lakeY - 20f) {
                //弧顶咬合拍：湿噗的合口 + 一星血
                lastJumpBitten = jump;
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                if (!Main.dedServ) {
                    Vector2 mouth = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 40f;
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            mouth + Main.rand.NextVector2Circular(8f, 8f),
                            Main.rand.NextVector2Circular(2.4f, 2.4f),
                            BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                            ?.Configure(Main.rand.Next(14, 24));
                    }
                }
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
            }

            //回潜完成或超时：进下一跳（过水线水花由通用过线器补）
            if (t > 6 && Projectile.Center.Y > lakeY + 30f || t > LeapTimeout) {
                NextJump();
            }
        }

        //==================== 血龙卷：贴水刻痕甩尾 ====================

        private void UpdateNado(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            if (phase == 0) {
                //水下就位：滑到目标一侧偏后，为贴水刻痕摆好进线
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                float side = MathF.Sign(Projectile.Center.X - npc.Center.X);
                if (side == 0f) {
                    side = 1f;
                }
                float alignX = npc.Center.X + side * 240f;
                float dx = alignX - Projectile.Center.X;
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X + MathF.Sign(dx) * 1.3f, -24f, 24f);
                Projectile.velocity.Y = MathHelper.Clamp((lakeY + StalkDepth - Projectile.Center.Y) * 0.12f, -6f, 6f);
                FinWakeFX(domain, dense: false);

                if (MathF.Abs(dx) < 56f && t > 14 || t >= NadoAlignMax) {
                    StateParam = 1;
                    StateTimer = 0;
                    //刻痕方向一帧定死：朝目标那侧掠过去
                    Projectile.velocity = new Vector2(-side * 16f, -3.2f);
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //贴水刻痕：半身弓出水面横掠，两记甩尾各拉起一根龙卷
                if (t == 1) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.35f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                }
                float carveDir = MathF.Sign(Projectile.velocity.X);
                if (carveDir == 0f) {
                    carveDir = 1f;
                }
                Projectile.velocity.X = carveDir * 16f;
                Projectile.velocity.Y = MathHelper.Clamp((lakeY - 10f - Projectile.Center.Y) * 0.3f, -4f, 4f);

                if (ViewedOwner && t % 3 == 1) {
                    //犁开水面的溅痕
                    Vector2 hull = new(Projectile.Center.X - carveDir * 30f, lakeY);
                    KikasaDomainDeco.SplashAt(hull, 3);
                    KikasaDomainDeco.RippleAt(hull, 0.8f);
                }

                int whipIndex = t >= 22 ? 1 : t >= 9 ? 0 : -1;
                if (whipIndex >= 0 && lastWhipFired < whipIndex) {
                    lastWhipFired = whipIndex;
                    WhipUpNado(owner, carveDir, authority);
                }

                if (t >= CarveFrames) {
                    StateParam = 2;
                    StateTimer = 0;
                    //头朝下扎回
                    Projectile.velocity = new Vector2(carveDir * 6f, 11f);
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //回潜收势
            Projectile.velocity.X *= 0.93f;
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp(lakeY + FinDepth - Projectile.Center.Y, -6f, 6f) * 0.2f, 0.2f);
            if (t >= NadoDiveMax) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>甩尾拍：尾位水花重拍 + owner 端拉起一根龙卷（场上上限 2 根）</summary>
        private void WhipUpNado(Player owner, float carveDir, bool authority) {
            Vector2 tailAt = new(Projectile.Center.X - carveDir * 58f, lakeYCache);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.25f, MaxInstances = 2 }, tailAt);
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.3f, Pitch = 0.15f, MaxInstances = 2 }, tailAt);
            if (ViewedOwner) {
                KikasaDomainDeco.SplashAt(tailAt, 10);
                KikasaDomainDeco.RippleAt(tailAt, 1.5f);
                ShakeViewer(2.5f);
            }
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        tailAt + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                        new Vector2(-carveDir * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(4f, 9f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(22, 36));
                }
            }
            //龙卷只在 owner 端生成；spawn 参数自带全部初值（漂移向/花样籽/湖面Y）
            if (authority && CountNados() < 2) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(NadoDamage);
                //朝目标那侧慢慢压过去，是骚扰不是围栏
                int target = FindTarget(owner);
                float drift = target >= 0
                    ? MathF.Sign(Main.npc[target].Center.X - tailAt.X) : carveDir;
                if (drift == 0f) {
                    drift = carveDir;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), tailAt, Vector2.Zero,
                    ModContent.ProjectileType<KikasaFishronBloodnado>(), damage, 3f,
                    Projectile.owner, drift, lastWhipFired, lakeYCache);
            }
        }

        private int CountNados() {
            int count = 0;
            int type = ModContent.ProjectileType<KikasaFishronBloodnado>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Projectile.owner && proj.type == type) {
                    count++;
                }
            }
            return count;
        }

        //==================== 气泡雷环：绕目标一圈吐悬滞血泡 ====================

        private void UpdateBubbleRing(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            if (phase == 0) {
                //就位：水下滑到目标水平近旁，准备出圈
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                float dx = npc.Center.X - Projectile.Center.X;
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X + MathF.Sign(dx) * 1.4f, -24f, 24f);
                Projectile.velocity.Y = MathHelper.Clamp((lakeY + FinDepth - Projectile.Center.Y) * 0.15f, -6f, 6f);
                FinWakeFX(domain, dense: false);

                if (MathF.Abs(dx) < RingRadius + 120f && t > 10 || t >= BubblePosMax) {
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //绕圈：追参数点成圆，空中航速被水下压一头，空中不是它的主场
                if (!ringInit) {
                    //环几何各端进相自建；绕向走确定性籽。中途接管的端
                    //从自己当前所在角续圆，不做闪跳
                    ringInit = true;
                    ringCenter = target >= 0 ? Main.npc[target].Center
                        : Projectile.Center - Vector2.UnitY * RingRadius;
                    ringDir = MathF.Sign(MathF.Sin(Seed));
                    if (ringDir == 0f) {
                        ringDir = 1f;
                    }
                    ringTheta0 = (Projectile.Center - ringCenter).ToRotation()
                        - ringDir * MathHelper.TwoPi * (t / (float)CircleFrames);
                    if (t <= 2) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
                    }
                }
                if (target >= 0) {
                    ringCenter = Vector2.Lerp(ringCenter, Main.npc[target].Center, 0.04f);
                }
                float theta = ringTheta0 + ringDir * MathHelper.TwoPi * (t / (float)CircleFrames);
                Vector2 wantPos = ringCenter + theta.ToRotationVector2() * RingRadius;
                Vector2 chase = (wantPos - Projectile.Center) * 0.5f;
                if (chase.Length() > 19f) {
                    chase = chase.SafeNormalize(Vector2.Zero) * 19f;
                }
                Projectile.velocity = chase;

                //沿圈吐泡：owner 端生成，出口朝环外
                int spitIndex = t * BubbleCount / CircleFrames;
                if (spitIndex < BubbleCount && lastBubbleSpit < spitIndex) {
                    lastBubbleSpit = spitIndex;
                    Vector2 outward = (Projectile.Center - ringCenter).SafeNormalize(Vector2.UnitX);
                    Vector2 mouth = Projectile.Center + Projectile.velocity.SafeNormalize(outward) * 34f;
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, mouth);
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, mouth);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(mouth,
                                outward.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 2.6f),
                                FoamPale * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                                ?.Configure(Main.rand.Next(12, 20), 0f);
                        }
                    }
                    if (authority) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BubbleDamage);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth,
                            outward * 2.3f + Projectile.velocity * 0.08f,
                            ModContent.ProjectileType<KikasaFishronBubble>(), damage, 1.5f,
                            Projectile.owner, spitIndex * 0.83f);
                    }
                }

                if (t >= CircleFrames) {
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //回潜：钻回湖里再收势
            Vector2 diveTo = new(Projectile.Center.X + Projectile.velocity.X * 4f, lakeY + StalkDepth);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                (diveTo - Projectile.Center).SafeNormalize(Vector2.UnitY) * 17f, 0.16f);
            if (Projectile.Center.Y > lakeY + 40f || t >= BubbleDiveMax) {
                EndAttack(authority, 170);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：掠食者的谢幕 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int branch = (int)StateParam;
            float lakeY = domain.LakeWorldY;

            if (branch == 0) {
                //湖已不在：原地化水下坠
                Projectile.velocity.X *= 0.9f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
                if (!Main.dedServ && t % 2 == 0 && t < DissolveFadeTotal - 8) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(30f, 22f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.4f, 3f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 22));
                }
                KillOnSchedule(authority, t, DissolveFadeTotal);
                return;
            }

            int burstT = branch == 2 ? DissolveAirBurstT : DissolveBurstT;
            int total = branch == 2 ? DissolveAirTotal : DissolveLeapTotal;

            if (branch == 1 && !dissolveLaunched) {
                if (t < DissolveGather) {
                    //收势沉肩：往下压一口气，谢幕跃更有蹬水感
                    Projectile.velocity.X *= 0.86f;
                    Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, 2.2f, 0.3f);
                    return;
                }
                //谢幕跃：最后一次破水（过线水花由通用过线器给）
                dissolveLaunched = true;
                Projectile.velocity = new Vector2(Projectile.velocity.X * 0.2f, -13.8f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.4f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
            }

            if (!dissolveBurstDone) {
                //上升渐散
                Projectile.velocity.Y += 0.5f;
                Projectile.velocity.X *= 0.985f;
            }

            if (!dissolveBurstDone && t >= burstT) {
                //空中炸成一蓬血雨：身体没了，血水各自落回湖里
                dissolveBurstDone = true;
                BurstIntoRain();
            }

            if (dissolveBurstDone) {
                Projectile.velocity = Vector2.Zero;
            }

            KillOnSchedule(authority, t, total);
        }

        private void KillOnSchedule(bool authority, int t, int total) {
            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= total) {
                Projectile.Kill();
            }
            else if (!authority && t >= total + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>谢幕血雨：满身血水一次性撒出去，deco 血滴带物理落湖荡微圈</summary>
        private void BurstIntoRain() {
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 1 }, Projectile.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 24; i++) {
                    float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.25f, 1.25f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(34f, 26f),
                        ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 6.5f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.45f, 0.85f))?.Configure(Main.rand.Next(28, 46));
                }
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(30f, 24f),
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(1f, 4f)),
                        FoamPale * 0.55f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(24, 40), 0f);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center + Main.rand.NextVector2Circular(26f, 20f),
                        new Vector2(0f, -0.2f), MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1f))
                        ?.Configure(Main.rand.Next(50, 84));
                }
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.08f)
                    ?.Configure(new Vector2(0.85f, 1f), 0f, 0.3f, 10);
            }
            if (ViewedOwner) {
                //deco 血滴自带抛物线与落湖微圈：谢幕雨真的会"落回湖里"
                KikasaDomainDeco.BloodBurst(new Vector2(Projectile.Center.X, lakeYCache), 16, 1.2f);
                ShakeViewer(3.5f);
            }
        }

        //==================== 过水线（通用）====================

        /// <summary>双向过线拍：±16px 死区滞回，水花量随竖速；贴水掠行不触发</summary>
        private void UpdateWaterCrossing(KikasaDomainPlayer domain) {
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;
            float side = Projectile.Center.Y - lakeY;
            int nowSide = side < -16f ? 1 : side > 16f ? -1 : waterSide;
            if (waterSide == 0) {
                //出生首帧只记边，不放拍
                waterSide = nowSide == 0 ? -1 : nowSide;
                return;
            }
            if (nowSide == waterSide || nowSide == 0) {
                wetness = waterSide < 0 ? 1f : MathF.Max(0f, wetness - 0.012f);
                return;
            }
            waterSide = nowSide;
            wetness = 1f;
            if (!lakeAlive || dissolveBurstDone) {
                return;
            }

            float k = MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) / 22f, 0.25f, 1.2f);
            Vector2 hit = new(Projectile.Center.X, lakeY);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.3f + 0.4f * k,
                Pitch = nowSide < 0 ? -0.45f : -0.2f,
                MaxInstances = 3
            }, hit);
            if (ViewedOwner) {
                KikasaDomainDeco.SplashAt(hit, (int)(4 + 8 * k));
                KikasaDomainDeco.RippleAt(hit, 0.7f + 0.9f * k);
                ShakeViewer(0.8f + 1.8f * k);
            }
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
            float bestDist = 1150f;
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

        /// <summary>咬合帧裁决：6=张口预备 7=全开，-1 走游泳循环</summary>
        private int OverrideFrame() {
            int t = (int)StateTimer;
            if (State == StatePorpoise) {
                if (JumpPhase == 0 && t > StalkMin - 12) {
                    return 6;   //暴起前一口气先张开
                }
                if (JumpPhase == 1) {
                    return t <= BiteWindow ? 7 : 6;
                }
            }
            if (State == StateNado && (int)StateParam == 1) {
                return 7;   //甩尾横掠全程吼着
            }
            if (State == StateBubbleRing && (int)StateParam == 1) {
                return t * BubbleCount / CircleFrames % 2 == 0 ? 7 : 6;   //吐一口合一口
            }
            if (State == StateEmerge && EmergePhase == 1) {
                return 7;   //亮相回旋张口长啸
            }
            return -1;
        }

        private void UpdateFrames() {
            int over = OverrideFrame();
            if (over >= 0) {
                frameIndex = over;
                return;
            }
            if (frameIndex > 5) {
                frameIndex = 0;
            }
            float speed = Projectile.velocity.Length();
            if (++frameTick >= (speed > 16f ? 3 : 5)) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % 6;
            }
        }

        /// <summary>出水淌干前的滴落：湿度即概率，水下不滴</summary>
        private void UpdateDrips() {
            if (Main.dedServ || dissolveBurstDone
                || Projectile.Center.Y >= lakeYCache || wetness < 0.1f) {
                return;
            }
            if (Main.rand.NextFloat() > wetness * 0.4f) {
                return;
            }
            Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(34f, 24f);
            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                new Vector2(Projectile.velocity.X * 0.06f, Main.rand.NextFloat(0.8f, 1.8f)),
                (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 30), 0.3f);
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        /// <summary>uForm：1=全血水 0=真身。亮相跃出时血水在空中凝成真身；湿度抬血水度</summary>
        private float CurrentForm() {
            float steady = MathHelper.Clamp(
                0.32f + wetness * 0.16f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.8f + Seed) * 0.04f,
                0f, 0.6f);
            if (State == StateEmerge) {
                if (EmergePhase <= 0) {
                    return 1f;
                }
                if (EmergePhase == 1) {
                    float p = MathHelper.Clamp(StateTimer / (float)ShowLeapFrames, 0f, 1f);
                    return MathHelper.Lerp(1f, steady, p * p * (3f - 2f * p));
                }
                return steady;
            }
            if (State == StateDissolve) {
                return MathHelper.Clamp(steady + StateTimer / 42f * 0.5f, 0f, 1f);
            }
            return steady;
        }

        private float CurrentDissolve() {
            if (State != StateDissolve) {
                return 0f;
            }
            int branch = (int)StateParam;
            float span = branch == 2 ? DissolveAirBurstT : branch == 1 ? DissolveBurstT : DissolveFadeTotal;
            return MathF.Pow(MathHelper.Clamp(StateTimer / span, 0f, 1f), 0.9f);
        }

        private float CurrentAlpha() {
            if (State != StateDissolve) {
                return 1f;
            }
            if (dissolveBurstDone) {
                return 0f;
            }
            if ((int)StateParam == 0) {
                return MathHelper.Clamp((DissolveFadeTotal - StateTimer) / 14f, 0f, 1f);
            }
            return 1f;
        }

        /// <summary>绘制旋转与翻面：原版猪龙鱼约定，贴图朝右；
        /// 全角机动时向左走水平翻并给角度补 π，巡游时只做小倾角不补</summary>
        private void GetDrawPose(out float rotation, out SpriteEffects flip) {
            Vector2 v = Projectile.velocity;
            if (v.X > 0.8f) {
                faceLeftLatch = false;
            }
            else if (v.X < -0.8f) {
                faceLeftLatch = true;
            }
            bool faceLeft = faceLeftLatch;
            flip = faceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (State == StateFollow || State == StateEmerge && EmergePhase == 0
                || State == StatePorpoise && JumpPhase == 0
                || State == StateNado && (int)StateParam == 0
                || State == StateBubbleRing && (int)StateParam == 0) {
                //巡游姿态：身体近水平，背鳍随速度前倾（原版悬停同款小倾角）
                rotation = MathHelper.Clamp(v.X * 0.018f, -0.3f, 0.3f);
                return;
            }

            //机动姿态：头随速度走
            rotation = v.Length() > 0.5f ? v.ToRotation() : faceLeft ? MathHelper.Pi : 0f;
            if (faceLeft) {
                rotation += MathHelper.Pi;
            }

            //亮相回旋：跃出段叠一整圈拧身
            if (State == StateEmerge && EmergePhase == 1) {
                float p = MathHelper.Clamp(StateTimer / (float)ShowLeapFrames, 0f, 1f);
                rotation += ArcDir * MathHelper.TwoPi * (p * p * (3f - 2f * p));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.DukeFishron);
            Texture2D tex = TextureAssets.Npc[NPCID.DukeFishron]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.DukeFishron];
            Rectangle frame = new(0, frameH * frameIndex, tex.Width, frameH);
            float alpha = CurrentAlpha();
            if (alpha <= 0.01f) {
                return false;
            }

            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            bool lakeAlive = domain != null && domain.AnyActive && domain.RiseT > 0.5f;
            SpriteBatch sb = Main.spriteBatch;
            float rotation = Projectile.rotation;
            SpriteEffects flip = drawFlipLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //水下暗影团：主批直接画（身体沉在湖里时唯一的"体量"读数）
            DrawUnderwaterShadow(sb, lakeAlive);

            //本体：血湖材质 + 水线裁剪（湖面以下不画，只露背鳍）
            DrawBody(sb, tex, frame, rotation, flip, alpha, lakeAlive);

            //加色层：水面行进光斑 / 鳍尖泡沫 / 咬合闪光
            DrawGlow(sb, lakeAlive);

            return false;
        }

        private void DrawUnderwaterShadow(SpriteBatch sb, bool lakeAlive) {
            //暗影团必须用真 alpha 的 Extra_98：黑底 SoftGlow 在 AlphaBlend 里会糊出黑块
            Texture2D shadow = CWRAsset.Extra_98?.Value;
            if (shadow == null || !lakeAlive || dissolveBurstDone
                || Projectile.Center.Y < lakeYCache + 8f) {
                return;
            }
            float speedK = MathHelper.Clamp(Projectile.velocity.Length() / 24f, 0.2f, 1f);
            Vector2 v = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float len = 70f + 50f * speedK;
            const float wid = 30f;
            //乘暗的血影：alpha 混合的深色团，不是发光体（×2 补偿更紧的径向衰减）
            sb.Draw(shadow, Projectile.Center - Main.screenPosition, null,
                BloodDark * (0.20f + 0.12f * speedK), v.ToRotation(),
                shadow.Size() * 0.5f,
                new Vector2(len * 2f / shadow.Width, wid * 2f / shadow.Height) * 2f, SpriteEffects.None, 0f);
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame,
            float rotation, SpriteEffects flip, float alpha, bool lakeAlive) {
            GraphicsDevice device = Main.instance.GraphicsDevice;

            //水线裁剪：把湖面世界 Y 变换到视口像素，只画水上部分
            bool clip = false;
            Rectangle scissor = default;
            if (lakeAlive) {
                Matrix view = Main.GameViewMatrix.TransformationMatrix;
                float cutY = Vector2.Transform(
                    new Vector2(0f, lakeYCache - Main.screenPosition.Y), view).Y;
                int vh = device.Viewport.Height;
                int cut = (int)MathHelper.Clamp(cutY, 0f, vh);
                //视图矩阵带竖直翻转（反重力视角）时，水上区落在裁剪线下方
                Rectangle above = view.M22 < 0f
                    ? new Rectangle(0, cut, device.Viewport.Width, vh - cut)
                    : new Rectangle(0, 0, device.Viewport.Width, cut);
                if (above.Height <= 0) {
                    return;   //整个水上区都不在屏里，身体全沉着，没有可画的部分
                }
                if (above.Height < vh) {
                    clip = true;
                    scissor = above;
                }
            }

            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, clip ? scissorOn : RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (clip) {
                device.ScissorRectangle = scissor;
            }

            Vector2 origin = frame.Size() * 0.5f;

            //跃出残影：速度门控，Apply 之前画的都走默认精灵着色器
            float speed = Projectile.velocity.Length();
            if (speed > 14f && !dissolveBurstDone) {
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                        BloodMain * (0.30f * fall * alpha), Projectile.oldRot[k],
                        origin, DrawScale * (0.97f - k * 0.014f), flip, 0f);
                }
            }

            Color color;
            if (shaderOk) {
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(CurrentForm());
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
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
                //无着色器回退：CPU 血染
                color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
            }

            sb.Draw(tex, Projectile.Center - Main.screenPosition, frame, color,
                rotation, origin, DrawScale, flip, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb, bool lakeAlive) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || !lakeAlive || dissolveBurstDone) {
                return;
            }
            Vector2 gOrigin = glow.Size() * 0.5f;
            bool begun = false;
            void EnsureBegin() {
                if (!begun) {
                    begun = true;
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
            }

            float speedX = MathF.Abs(Projectile.velocity.X);

            //水下行进的水面光斑：速度越快越亮越长，航迹密度之外的第二速度读数
            if (Projectile.Center.Y > lakeYCache + 8f && speedX > 3f) {
                EnsureBegin();
                float speedK = MathHelper.Clamp(speedX / 27f, 0.2f, 1f);
                Vector2 pos = new(Projectile.Center.X + Projectile.velocity.X * 0.8f, lakeYCache + 6f);
                float r = 26f + 30f * speedK;
                sb.Draw(glow, pos - Main.screenPosition, null, FoamPale * (0.34f * speedK), 0f,
                    gOrigin, new Vector2(r * 3.0f / glow.Width, r * 0.8f / glow.Height), SpriteEffects.None, 0f);
            }

            //鳍尖切水的泡沫痕：贴着鳍根的两道短流光，顺带糊住裁剪缝
            if (Projectile.Center.Y > lakeYCache && Projectile.Center.Y < lakeYCache + FinDepth + 26f
                && speedX > 2.5f) {
                EnsureBegin();
                float dir = MathF.Sign(Projectile.velocity.X);
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = new(Projectile.Center.X - dir * (14f + i * 22f), lakeYCache - 1f);
                    float w = 26f - i * 8f;
                    sb.Draw(glow, pos - Main.screenPosition, null,
                        FoamPale * (0.4f - i * 0.14f), 0f, gOrigin,
                        new Vector2(w * 2f / glow.Width, 7f / glow.Height), SpriteEffects.None, 0f);
                }
                //渊青只做鳍缘次要点缀
                Vector2 finTip = new(Projectile.Center.X, lakeYCache - 10f);
                sb.Draw(glow, finTip - Main.screenPosition, null, AbyssSheen * 0.14f, 0f,
                    gOrigin, new Vector2(16f * 2f / glow.Width, 8f / glow.Height), SpriteEffects.None, 0f);
            }

            //咬合闪光：弧顶那一口
            if (State == StatePorpoise && JumpPhase == 1 && (int)StateTimer <= BiteWindow
                && MathF.Abs(Projectile.velocity.Y) < 6f) {
                EnsureBegin();
                Vector2 mouth = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42f;
                sb.Draw(glow, mouth - Main.screenPosition, null, FoamPale * 0.5f, 0f,
                    gOrigin, new Vector2(22f * 2f / glow.Width, 22f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //破水咬命中的溅血（OnHit 只在 owner 端跑，队友看拖尾即可）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    Projectile.velocity * 0.24f + Main.rand.NextVector2Circular(2.6f, 2.6f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：异常移除也留一口血水
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 22f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
