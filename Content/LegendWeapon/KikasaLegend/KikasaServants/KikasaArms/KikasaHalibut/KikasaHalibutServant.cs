using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
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
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaHalibut
{
    /// <summary>
    /// 械奴·比目鱼（专属条目，短路通用枪奴推断，通用档案只会读到模板伤 4 的笑话）。
    /// 单杆湖水凝成的星港比目鱼枪：普攻吐真海水（直接生成武器本体的
    /// <see cref="OceanCurrent"/> 洪流，血湖的枪打出海洋的弹，反差即身份），
    /// 出招池 = 洪流点射与潮涌扇喷轮换；领域冷却就绪时冲位到猎物上方泼出
    /// <see cref="KikasaHalibutDomain"/> 小海，领域存续期射速上浮。
    /// 强度读沉入原件的传奇等级（Summon 时 owner 本机烘焙，ExtraAI 随包补发），
    /// 联机契约与通用械奴同构：owner 裁决转场盖 netUpdate 章、弹与领域仅 authority 生成、
    /// 生命线只有 owner 判、节拍闩防快照回卷
    /// </summary>
    internal class KikasaHalibutServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>洪流单发伤害倍率（基伤=沉入原件的等级伤害；械奴射速远低于玩家手打，单发补重）</summary>
        internal const float TorrentDamageMul = 1.8f;

        /// <summary>潮涌扇喷单股倍率</summary>
        internal const float SurgeDamageMul = 1.2f;

        /// <summary>小海水压每记倍率</summary>
        internal const float DomainTickMul = 0.5f;

        /// <summary>领域冷却帧数（约 18 秒）</summary>
        internal const int DomainCooldownFrames = 1080;

        /// <summary>洪流出膛速度</summary>
        private const float TorrentSpeed = 15.5f;

        //==================== 烘焙数值（owner 在 Summon 里定值，ExtraAI 随包同步）====================

        /// <summary>沉入原件的等级伤害（召唤加成前）；远端与服务器不读湖藏，只认这份烘焙</summary>
        private int baseDamage = 4;

        /// <summary>沉入原件的传奇等级（领域半径微调用）</summary>
        private int legendLevel;

        /// <summary>沉影盘在场判定用：专属械奴恒复制比目鱼</summary>
        public int ArmsItemType => HalibutOverride.ID;

        /// <summary>绘制缩放：128×76 贴图对齐武器本体手感（HalibutOverride.ItemScale 同源）</summary>
        private const float GunDrawScale = 0.8f;

        /// <summary>枪口探出距离</summary>
        private const float MuzzleLen = 60f;

        /// <summary>小海半径：随等级缓涨</summary>
        private float DomainRadius => 222f + legendLevel * 6f;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateTorrent = 2;
        private const int StateSurge = 3;
        private const int StateDomainCast = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：单点预兆→破水翻腾→凝实→上膛拍
        private const int OmenFrames = 26;
        private const int RiseEnd = 56;
        private const int FormupFrame = 60;
        private const int EmergeTotal = 76;

        //洪流点射：甩入射击位→锁线→三发点射→收势
        private const int TorrentFormEnd = 12;
        private const int TorrentLockEnd = 16;
        private const int TorrentShots = 3;
        private const int TorrentTotal = 84;

        /// <summary>点射节拍：小海存续时打得更密、多补一发</summary>
        private int TorrentPeriod => OwnDomainAlive() ? 12 : 18;

        private int TorrentShotCount => OwnDomainAlive() ? TorrentShots + 1 : TorrentShots;

        //潮涌扇喷：压近站位→泵动蓄势→两轮三股齐喷（整枪后坐）
        private const int SurgeFormEnd = 16;
        private const int SurgeSalvoA = 26;
        private const int SurgeSalvoB = 54;
        private const int SurgeTotal = 86;

        //领域压制：冲位到猎物上方→蓄水→泼出小海→收势
        private const int CastDashEnd = 16;
        private const int CastReleaseFrame = 38;
        private const int CastTotal = 60;

        //溶解：失力坠湖
        private const int DissolveFrames = 70;

        //==================== 枪体本地模拟（各端自算，质心同步纠偏）====================

        private Vector2 gunPos;
        private Vector2 gunVel;
        private Vector2 gunTarget;
        private float gunRot;
        /// <summary>出水翻腾角速度</summary>
        private float gunSpin;
        /// <summary>后坐量 px，沿 -瞄准向偏移绘制位并抬枪口</summary>
        private float gunRecoil;
        /// <summary>贴图翻面状态：带滞回，瞄向正上/正下时不逐帧镜像抖动</summary>
        private bool gunFlip;
        private readonly Vector2[] gunOld = new Vector2[8];
        private readonly float[] gunOldRot = new float[8];
        private bool gunInit;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private bool breachDone;
        private int muzzleFlash;
        private int lastFireTick;
        private bool castReleased;
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private int domainCooldown;
        private bool formSnapDone;
        private bool dissolveSplashed;
        /// <summary>领域施放点：冲位/蓄水/泼出共用，先声明后泼</summary>
        private Vector2 castPos;
        private bool castDeclared;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        /// <summary>
        /// KikasaArmsIndex 专门条目的召唤入口；count 不折算编制，传奇武器沉一件即完整形态，
        /// 多件只取最高等级件定强度
        /// </summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            //湖藏里最高等级的比目鱼原件：伤害与领域规格的依据（湖藏数据本机私有，烘焙后随包同步）
            Item best = null;
            int bestLv = -1;
            foreach (Item item in owner.GetModPlayer<KikasaVaultPlayer>().Stored) {
                if (item?.IsAir == false && item.type == HalibutOverride.ID) {
                    int lv = HalibutData.GetLevel(item);
                    if (lv > bestLv) {
                        bestLv = lv;
                        best = item;
                    }
                }
            }
            int baseDmg = best != null ? HalibutOverride.GetOnDamage(best) : HalibutOverride.GetStartDamage;
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg * TorrentDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaHalibutServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaHalibutServant gun) {
                gun.baseDamage = baseDmg;
                gun.legendLevel = Math.Max(bestLv, 0);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
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
            Projectile.timeLeft = 180;
        }

        /// <summary>枪体不做接触判定，伤害全在海洋洪流与小海上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(baseDamage);
            writer.Write((byte)Math.Clamp(legendLevel, 0, 255));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int dmg = reader.ReadInt32();
            int lv = reader.ReadByte();
            if (dmg > 0) {
                baseDamage = dmg;
            }
            legendLevel = lv;
        }

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //还没破水就要收场：直接收掉，免得溶解演出让枪凭空闪现再化水
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

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约）
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * TorrentDamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                lastFireTick = -1;
                castReleased = false;
                castDeclared = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!gunInit) {
                RebuildGun(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateTorrent: UpdateTorrent(owner, authority); break;
                case StateSurge: UpdateSurge(owner, authority); break;
                case StateDomainCast: UpdateDomainCast(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateGun(owner, domain);
            PushGunHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (domainCooldown > 0) {
                domainCooldown--;
            }
            if (muzzleFlash > 0) {
                muzzleFlash--;
            }
            gunRecoil *= 0.76f;
            float glow = GunAlpha() * 0.35f;
            if (glow > 0.02f) {
                //血枪身 + 海洋弹的混色灯
                Lighting.AddLight(gunPos, 0.3f * glow, 0.25f * glow, 0.4f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>自家小海是否在场（射速上浮的依据；各端本地扫，弹幕表可见即一致）</summary>
        private bool OwnDomainAlive() {
            int type = ModContent.ProjectileType<KikasaHalibutDomain>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && proj.owner == Projectile.owner) {
                    return true;
                }
            }
            return false;
        }

        //==================== 出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：一处水面起预兆
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    float wobble = MathF.Sin(t * 0.5f) * converge * 24f;
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + wobble, lakeY),
                        0.35f + (1f - converge) * 0.45f);
                }
                if (viewed && (t == 6 || t == 16)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //破水翻腾
            if (!breachDone) {
                breachDone = true;
                gunVel = new Vector2(0f, -12.6f);
                gunSpin = 0.3f;
                Projectile.velocity = new Vector2(0f, -3.2f);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.72f,
                    Pitch = -0.38f,
                    MaxInstances = 3
                }, gunPos);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            Projectile.velocity *= 0.96f;

            //身上的湖水成帘往下淌
            if (viewed && t < RiseEnd && t % 3 == 0) {
                Vector2 dropPos = gunPos + new Vector2(
                    Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(2f, 14f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }

            //上膛双拍：一顿、枪机咔嗒，它醒了
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                gunVel += new Vector2(0f, -1.2f);
                if (viewed) {
                    for (int k = 0; k < 3; k++) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            gunPos + Main.rand.NextVector2Circular(16f, 8f),
                            new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 1.8f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(10, 18), 0.25f);
                    }
                    ShakeViewer(2f);
                }
            }
            if (t == FormupFrame + 4) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                //出水后小海先候半程，别开场就把大招拍脸上
                domainCooldown = DomainCooldownFrames / 2;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠：单枪规格</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 1.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(22f, 0f), 0.6f);
            KikasaDomainDeco.SplashAt(hit, 8);

            for (int k = 0; k < 12; k++) {
                float angle = -MathHelper.Pi * (0.16f + 0.68f * k / 11f);
                float speed = Main.rand.NextFloat(2.6f, 6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-18f, 18f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.6f)),
                MistBlood * 0.75f, Main.rand.NextFloat(0.55f, 0.8f))
                ?.Configure(Main.rand.Next(50, 80));

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.32f,
                Pitch = -0.75f,
                MaxInstances = 1
            }, hit);
            ShakeViewer(1.5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：贴着玩家肩侧游弋
            Vector2 anchor = owner.Center + new Vector2(0f, -28f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildGun(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：小海冷却就绪优先泼海，其余点射与扇喷轮换；owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                if (domainCooldown <= 0) {
                    State = StateDomainCast;
                }
                else {
                    State = attackIndex % 2 == 1 ? StateTorrent : StateSurge;
                }
                Projectile.netUpdate = authority;
            }
        }

        //==================== 洪流点射 ====================

        private static int TorrentShotFrame(int k, int period) => TorrentLockEnd + 4 + k * period;

        private void UpdateTorrent(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= TorrentLockEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : Projectile.Center + gunRot.ToRotationVector2() * 500f;

            //质心压到玩家与目标之间的射击位，边打边横移
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            float strafe = MathF.Sin(t * 0.05f + Seed) * 26f;
            Vector2 anchor = owner.Center + toT * 58f + perp * strafe + new Vector2(0f, -22f);
            Vector2 desired = (anchor - Projectile.Center) * 0.11f;
            if (desired.Length() > 14f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 14f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            //锁线一声上膛
            if (t == 6) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Volume = 0.38f,
                    Pitch = -0.35f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            //点射节拍：节拍闩防快照回卷重播
            int period = TorrentPeriod;
            int shots = TorrentShotCount;
            for (int k = 0; k < shots; k++) {
                if (t == TorrentShotFrame(k, period) && k > lastFireTick) {
                    lastFireTick = k;
                    FireTorrent(owner, authority, focus);
                }
            }

            if (t >= TorrentTotal) {
                EndAttack(authority, OwnDomainAlive() ? 60 : 95);
            }
        }

        /// <summary>吐一口洪流：blood 枪身泼出海洋的水，出膛即比目鱼普攻的 OceanCurrent</summary>
        private void FireTorrent(Player owner, bool authority, Vector2 focus, float spreadAng = 0f, float damageMul = TorrentDamageMul) {
            Vector2 aimDir = gunRot.ToRotationVector2().RotatedBy(spreadAng);
            Vector2 muzzle = MuzzlePos();
            gunRecoil = 14f;
            gunVel -= aimDir * 1.5f;
            muzzleFlash = 5;

            //开火音借武器本体的 Item38，垫一记水花
            SoundEngine.PlaySound(SoundID.Item38 with {
                Volume = 0.42f,
                Pitch = -0.1f + spreadAng * 0.4f,
                MaxInstances = 4
            }, muzzle);
            if (!Main.dedServ) {
                //枪口浪花：海洋色的崩碎（弹的身份从出膛这一刻就换成海）
                OceanCurrentVFX.SplashBurst(muzzle, aimDir * 5f, 0.55f);
            }
            if (ViewedOwner) {
                ShakeViewer(0.7f);
            }

            //弹体只在 owner 端生成：抬角补偿洪流的重力弧线
            if (authority) {
                float dist = Vector2.Distance(muzzle, focus);
                Vector2 vel = aimDir * TorrentSpeed;
                vel.Y -= dist * 0.0022f;
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * damageMul);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<OceanCurrent>(), damage, 2f, Projectile.owner);
            }
        }

        //==================== 潮涌扇喷 ====================

        private void UpdateSurge(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= SurgeFormEnd) {
                EndAttack(authority, 60);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 5f
                : Projectile.Center + gunRot.ToRotationVector2() * 300f;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);

            //压近站位：贴到目标跟前一段；齐喷后被后坐顶开，拍间再压回
            Vector2 anchor = focus - toT * 200f + new Vector2(0f, -14f);
            Vector2 desired = (anchor - Projectile.Center) * 0.12f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.2f);

            //泵动双拍：要泼了
            if (t == SurgeFormEnd || t == SurgeFormEnd + 6) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Volume = 0.45f,
                    Pitch = t == SurgeFormEnd ? -0.35f : -0.1f,
                    MaxInstances = 2
                }, Projectile.Center);
                gunVel -= gunRot.ToRotationVector2() * 1.1f;
            }

            //两轮三股扇喷：节拍闩记轮次
            for (int k = 0; k < 2; k++) {
                int frame = k == 0 ? SurgeSalvoA : SurgeSalvoB;
                if (t == frame && k > lastFireTick) {
                    lastFireTick = k;
                    FireTorrent(owner, authority, focus, -0.2f, SurgeDamageMul);
                    FireTorrent(owner, authority, focus, 0f, SurgeDamageMul);
                    FireTorrent(owner, authority, focus, 0.2f, SurgeDamageMul);
                    //整枪后坐推退：泼出去的水把枪顶回来
                    Projectile.velocity -= toT * 6f;
                    SoundEngine.PlaySound(SoundID.Splash with {
                        Volume = 0.5f,
                        Pitch = -0.2f,
                        MaxInstances = 2
                    }, MuzzlePos());
                    if (ViewedOwner) {
                        ShakeViewer(2.4f);
                    }
                }
            }

            if (t >= SurgeTotal) {
                EndAttack(authority, 130);
            }
        }

        //==================== 领域压制：冲位、蓄水、泼海 ====================

        private void UpdateDomainCast(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= CastDashEnd) {
                EndAttack(authority, 60);
                return;
            }

            //施放点声明：猎物此刻的位置（跳帧进窗也补上）
            if (!castDeclared) {
                castDeclared = true;
                castPos = target >= 0 ? Main.npc[target].Center : Projectile.Center;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
            }

            //冲位到施放点上方；蓄水期缓慢下压
            Vector2 anchor = castPos + new Vector2(0f, t <= CastDashEnd ? -170f : -140f);
            float chase = t <= CastDashEnd ? 0.2f : 0.08f;
            Vector2 desired = (anchor - Projectile.Center) * chase;
            if (desired.Length() > 24f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 24f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.25f);

            //蓄水：枪身滴水渐密、一声深海回响预告
            if (t == CastDashEnd + 6) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
            }
            if (!Main.dedServ && t > CastDashEnd && t < CastReleaseFrame && t % 3 == 0) {
                float chargeT = (t - CastDashEnd) / (float)(CastReleaseFrame - CastDashEnd);
                OceanCurrentVFX.SplashBurst(MuzzlePos(), new Vector2(0f, 2f), 0.3f + chargeT * 0.3f);
            }

            //泼海：owner 端在施放点生成小海（节拍闩防重泼）
            if (t >= CastReleaseFrame && !castReleased) {
                castReleased = true;
                domainCooldown = DomainCooldownFrames;
                gunRecoil = 22f;
                gunVel += new Vector2(0f, -3f);
                muzzleFlash = 7;
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, castPos);
                SoundEngine.PlaySound(SoundID.Item38 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, MuzzlePos());
                if (!Main.dedServ) {
                    OceanCurrentVFX.SplashBurst(MuzzlePos(), (castPos - MuzzlePos()).SafeNormalize(Vector2.UnitY) * 8f, 1.1f);
                }
                if (ViewedOwner) {
                    ShakeViewer(3f);
                }
                if (authority) {
                    int tickDamage = (int)Owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * DomainTickMul);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), castPos, Vector2.Zero,
                        ModContent.ProjectileType<KikasaHalibutDomain>(), tickDamage, 0f, Projectile.owner,
                        DomainRadius);
                }
            }

            if (t >= CastTotal) {
                EndAttack(authority, 85);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (lakeAlive) {
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.2f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            if (lakeAlive && !dissolveSplashed && gunPos.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.55f,
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, gunPos);
                if (ViewedOwner) {
                    Vector2 hit = new(gunPos.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 6);
                    KikasaDomainDeco.RippleAt(hit, 0.9f);
                    ShakeViewer(1f);
                }
            }

            //边沉边化成水珠
            if (!Main.dedServ && GunAlpha() > 0.15f && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + Main.rand.NextVector2Circular(20f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 枪体推进 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防枪体与残影抽搐</summary>
        private void RebuildGun(KikasaDomainPlayer domain) {
            gunInit = true;
            if (State == StateEmerge) {
                gunPos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 26f);
                gunRot = -MathHelper.PiOver2;
            }
            else {
                gunPos = Projectile.Center + new Vector2(0f, -6f);
                gunRot = 0f;
            }
            gunFlip = MathF.Cos(gunRot) < 0f;
            gunVel = Vector2.Zero;
            gunSpin = 0f;
            gunRecoil = 0f;
            gunTarget = gunPos;
            for (int k = 0; k < gunOld.Length; k++) {
                gunOld[k] = gunPos;
                gunOldRot[k] = gunRot;
            }
        }

        private void ChaseGun(float accel, float damp) {
            gunVel = (gunVel + (gunTarget - gunPos) * accel) * damp;
            gunPos += gunVel;
        }

        /// <summary>呼吸浮动相位（Seed 确定性，各端一致）</summary>
        private float Sway(float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed) * amp;

        private void FaceGun(Vector2 worldPos, float rate) {
            float want = (worldPos - gunPos).ToRotation();
            gunRot = gunRot.AngleLerp(want, rate);
        }

        private void UpdateGun(Player owner, KikasaDomainPlayer domain) {
            if (!gunInit) {
                return;
            }
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    if (t < OmenFrames) {
                        //水下待命：钉在破水点，鼻朝上
                        gunPos = new Vector2(Projectile.Center.X, lakeY + 26f);
                        gunVel = Vector2.Zero;
                        gunTarget = gunPos;
                        gunRot = -MathHelper.PiOver2;
                        break;
                    }
                    //破水后：先弹道升+翻腾，14 帧后弹簧接管贴向悬位
                    gunTarget = new Vector2(Projectile.Center.X, lakeY - 96f + Sway(2.1f, 9f));
                    int lt = t - OmenFrames;
                    if (lt < 14) {
                        gunVel.Y *= 0.955f;
                        gunVel.X *= 0.98f;
                        gunPos += gunVel;
                        gunRot += gunSpin;
                        gunSpin *= 0.94f;
                    }
                    else {
                        ChaseGun(0.05f, 0.86f);
                        gunRot += gunSpin;
                        gunSpin *= 0.9f;
                        if (MathF.Abs(gunSpin) < 0.05f) {
                            //翻腾散尽后校平，鼻朝外
                            float level = gunPos.X >= Projectile.Center.X ? 0f : MathHelper.Pi;
                            gunRot = gunRot.AngleLerp(level, 0.14f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    float phase = tGlobal * 0.62f + Seed;
                    Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 30f, MathF.Sin(phase) * 12f - 8f);
                    slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f) * 7f;
                    gunTarget = slot;
                    ChaseGun(0.06f, 0.84f);

                    //朝向：有猎物盯猎物，没有就贴游弋速度入弯
                    if (target >= 0) {
                        FaceGun(targetPos, 0.16f);
                    }
                    else if (gunVel.Length() > 2.6f) {
                        gunRot = gunRot.AngleLerp(gunVel.ToRotation(), 0.12f);
                    }
                    else {
                        gunRot = gunRot.AngleLerp(owner.direction > 0 ? 0f : MathHelper.Pi, 0.05f);
                    }
                    break;
                }
                case StateTorrent: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                        : Projectile.Center + gunRot.ToRotationVector2() * 500f;
                    Vector2 toT = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    //探身射击位
                    Vector2 slot = Projectile.Center + toT * 26f + new Vector2(0f, Sway(1.8f, 4f));
                    gunTarget = slot;
                    ChaseGun(t < TorrentFormEnd ? 0.12f : 0.08f, 0.8f);
                    FaceGun(aimPos, t < TorrentLockEnd ? 0.3f : 0.45f);
                    break;
                }
                case StateSurge: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 4f
                        : Projectile.Center + gunRot.ToRotationVector2() * 300f;
                    Vector2 toT = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    //抵近压枪位
                    Vector2 slot = Projectile.Center + toT * 20f + new Vector2(0f, Sway(2f, 3f));
                    gunTarget = slot;
                    ChaseGun(0.13f, 0.78f);
                    FaceGun(aimPos, 0.42f);
                    break;
                }
                case StateDomainCast: {
                    //枪口垂向施放点：泼海的姿势
                    Vector2 slot = Projectile.Center + new Vector2(Sway(1.5f, 6f), 4f);
                    gunTarget = slot;
                    ChaseGun(0.1f, 0.8f);
                    FaceGun(castDeclared ? castPos : targetPos, 0.24f);
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    gunVel.X *= 0.93f;
                    gunVel.Y = MathF.Min(gunVel.Y + 0.3f, 9.5f);
                    //鼻端下垂着沉下去
                    float droop = gunRot + (MathF.Cos(gunRot) >= 0f ? 0.5f : -0.5f);
                    gunRot = gunRot.AngleLerp(droop, 0.02f);
                    gunPos += gunVel;
                    gunTarget = gunPos;
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix && Vector2.Distance(gunPos, gunTarget) > 780f) {
                gunPos = gunTarget;
                gunVel = Vector2.Zero;
            }

            //翻面滞回：cos 越过 ±0.22 才换面，正上/正下瞄准不抖镜像
            float c = MathF.Cos(gunRot);
            if (c > 0.22f) {
                gunFlip = false;
            }
            else if (c < -0.22f) {
                gunFlip = true;
            }
        }

        private void PushGunHistory() {
            for (int k = gunOld.Length - 1; k >= 1; k--) {
                gunOld[k] = gunOld[k - 1];
                gunOldRot[k] = gunOldRot[k - 1];
            }
            gunOld[0] = gunPos;
            gunOldRot[0] = gunRot;
        }

        /// <summary>常驻氛围：液态下缘偶发凝珠滴落，枪一直在往下滴湖水</summary>
        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateTorrent or StateSurge or StateDomainCast)) {
                return;
            }
            if (Main.rand.NextBool(16) && GunAlpha() > 0.5f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(6f, 12f)),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(16, 28), 0f);
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

        /// <summary>绘制位：后坐沿 -瞄准向顶回</summary>
        private Vector2 GunDrawPos()
            => gunPos - gunRot.ToRotationVector2() * gunRecoil;

        /// <summary>枪口位：绘制位沿瞄准向探出半个枪身</summary>
        private Vector2 MuzzlePos()
            => GunDrawPos() + gunRot.ToRotationVector2() * (MuzzleLen * GunScale() / GunDrawScale);

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float GunAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm 水线呼吸：同通用械奴，实体上半 + 液态下缘</summary>
        private float GunForm() {
            int t = (int)StateTimer;
            float steady = 0.24f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 0.06f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uDissolve：溶解期蚀散，落水后加速</summary>
        private float DissolveAmt() {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp(StateTimer / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed ? 0.15f : 0f), 0f, 1f);
        }

        private float GunScale() {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            //后坐压缩一口气
            scale *= 1f - gunRecoil * 0.004f;
            return scale * GunDrawScale;
        }

        /// <summary>领域蓄水进度 0~1：泼海前的预告</summary>
        private float CastCharge() {
            if (State != StateDomainCast) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= CastDashEnd || t >= CastReleaseFrame) {
                return 0f;
            }
            return MathHelper.Clamp((t - CastDashEnd) / (float)(CastReleaseFrame - CastDashEnd), 0f, 1f);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        /// <summary>
        /// 翻面走水平镜像 + 旋转加 π（持枪标准做法）：贴图 V 轴不动，
        /// 扫描水线永远贴着枪的下缘
        /// </summary>
        private SpriteEffects GunFx()
            => gunFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        private float FlipRotOffset() => gunFlip ? MathHelper.Pi : 0f;

        /// <summary>绘制用旋转：后坐抬枪口（屏幕向上，符号随翻面）</summary>
        private float GunDrawRot()
            => gunRot - gunRecoil * 0.006f * (gunFlip ? -1f : 1f);

        public override bool PreDraw(ref Color lightColor) {
            if (!gunInit) {
                return false;
            }
            Main.instance.LoadItem(HalibutOverride.ID);
            Texture2D tex = TextureAssets.Item[HalibutOverride.ID]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //冲刺残影
            DrawDashTrails(sb, tex);

            //枪体本体：血湖材质
            DrawBody(sb, tex);

            //加色层：预兆水光 / 蓄水预告 / 枪口闪
            DrawGlow(sb);

            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            float trailA = MathHelper.Clamp((gunVel.Length() - 8f) / 10f, 0f, 1f) * GunAlpha();
            if (trailA <= 0.03f) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            for (int k = gunOld.Length - 1; k >= 1; k--) {
                float fall = 1f - k / (float)gunOld.Length;
                sb.Draw(tex, gunOld[k] - Main.screenPosition, null,
                    BloodMain * (0.26f * fall * trailA), gunOldRot[k] + FlipRotOffset(),
                    origin, GunScale() * (0.96f - k * 0.015f), GunFx(), 0f);
            }
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex) {
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
                form.Parameters["uScanMode"]?.SetValue(1f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
            }

            float alpha = GunAlpha();
            if (alpha > 0.01f) {
                float rot = GunDrawRot() + FlipRotOffset();
                Vector2 drawPos = GunDrawPos() - Main.screenPosition;
                float dissolve = DissolveAmt();
                Vector2 origin = tex.Size() * 0.5f;

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = GunScale() * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, GunFx(), 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(GunForm());
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, drawPos, null, color, rot, origin, GunScale(), GunFx(), 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            if (domain == null) {
                return;
            }

            bool begun = false;
            Vector2 gOrigin = glow.Size() * 0.5f;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            int t = (int)StateTimer;

            //预兆：水下血光上浮
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                float r = 20f + 14f * ease;
                sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                    gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            //水光扫掠：湿面上一道窄亮痕周期滑过枪身
            if (State is StateFollow or StateTorrent or StateSurge or StateDomainCast) {
                float p = (Main.GlobalTimeWrappedHourly * 0.42f + Seed * 0.13f) % 1f;
                if (p < 0.34f && GunAlpha() > 0.5f) {
                    EnsureBegin();
                    float k = p / 0.34f;
                    float a = MathF.Sin(k * MathHelper.Pi) * 0.3f * GunAlpha();
                    Vector2 dir = gunRot.ToRotationVector2();
                    float halfLen = MuzzleLen * 0.9f;
                    Vector2 pos = GunDrawPos() + dir * MathHelper.Lerp(-halfLen, halfLen, k);
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * a,
                        gunRot + MathHelper.PiOver2, gOrigin,
                        new Vector2(20f * 2f / glow.Width, 5f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //蓄水预告：施放点一圈渐涨的海光，领域要来了
            float charge = CastCharge();
            if (charge > 0.05f) {
                EnsureBegin();
                Color sea = OceanCurrentVFX.WaterBright;
                float ringR = MathHelper.Lerp(24f, DomainRadius * 0.4f, charge);
                sb.Draw(glow, castPos - Main.screenPosition, null, sea * (0.3f * charge), 0f,
                    gOrigin, new Vector2(ringR * 2f / glow.Width), SpriteEffects.None, 0f);
                //枪口积水光
                sb.Draw(glow, MuzzlePos() - Main.screenPosition, null,
                    OceanCurrentVFX.ShallowOcean * (0.5f * charge), 0f,
                    gOrigin, new Vector2((8f + 12f * charge) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //枪口闪：出膛那一帧的浪光爆点（海洋色，弹的身份）
            if (muzzleFlash > 0) {
                EnsureBegin();
                float a = muzzleFlash / 4f;
                Vector2 muzzle = MuzzlePos();
                sb.Draw(glow, muzzle - Main.screenPosition, null,
                    OceanCurrentVFX.WaterBright * (0.55f * a), gunRot,
                    gOrigin, new Vector2(30f / glow.Width * 2f, 10f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, muzzle - Main.screenPosition, null,
                    OceanCurrentVFX.OceanFoam * (0.32f * a), 0f,
                    gOrigin, new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !gunInit) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + Main.rand.NextVector2Circular(18f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.65f, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(45, 70));
        }
    }
}
