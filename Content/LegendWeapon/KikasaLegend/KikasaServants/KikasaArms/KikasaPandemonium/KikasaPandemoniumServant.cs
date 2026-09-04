using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaPandemonium
{
    /// <summary>
    /// 械奴·万魔殿（专属条目，短路通用推断，HoldUp 魔法进不了枪/刀/鞭）。
    /// 单本法书：血湖凝成的书身吐真硫磺火（直接生成武器本体镰/火球/闪电/血雨），
    /// 出招池 = 镰刀螺旋与火球拍击轮换；法阵冷却就绪时冲位泼出
    /// <see cref="KikasaPandemoniumCircle"/> 小硫磺阵。
    /// 强度读沉入原件的物品伤（Summon 本机烘焙，ExtraAI 随包补发），
    /// 联机契约与通用械奴同构：owner 裁决转场、弹与阵仅 authority 生成、生命线只有 owner 判
    /// </summary>
    internal class KikasaPandemoniumServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float ScytheDamageMul = 0.5f;
        internal const float FireballDamageMul = 0.65f;
        internal const float CircleTickMul = 0.2f;
        internal const int CircleCooldownFrames = 960;

        private int baseDamage = 320;
        public int ArmsItemType => ModContent.ItemType<Pandemonium>();

        /// <summary>专属单体：强度由原件模板伤烘焙，不吃编队摊薄</summary>
        public int UnitCount => 1;

        private const float BookDrawScale = 0.92f;

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateScythe = 2;
        private const int StateFireball = 3;
        private const int StateCircleCast = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        private const int OmenFrames = 26;
        private const int RiseEnd = 56;
        private const int FormupFrame = 60;
        private const int EmergeTotal = 76;

        private const int ScytheFormEnd = 14;
        private const int ScytheRelease = 22;
        private const int ScytheTotal = 78;
        private const int ScytheCount = 5;

        private const int FireFormEnd = 12;
        private const int FireReleaseA = 20;
        private const int FireReleaseB = 28;
        private const int FireReleaseC = 36;
        private const int FireTotal = 70;

        /// <summary>拍击三拍帧位（错帧感全靠拍位，火球 ai0 延迟不可用，见 ReleaseFireball）</summary>
        private static readonly int[] FireBeats = [FireReleaseA, FireReleaseB, FireReleaseC];

        private const int CastDashEnd = 16;
        private const int CastReleaseFrame = 34;
        private const int CastTotal = 56;

        private const int DissolveFrames = 70;

        private Vector2 bookPos;
        private Vector2 bookVel;
        private Vector2 bookTarget;
        private float bookRot;
        private float bookSpin;
        private float bookRecoil;
        /// <summary>翻页开合 0 合 1 开，只喂绘制与加色</summary>
        private float bookOpen;
        private bool bookFlip;
        private readonly Vector2[] bookOld = new Vector2[8];
        private readonly float[] bookOldRot = new float[8];
        private bool bookInit;

        private bool breachDone;
        private int lastFireTick;
        private bool castReleased;
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private int circleCooldown;
        private bool formSnapDone;
        private bool dissolveSplashed;
        private Vector2 castPos;
        private bool castDeclared;
        private int castFlash;

        private Player Owner => Main.player[Projectile.owner];
        private float Seed => Projectile.identity * 0.6173f;
        private static int ItemType => ModContent.ItemType<Pandemonium>();

        internal static void Summon(Player owner, Vector2 emergeAt, int count) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            //模板伤烘焙,词缀不参与(同推断器契约),沉几件都一样
            int baseDmg = 320;
            if (ContentSamples.ItemsByType.TryGetValue(ItemType, out Item sample)
                && sample?.IsAir == false && sample.damage > 0) {
                baseDmg = sample.damage;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg * ScytheDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaPandemoniumServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaPandemoniumServant book) {
                book.baseDamage = baseDmg;
                Main.projectile[index].netUpdate = true;
            }
        }

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
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

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(baseDamage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int dmg = reader.ReadInt32();
            if (dmg > 0) {
                baseDamage = dmg;
            }
        }

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            if (State == StateEmerge && StateTimer < OmenFrames) {
                Projectile.Kill();
                return;
            }
            State = StateDissolve;
            StateTimer = 0;
            StateParam = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();

            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * ScytheDamageMul);

            if (State != lastSeenState) {
                lastSeenState = State;
                lastFireTick = -1;
                castReleased = false;
                castDeclared = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!bookInit) {
                RebuildBook(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateScythe: UpdateScythe(owner, authority); break;
                case StateFireball: UpdateFireball(owner, authority); break;
                case StateCircleCast: UpdateCircleCast(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateBook(owner, domain);
            PushBookHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (circleCooldown > 0) {
                circleCooldown--;
            }
            if (castFlash > 0) {
                castFlash--;
            }
            bookRecoil *= 0.78f;
            bookOpen = MathHelper.Lerp(bookOpen, WantedOpen(), 0.18f);

            float glow = BookAlpha() * (0.3f + bookOpen * 0.35f);
            if (glow > 0.02f) {
                Lighting.AddLight(bookPos, 0.55f * glow, 0.18f * glow, 0.1f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        private bool OwnCircleAlive() {
            int type = ModContent.ProjectileType<KikasaPandemoniumCircle>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && proj.owner == Projectile.owner) {
                    return true;
                }
            }
            return false;
        }

        private float WantedOpen()
            => State switch {
                StateScythe => StateTimer >= ScytheFormEnd ? 1f : StateTimer / ScytheFormEnd,
                StateFireball => StateTimer >= FireFormEnd ? 0.85f : 0.2f + StateTimer / FireFormEnd * 0.4f,
                StateCircleCast => StateTimer >= CastDashEnd ? 1f : 0.45f,
                StateEmerge => StateTimer >= FormupFrame ? 0.2f : 0f,
                StateDissolve => 0f,
                _ => 0.12f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Seed) * 0.04f,
            };

        //==================== 出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
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

            if (!breachDone) {
                breachDone = true;
                bookVel = new Vector2(0f, -11.8f);
                bookSpin = 0.26f;
                Projectile.velocity = new Vector2(0f, -3.2f);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.72f,
                    Pitch = -0.38f,
                    MaxInstances = 3
                }, bookPos);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd && t % 3 == 0) {
                Vector2 dropPos = bookPos + new Vector2(
                    Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(2f, 12f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }

            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.48f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                bookVel += new Vector2(0f, -1.1f);
                if (viewed) {
                    for (int k = 0; k < 4; k++) {
                        Dust d = Dust.NewDustPerfect(bookPos + Main.rand.NextVector2Circular(14f, 8f),
                            CWRID.Dust_Brimstone, new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.4f, 1.6f)),
                            80, default, 1.4f);
                        d.noGravity = true;
                    }
                    ShakeViewer(1.6f);
                }
            }

            if (t >= EmergeTotal) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                circleCooldown = CircleCooldownFrames / 2;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

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
            Vector2 anchor = owner.Center + new Vector2(0f, -32f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildBook(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                if (circleCooldown <= 0) {
                    State = StateCircleCast;
                }
                else {
                    State = attackIndex % 2 == 1 ? StateScythe : StateFireball;
                }
                Projectile.netUpdate = authority;
            }
        }

        //==================== 镰刀螺旋 ====================

        private void UpdateScythe(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            if (target < 0 && t <= ScytheFormEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 5f
                : Projectile.Center + new Vector2(owner.direction * 400f, 0f);

            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            Vector2 anchor = owner.Center + toT * 48f + perp * MathF.Sin(t * 0.05f + Seed) * 18f + new Vector2(0f, -26f);
            Vector2 desired = (anchor - Projectile.Center) * 0.11f;
            if (desired.Length() > 13f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 13f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            if (t == 8) {
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.4f,
                    Pitch = -0.45f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            if (t == ScytheRelease && 0 > lastFireTick) {
                lastFireTick = 0;
                ReleaseScythes(owner, authority, focus);
            }

            if (t >= ScytheTotal) {
                EndAttack(authority, OwnCircleAlive() ? 70 : 95);
            }
        }

        private void ReleaseScythes(Player owner, bool authority, Vector2 focus) {
            Vector2 emit = EmitPos();
            Vector2 aim = (focus - emit).SafeNormalize(Vector2.UnitX);
            bookRecoil = 10f;
            bookVel -= aim * 1.2f;
            castFlash = 6;

            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.7f,
                Pitch = -0.45f,
                MaxInstances = 3
            }, emit);
            if (ViewedOwner) {
                ShakeViewer(1.4f);
            }
            if (!Main.dedServ) {
                for (int k = 0; k < 8; k++) {
                    Dust d = Dust.NewDustPerfect(emit, DustID.Blood,
                        aim.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5f),
                        80, Color.OrangeRed, 1.4f);
                    d.noGravity = true;
                }
            }
            if (!authority) {
                return;
            }

            int count = OwnCircleAlive() ? ScytheCount + 1 : ScytheCount;
            int dmg = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * ScytheDamageMul);
            float baseAng = aim.ToRotation();
            for (int k = 0; k < count; k++) {
                float skew = (k - (count - 1) * 0.5f) * 0.26f;
                Vector2 vel = (baseAng + skew).ToRotationVector2() * 12f;
                //弱追踪档走 ai2 随生成包同步（post-spawn 写 localAI 远端看不见追踪）
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), emit, vel,
                    ModContent.ProjectileType<PandemoniumScythe>(), dmg, 2.2f, Projectile.owner,
                    1f, k * 0.5f, 1f);
            }
        }

        //==================== 火球拍击 ====================

        private void UpdateFireball(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            if (target < 0 && t <= FireFormEnd) {
                EndAttack(authority, 50);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : Projectile.Center + new Vector2(owner.direction * 360f, 0f);
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 40f + new Vector2(0f, -24f);
            Vector2 desired = (anchor - Projectile.Center) * 0.12f;
            if (desired.Length() > 13f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 13f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.2f);

            if (t == FireFormEnd) {
                SoundEngine.PlaySound(SoundID.Item20 with {
                    Volume = 0.42f,
                    Pitch = -0.5f,
                    MaxInstances = 2
                }, Projectile.Center);
                bookVel -= toT * 0.8f;
            }

            int extra = OwnCircleAlive() ? 1 : 0;
            for (int k = 0; k < FireBeats.Length; k++) {
                if (t == FireBeats[k] && k > lastFireTick) {
                    lastFireTick = k;
                    ReleaseFireball(owner, authority, focus, k);
                }
            }
            if (extra > 0 && t == FireReleaseC + 8 && 3 > lastFireTick) {
                lastFireTick = 3;
                ReleaseFireball(owner, authority, focus, 3);
            }

            if (t >= FireTotal) {
                EndAttack(authority, OwnCircleAlive() ? 80 : 105);
            }
        }

        /// <summary>
        /// 吐一颗火球。ai0 延迟必须为 0：火球的延迟路径逐帧清零 velocity 后才缓存
        /// targetVelocity，延迟>0 的普通球会冻在原地——错帧感由三拍帧位承担
        /// </summary>
        private void ReleaseFireball(Player owner, bool authority, Vector2 focus, int lane) {
            Vector2 emit = EmitPos();
            float skew = (lane - 1) * 0.12f;
            Vector2 aim = (focus - emit).SafeNormalize(Vector2.UnitY).RotatedBy(skew);
            bookRecoil = 8f;
            bookVel -= aim * 0.9f;
            castFlash = 5;

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.55f,
                Pitch = -0.25f + lane * 0.08f,
                MaxInstances = 4
            }, emit);
            if (!Main.dedServ) {
                Dust d = Dust.NewDustPerfect(emit, DustID.Torch, aim * 3f, 60, Color.OrangeRed, 1.6f);
                d.noGravity = true;
            }
            if (authority) {
                int dmg = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * FireballDamageMul);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), emit, aim * 0.1f,
                    ModContent.ProjectileType<PandemoniumFireball>(), dmg, 2f, Projectile.owner, 0f, 0f);
            }
        }

        //==================== 法阵特招 ====================

        private void UpdateCircleCast(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            if (target < 0 && t <= CastDashEnd) {
                EndAttack(authority, 60);
                return;
            }

            if (!castDeclared) {
                castDeclared = true;
                castPos = target >= 0 ? Main.npc[target].Center : Projectile.Center;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                    Volume = 0.48f,
                    Pitch = -0.15f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            Vector2 anchor = castPos + new Vector2(0f, t <= CastDashEnd ? -150f : -118f);
            float chase = t <= CastDashEnd ? 0.2f : 0.09f;
            Vector2 desired = (anchor - Projectile.Center) * chase;
            if (desired.Length() > 22f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 22f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.24f);

            if (t == CastDashEnd + 4) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with {
                    Volume = 0.55f,
                    Pitch = -0.4f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
            if (!Main.dedServ && t > CastDashEnd && t < CastReleaseFrame && t % 3 == 0) {
                Dust d = Dust.NewDustPerfect(bookPos + Main.rand.NextVector2Circular(16f, 10f),
                    CWRID.Dust_Brimstone, new Vector2(0f, 1.4f), 70, default, 1.5f);
                d.noGravity = true;
            }

            if (t >= CastReleaseFrame && !castReleased) {
                castReleased = true;
                circleCooldown = CircleCooldownFrames;
                bookRecoil = 16f;
                bookVel += new Vector2(0f, -2.6f);
                castFlash = 8;
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                    Volume = 0.7f,
                    Pitch = -0.5f,
                    MaxInstances = 2
                }, castPos);
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.5f,
                    Pitch = -0.2f,
                    MaxInstances = 2
                }, bookPos);
                if (ViewedOwner) {
                    ShakeViewer(2.6f);
                }
                if (authority) {
                    int tick = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * CircleTickMul);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), castPos, Vector2.Zero,
                        ModContent.ProjectileType<KikasaPandemoniumCircle>(), tick, 0f, Projectile.owner, 170f);
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

            if (lakeAlive && !dissolveSplashed && bookPos.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.55f,
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, bookPos);
                if (ViewedOwner) {
                    Vector2 hit = new(bookPos.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 6);
                    KikasaDomainDeco.RippleAt(hit, 0.9f);
                    ShakeViewer(1f);
                }
            }

            if (!Main.dedServ && BookAlpha() > 0.15f && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bookPos + Main.rand.NextVector2Circular(16f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 书体推进 ====================

        private void RebuildBook(KikasaDomainPlayer domain) {
            bookInit = true;
            if (State == StateEmerge) {
                bookPos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 26f);
                bookRot = 0.4f;
            }
            else {
                bookPos = Projectile.Center + new Vector2(0f, -6f);
                bookRot = 0f;
            }
            bookFlip = false;
            bookVel = Vector2.Zero;
            bookSpin = 0f;
            bookRecoil = 0f;
            bookOpen = 0f;
            bookTarget = bookPos;
            for (int k = 0; k < bookOld.Length; k++) {
                bookOld[k] = bookPos;
                bookOldRot[k] = bookRot;
            }
        }

        private void ChaseBook(float accel, float damp) {
            bookVel = (bookVel + (bookTarget - bookPos) * accel) * damp;
            bookPos += bookVel;
        }

        private float Sway(float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed) * amp;

        /// <summary>书体保持立姿，只向猎物侧轻倾，不当枪口瞄准</summary>
        private void TiltToward(Vector2 worldPos, float rate, float maxTilt = 0.38f) {
            float side = MathHelper.Clamp((worldPos.X - bookPos.X) * 0.004f, -maxTilt, maxTilt);
            bookRot = bookRot.AngleLerp(side, rate);
        }

        private void UpdateBook(Player owner, KikasaDomainPlayer domain) {
            if (!bookInit) {
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
                        bookPos = new Vector2(Projectile.Center.X, lakeY + 26f);
                        bookVel = Vector2.Zero;
                        bookTarget = bookPos;
                        bookRot = 0.35f;
                        break;
                    }
                    bookTarget = new Vector2(Projectile.Center.X, lakeY - 92f + Sway(2.1f, 8f));
                    int lt = t - OmenFrames;
                    if (lt < 14) {
                        bookVel.Y *= 0.955f;
                        bookVel.X *= 0.98f;
                        bookPos += bookVel;
                        bookRot += bookSpin;
                        bookSpin *= 0.93f;
                    }
                    else {
                        ChaseBook(0.05f, 0.86f);
                        bookRot += bookSpin;
                        bookSpin *= 0.88f;
                        if (MathF.Abs(bookSpin) < 0.04f) {
                            bookRot = bookRot.AngleLerp(0f, 0.14f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float phase = Main.GlobalTimeWrappedHourly * 0.58f + Seed;
                    Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 22f, MathF.Sin(phase) * 10f - 6f);
                    slot.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Seed * 2f) * 6f;
                    bookTarget = slot;
                    ChaseBook(0.06f, 0.84f);
                    if (target >= 0) {
                        TiltToward(targetPos, 0.12f, 0.28f);
                    }
                    else {
                        bookRot = bookRot.AngleLerp(0f, 0.06f);
                    }
                    break;
                }
                case StateScythe: {
                    Vector2 slot = Projectile.Center + new Vector2(0f, Sway(1.8f, 4f) - 4f);
                    bookTarget = slot;
                    ChaseBook(t < ScytheFormEnd ? 0.12f : 0.08f, 0.8f);
                    TiltToward(targetPos, 0.22f, 0.42f);
                    break;
                }
                case StateFireball: {
                    //拍击前仰、放出时前倾
                    float lean = t < FireFormEnd ? -0.18f : 0.22f;
                    Vector2 slot = Projectile.Center + new Vector2(0f, Sway(2f, 3f));
                    bookTarget = slot;
                    ChaseBook(0.12f, 0.8f);
                    float side = MathHelper.Clamp((targetPos.X - bookPos.X) * 0.004f, -0.36f, 0.36f);
                    bookRot = bookRot.AngleLerp(side + lean * Math.Sign(side == 0 ? 1 : side), 0.2f);
                    break;
                }
                case StateCircleCast: {
                    Vector2 slot = Projectile.Center + new Vector2(Sway(1.5f, 5f), 6f);
                    bookTarget = slot;
                    ChaseBook(0.1f, 0.8f);
                    bookRot = bookRot.AngleLerp(0.55f, 0.16f);
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    bookVel.X *= 0.93f;
                    bookVel.Y = MathF.Min(bookVel.Y + 0.3f, 9.5f);
                    bookRot = bookRot.AngleLerp(0.7f, 0.02f);
                    bookPos += bookVel;
                    bookTarget = bookPos;
                    break;
                }
            }

            if (!skipFix && Vector2.Distance(bookPos, bookTarget) > 780f) {
                bookPos = bookTarget;
                bookVel = Vector2.Zero;
            }

            float sideX = targetPos.X - bookPos.X;
            if (sideX > 18f) {
                bookFlip = false;
            }
            else if (sideX < -18f) {
                bookFlip = true;
            }
        }

        private void PushBookHistory() {
            for (int k = bookOld.Length - 1; k >= 1; k--) {
                bookOld[k] = bookOld[k - 1];
                bookOldRot[k] = bookOldRot[k - 1];
            }
            bookOld[0] = bookPos;
            bookOldRot[0] = bookRot;
        }

        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateScythe or StateFireball or StateCircleCast)) {
                return;
            }
            if (Main.rand.NextBool(16) && BookAlpha() > 0.5f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bookPos + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(6f, 12f)),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(16, 28), 0f);
            }
            if (bookOpen > 0.55f && Main.rand.NextBool(8)) {
                Dust d = Dust.NewDustPerfect(EmitPos() + Main.rand.NextVector2Circular(8f, 6f),
                    CWRID.Dust_Brimstone, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)),
                    90, default, 1.1f);
                d.noGravity = true;
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

        private Vector2 BookDrawPos()
            => bookPos - new Vector2(0f, bookRecoil * 0.35f);

        private Vector2 EmitPos()
            => BookDrawPos() + new Vector2(bookFlip ? -10f : 10f, -4f) + bookRot.ToRotationVector2() * 6f;

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        private float BookAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float BookForm() {
            int t = (int)StateTimer;
            float steady = 0.24f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 0.06f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        private float DissolveAmt() {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp(StateTimer / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed ? 0.15f : 0f), 0f, 1f);
        }

        private float BookScale() {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            //翻开时横向略撑
            scale *= 1f + bookOpen * 0.06f;
            scale *= 1f - bookRecoil * 0.004f;
            return scale * BookDrawScale;
        }

        private float CastCharge() {
            if (State != StateCircleCast) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= CastDashEnd || t >= CastReleaseFrame) {
                return 0f;
            }
            return MathHelper.Clamp((t - CastDashEnd) / (float)(CastReleaseFrame - CastDashEnd), 0f, 1f);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        private SpriteEffects BookFx()
            => bookFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        public override bool PreDraw(ref Color lightColor) {
            if (!bookInit) {
                return false;
            }
            Main.instance.LoadItem(ItemType);
            Texture2D tex = TextureAssets.Item[ItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            DrawDashTrails(sb, tex);
            DrawBody(sb, tex);
            DrawGlow(sb);
            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            float trailA = MathHelper.Clamp((bookVel.Length() - 8f) / 10f, 0f, 1f) * BookAlpha();
            if (trailA <= 0.03f) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            for (int k = bookOld.Length - 1; k >= 1; k--) {
                float fall = 1f - k / (float)bookOld.Length;
                sb.Draw(tex, bookOld[k] - Main.screenPosition, null,
                    BloodMain * (0.26f * fall * trailA), bookOldRot[k],
                    origin, BookScale() * (0.96f - k * 0.015f), BookFx(), 0f);
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

            float alpha = BookAlpha();
            if (alpha > 0.01f) {
                float rot = bookRot;
                Vector2 drawPos = BookDrawPos() - Main.screenPosition;
                float dissolve = DissolveAmt();
                Vector2 origin = tex.Size() * 0.5f;

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = BookScale() * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, BookFx(), 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(BookForm());
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, BookScale(), BookFx(), 0f);
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
            Color brim = new(255, 110, 48);

            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                float r = 20f + 14f * ease;
                sb.Draw(glow, pos - Main.screenPosition, null, brim * (0.32f * ease), 0f,
                    gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            if (State is StateFollow or StateScythe or StateFireball or StateCircleCast) {
                float p = (Main.GlobalTimeWrappedHourly * 0.42f + Seed * 0.13f) % 1f;
                if (p < 0.34f && BookAlpha() > 0.5f) {
                    EnsureBegin();
                    float k = p / 0.34f;
                    float a = MathF.Sin(k * MathHelper.Pi) * 0.28f * BookAlpha();
                    Vector2 pos = BookDrawPos() + new Vector2(MathHelper.Lerp(-16f, 16f, k), 6f);
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * a,
                        MathHelper.PiOver2, gOrigin,
                        new Vector2(16f * 2f / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //翻开页缘的硫磺芯光
            if (bookOpen > 0.25f && BookAlpha() > 0.4f) {
                EnsureBegin();
                sb.Draw(glow, EmitPos() - Main.screenPosition, null,
                    brim * (0.28f * bookOpen * BookAlpha()), 0f, gOrigin,
                    new Vector2((10f + 10f * bookOpen) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            float charge = CastCharge();
            if (charge > 0.05f) {
                EnsureBegin();
                float ringR = MathHelper.Lerp(20f, 68f, charge);
                sb.Draw(glow, castPos - Main.screenPosition, null, brim * (0.32f * charge), 0f,
                    gOrigin, new Vector2(ringR * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, EmitPos() - Main.screenPosition, null,
                    new Color(255, 160, 70) * (0.5f * charge), 0f,
                    gOrigin, new Vector2((8f + 14f * charge) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (castFlash > 0) {
                EnsureBegin();
                float a = castFlash / 5f;
                sb.Draw(glow, EmitPos() - Main.screenPosition, null,
                    new Color(255, 180, 80) * (0.5f * a), 0f,
                    gOrigin, new Vector2(28f * 2f / glow.Width, 12f / glow.Height), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !bookInit) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bookPos + Main.rand.NextVector2Circular(16f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }
            for (int k = 0; k < 6; k++) {
                Dust d = Dust.NewDustPerfect(bookPos, CWRID.Dust_Brimstone,
                    Main.rand.NextVector2Circular(2.4f, 2.4f), 70, default, 1.5f);
                d.noGravity = true;
            }
        }
    }
}
