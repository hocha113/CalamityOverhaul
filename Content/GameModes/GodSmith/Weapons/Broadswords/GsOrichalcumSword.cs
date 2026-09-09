using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【花瓣风暴】材质：山铜粉晶淬火的花刃。
    /// 签名：①每一挥沿刀弧撒出花瓣弹幕，先飘落打旋、再俯冲咬向近旁猎物（呼应山铜盔甲）
    /// ②终结拍舞袖回旋，整捧四瓣齐撒
    /// ③拍表走「舞袖」语汇：后摆最小、跟进最大，挥音下衬花瓣簌簌
    /// </summary>
    internal class GsOrichalcumSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.OrichalcumSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsOrichalcumSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: each swing scatters drifting petals along the arc that flutter, then dive at nearby prey; the finisher looses a full bloom";
        internal static readonly Color BloomBright = new(255, 168, 210); //粉瓣亮
        internal static readonly Color BloomMain = new(232, 96, 160);    //山铜粉
        internal static readonly Color BloomHot = new(255, 214, 236);    //盛放粉白

        //预算账：拍均 (0.95+0.95+1.2)/3≈1.03；花瓣 2/2/4 枚 ×0.12x 追击
        //（散射后单体实取约半数 → +0.16/拍）；连段总帧 (20+19+27)=66 ≈ 原版 66 →
        //综合单体 DPS ≈ 1.03+0.16 ≈ 原版 105%~119%（瓣群散射的多目标覆盖另计），底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 花瓣风暴手持：三拍舞袖剑（RaiseBack 全族最小、Follow 全族最大，弧线连绵不断）。
    /// 每拍斩切爆发沿弧撒瓣，终结拍四瓣齐撒。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsOrichalcumSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.OrichalcumSword;
        protected override Color EdgeBright => GsOrichalcumSword.BloomBright;
        protected override Color BodyMain => GsOrichalcumSword.BloomMain;
        protected override Color HotAccent => GsOrichalcumSword.BloomHot;

        private bool petalsFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 舞袖顺斩：小后摆大跟进，弧线过身
            0 => new GsBroadBeat {
                Raise = 6, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.5f, Follow = 1.35f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.05f,
            },
            //拍1 返袖
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.45f, Follow = 1.4f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
            },
            //拍2 盛放回旋：跟进最深，整捧撒瓣
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 5, Recover = 13,
                RaiseBack = 1.9f, Follow = 1.6f, ReachScale = 1.12f, LeanAmp = 0.075f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.08f,
            },
        };

        /// <summary>挥音下衬花瓣簌簌</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.32f, Pitch = 0.35f, MaxInstances = 3 }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.3f, Pitch = 0.25f }, Owner.Center);
            }
        }

        /// <summary>沿本次挥弧撒瓣：普通拍两枚、终结拍四枚，均匀铺在弧上外抛</summary>
        protected override void OnSlashBegin() {
            if (petalsFired) {
                return;
            }
            petalsFired = true;
            int count = IsFinisher ? 4 : 2;
            int petalDamage = Math.Max(1, (int)(Projectile.damage * 0.12f));
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, (i + 1f) / (count + 1f));
                Vector2 dir = ang.ToRotationVector2();
                //切向外抛带一点上飘，随后进入飘落段
                Vector2 vel = dir * Main.rand.NextFloat(4.5f, 6f) + new Vector2(0f, -1.2f);
                SpawnOwnedProj(ModContent.ProjectileType<GsOrichalcumSwordPetalProj>(),
                    Hand + dir * (FullReach * 0.7f), vel, petalDamage, Projectile.knockBack * 0.2f,
                    i % 2 == 0 ? 1f : -1f, i);
            }
        }

        /// <summary>命中：花瓣簌落柔响（与金属剑的脆响区分）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 追击花瓣：沿挥弧撒出，用原版山铜花瓣贴图。先 14 帧飘落打旋（轻重力+横向摇曳），
    /// 再锁定 420 像素内猎物俯冲咬去，速度随俯冲渐升；无猎物则继续飘散。
    /// ai[0]=自旋方向 ai[1]=瓣序（错开摇曳相位）
    /// </summary>
    internal class GsOrichalcumSwordPetalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;
        public override LocalizedText DisplayName => Language.GetText("ItemName.OrichalcumSword");

        private ref float Life => ref Projectile.localAI[0];
        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float SwayPhase => Projectile.ai[1] * 1.7f;

        public override void SetStaticDefaults() {
            //原版花瓣贴图是竖排三帧，不声明帧数会整条竖图当一片瓣画
            Main.projFrames[Type] = Main.projFrames[ProjectileID.FlowerPetal];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            if (Life <= 14f) {
                //飘落段：轻重力 + 横向摇曳，像真花瓣旋落
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y += 0.05f;
                Projectile.velocity.X += MathF.Sin(Life * 0.5f + SwayPhase) * 0.16f;
                Projectile.rotation += SpinDir * 0.3f;
            }
            else {
                //俯冲段：咬向最近猎物，越追越快；无猎物继续飘
                NPC target = Projectile.Center.FindClosestNPC(420f);
                if (target != null) {
                    float chase = MathF.Min(6f + (Life - 14f) * 0.2f, 12f);
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * chase;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);
                    Projectile.rotation += SpinDir * 0.42f;
                }
                else {
                    Projectile.velocity *= 0.97f;
                    Projectile.velocity.Y += 0.04f;
                    Projectile.velocity.X += MathF.Sin(Life * 0.45f + SwayPhase) * 0.12f;
                    Projectile.rotation += SpinDir * 0.26f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.25f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
