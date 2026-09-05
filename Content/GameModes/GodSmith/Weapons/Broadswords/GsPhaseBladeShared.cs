using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 光剑族色板：刃缘/体色/过热三色（喂基类抽象色板）+ 本色音高偏移（七色的次要变奏载体）
    /// </summary>
    internal readonly struct GsPhasebladePalette
    {
        /// <summary>刃缘亮色</summary>
        public readonly Color Edge;
        /// <summary>束刃体色</summary>
        public readonly Color Body;
        /// <summary>过热白炽色</summary>
        public readonly Color Hot;
        /// <summary>本色音高偏移（每色的微小 rider）</summary>
        public readonly float Pitch;

        public GsPhasebladePalette(Color edge, Color body, Color hot, float pitch) {
            Edge = edge;
            Body = body;
            Hot = hot;
            Pitch = pitch;
        }

        public static readonly GsPhasebladePalette Blue = new(new(165, 220, 255), new(70, 140, 255), new(235, 250, 255), -0.04f);
        public static readonly GsPhasebladePalette Red = new(new(255, 160, 150), new(255, 75, 85), new(255, 235, 225), -0.08f);
        public static readonly GsPhasebladePalette Green = new(new(170, 255, 180), new(85, 235, 110), new(240, 255, 235), 0f);
        public static readonly GsPhasebladePalette Purple = new(new(215, 165, 255), new(170, 85, 255), new(250, 240, 255), -0.12f);
        public static readonly GsPhasebladePalette White = new(new(245, 248, 255), new(205, 216, 235), new(255, 255, 255), 0.10f);
        public static readonly GsPhasebladePalette Yellow = new(new(255, 240, 160), new(255, 218, 70), new(255, 255, 235), 0.06f);
        public static readonly GsPhasebladePalette Orange = new(new(255, 200, 140), new(255, 150, 55), new(255, 240, 220), 0.02f);
        /// <summary>兜底色板（方案查询失败时的中性白蓝）</summary>
        public static readonly GsPhasebladePalette Fallback = new(new(230, 240, 255), new(160, 190, 235), new(255, 255, 255), 0f);
    }

    /// <summary>
    /// 【光剑族方案核心】材质：等离子束刃（核心炽白线 + 色罩束鞘 + 端头收口光点）。
    /// 族签名「充能弧」：①能量刃命中积攒充能，刃身随充能延展变亮、嗡鸣升调
    /// ②满充能后下一次终结拍放电，甩出一道等离子光弧③七色共享机制，色板与音高是次要变奏。<br/>
    /// 联机纪律：方案单例跨玩家共享，充能只在 myPlayer 守门路径读写，
    /// 远端靠手持弹幕 ai[2] 看到同一场延展与放电
    /// </summary>
    internal abstract class GsPhasebladeSchemeCore : GsBroadswordScheme
    {
        /// <summary>族色板（七色子类指定，与手持侧同一静态实例）</summary>
        internal abstract GsPhasebladePalette Palette { get; }

        /// <summary>充能上限（Phasesaber 档更高）</summary>
        internal virtual int ChargeMax => 5;

        /// <summary>当前充能；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Charge;

        protected override string GsDescFallback =>
            "Reforged: a plasma edge that charges as it cuts; every hit extends the blade, and at full charge the next finishing slash hurls a plasma arc";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 【光剑（Phasesaber）档方案核心】精制等离子刃：充能上限更高、刃长延展更远；
    /// 终结拍必有小光刃延展斩，满充能时升级为过载光弧，命中再炸开等离子爆裂
    /// </summary>
    internal abstract class GsPhasesaberSchemeCore : GsPhasebladeSchemeCore
    {
        internal override int ChargeMax => 6;

        protected override string GsDescFallback =>
            "Reforged: a perfected plasma saber; hits overcharge the blade for greater reach, every finishing slash casts a short energy arc, and a full charge unleashes an overloaded arc that bursts on impact";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 光剑族手持核心：三拍连段（顺斩/返斩/终结轮斩）。
    /// 充能出手时由 owner 写入 ai[2] 随包过线，全端一致地延展刃长、抬升音调。
    /// ai[0]=拍号 ai[1]=交替符号 ai[2]=本次挥砍充能数
    /// </summary>
    internal abstract class GsPhasebladeHeldCore : GsBroadswordHeldBase
    {
        /// <summary>族色板（与方案侧同一静态实例）</summary>
        protected abstract GsPhasebladePalette Palette { get; }

        protected sealed override Color EdgeBright => Palette.Edge;
        protected sealed override Color BodyMain => Palette.Body;
        protected sealed override Color HotAccent => Palette.Hot;

        //==================== 档位参数（Phasesaber 档重写） ====================

        /// <summary>每层充能的刃长延展</summary>
        protected virtual float ReachPerCharge => 0.045f;
        /// <summary>光弧伤害系数（对当前拍伤害，终结拍 1.05x 后约合底伤 0.5x）</summary>
        protected virtual float ArcDamageFactor => 0.48f;
        /// <summary>放电甩出的光弧档位（Phasesaber 档 2：更大且命中爆裂）</summary>
        protected virtual float OverloadTier => 1f;

        protected override float BaseReach => 108f;

        /// <summary>充能上限缓存（出手时从方案侧取，单一事实源）</summary>
        protected int chargeMaxCache = 5;
        private bool arcFired;

        /// <summary>本次挥砍的充能数（ai[2] 随包过线，全端一致）</summary>
        protected int ChargeAtSwing => Math.Clamp((int)Projectile.ai[2], 0, chargeMaxCache);
        /// <summary>是否满充放电斩（终结拍 + 满充能）</summary>
        protected bool OverloadSwing => IsFinisher && ChargeAtSwing >= chargeMaxCache;

        /// <summary>方案实例（充能记账用；跨玩家共享，只在 myPlayer 路径写）</summary>
        protected GsPhasebladeSchemeCore Core =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsPhasebladeSchemeCore : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩：轻快平抹，能量刃没有钢铁的惯性
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.85f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
            },
            //拍1 返斩：更短的回手
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.042f,
                DamageMult = 0.85f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.08f,
            },
            //拍2 终结轮斩：小前压，满充能时在此放电
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.12f,
            },
        };

        /// <summary>出手时 owner 把方案侧充能写进 ai[2]；生成包不含本值，补一发同步（远端只用于演出）</summary>
        protected override void OnStageInit() {
            chargeMaxCache = Core?.ChargeMax ?? 5;
            if (Owner.whoAmI == Main.myPlayer) {
                Projectile.ai[2] = Math.Clamp(Core?.Charge ?? 0, 0, chargeMaxCache);
                Projectile.netUpdate = true;
            }
        }

        /// <summary>充能延展刃长：全端从 ai[2] 推导，几何逐帧重算</summary>
        protected override void UpdateBladeTransform(int phase) {
            reachScale = Beat.ReachScale * (1f + ReachPerCharge * ChargeAtSwing);
            base.UpdateBladeTransform(phase);
        }

        /// <summary>能量嗡鸣：Item15 相位嗡鸣 + Item1 切风；充能抬升音调，色板再偏移</summary>
        protected override void PlaySwingSound() {
            float pitch = Beat.SwingPitch + Palette.Pitch + 0.05f * ChargeAtSwing;
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.72f, Pitch = pitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = pitch + 0.12f }, Owner.Center);
            if (OverloadSwing) {
                //放电斩：低鸣加厚 + 一记电浆哨音
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = pitch - 0.5f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = 0.2f + Palette.Pitch }, Owner.Center);
            }
        }

        /// <summary>终结拍放电：满充能甩出等离子光弧并清空充能；未满充能走档位钩子</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || arcFired) {
                return;
            }
            arcFired = true;
            if (!OverloadSwing) {
                OnFinisherWithoutCharge();
                return;
            }
            //清账只在 myPlayer；远端靠 ai[2] 看到同一场放电
            if (Owner.whoAmI == Main.myPlayer && Core != null) {
                Core.Charge = 0;
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * ArcDamageFactor));
            SpawnOwnedProj(ModContent.ProjectileType<GsPhasebladeArcProj>(),
                Hand + dir * (FullReach * 0.9f), dir * 12f, dmg, Projectile.knockBack * 0.5f,
                swingDir, OverloadTier, SwordItemID);
        }

        /// <summary>未满充能的终结拍追加（Phasesaber 档甩小光刃）</summary>
        protected virtual void OnFinisherWithoutCharge() { }

        /// <summary>命中记账：等离子灼蚀短促电嘶；非放电斩每个目标 +1 充能，攒满一记升调提示音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.22f, Pitch = 0.5f + Palette.Pitch }, target.Center);
            }
            if (Owner.whoAmI != Main.myPlayer || OverloadSwing) {
                return;
            }
            GsPhasebladeSchemeCore core = Core;
            if (core == null || core.Charge >= core.ChargeMax) {
                return;
            }
            core.Charge++;
            if (core.Charge >= core.ChargeMax) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.25f + Palette.Pitch }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 光剑（Phasesaber）档手持核心：刃长延展更远；
    /// 未满充能的终结拍甩小光刃延展斩，满充过载弧命中炸开等离子爆裂
    /// </summary>
    internal abstract class GsPhasesaberHeldCore : GsPhasebladeHeldCore
    {
        protected override float ReachPerCharge => 0.05f;
        protected override float OverloadTier => 2f;
        protected override float BaseReach => 120f;
        protected override float CollisionWidth => 44f;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩：比 Phaseblade 多半分权威
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.02f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.1f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.06f,
            },
            //拍2 终结重轮斩：更长的举、更深的前压，小光刃/过载弧都在此出手
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.16f,
            },
        };

        /// <summary>档位签名：未满充能的终结拍也送出小光刃延展斩</summary>
        protected override void OnFinisherWithoutCharge() {
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.24f));
            SpawnOwnedProj(ModContent.ProjectileType<GsPhasebladeArcProj>(),
                Hand + dir * (FullReach * 0.85f), dir * 9f, dmg, Projectile.knockBack * 0.35f,
                swingDir, 0f, SwordItemID);
        }
    }

    /// <summary>
    /// 等离子光弧：光剑族终结放电。减速滑行的短程刃外延伸。
    /// ai[0]=挥动符号 ai[1]=档位（0 小光刃 / 1 满充弧 / 2 过载弧）ai[2]=本体物品 ID（爆裂查方案取音高）。
    /// 过载弧首个命中炸开等离子爆裂
    /// </summary>
    internal class GsPhasebladeArcProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SwordBeam;

        private int Tier => Math.Clamp((int)Projectile.ai[1], 0, 2);
        private ref float Life => ref Projectile.localAI[0];
        private ref float BurstSpent => ref Projectile.localAI[1];

        /// <summary>按本体物品 ID 查方案取色板，查不到用兜底白</summary>
        internal static GsPhasebladePalette PaletteFor(int itemID)
            => GodSmithScheme.TryGetScheme(itemID, out GodSmithScheme s) && s is GsPhasebladeSchemeCore core
                ? core.Palette : GsPhasebladePalette.Fallback;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 26;
        }

        public override void AI() {
            if (Life == 0f) {
                //档位就位（ai[1] 随生成包过线，各端一致）：小光刃短寿少穿，过载弧多穿
                if (Tier == 0) {
                    Projectile.timeLeft = 18;
                    Projectile.penetrate = 2;
                }
                else if (Tier == 2) {
                    Projectile.penetrate = 4;
                }
            }
            Life++;
            //减速滑行：12 → 约 4，光弧是刃外延伸不是远程光束
            Projectile.velocity *= 0.95f;
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //过载弧：首个命中炸开等离子爆裂（owner 端生成，随包过线）
            if (Tier == 2 && BurstSpent == 0f && Projectile.owner == Main.myPlayer) {
                BurstSpent = 1f;
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsPhasesaberBurstProj>(), dmg, Projectile.knockBack * 0.6f,
                    Projectile.owner, Projectile.ai[2]);
            }
        }
    }

    /// <summary>
    /// 等离子爆裂：过载光弧命中炸开的小范围二段。6 帧过冲撑到满径后回坐，
    /// 伤害只在扩张期结算一次；ai[0]=本体物品 ID（取音高）
    /// </summary>
    internal class GsPhasesaberBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HallowStar;

        private const int TotalLife = 18;
        private const float MaxRadius = 82f;
        private ref float Life => ref Projectile.localAI[0];
        private GsPhasebladePalette Pal => GsPhasebladeArcProj.PaletteFor((int)Projectile.ai[0]);

        /// <summary>当前扩张半径：6 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 6f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.5f, Pitch = -0.25f + Pal.Pitch }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 7f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外
    }
}
