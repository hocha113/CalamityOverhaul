using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【开幕重锤·毁灭者巨刃】材质：肉山熔渣锻的攻城巨刃，专治满血的嚣张。
    /// 签名：①对生命高于九成的目标伤害 ×2.5（原版特性原样保留）②破幕一击：满血目标被这一记
    /// 砸中时炸开碎甲冲击波+攻城重音 ③两拍重劈型拍表，第二拍前压追击、顿帧更狠
    /// </summary>
    internal class GsBreakerBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BreakerBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsBreakerBladeHeld>();

        protected override int ComboBeats => 2;

        protected override string GsDescFallback =>
            "Reforged: a two-beat wrecking rhythm; deals 2.5x damage to foes above 90% life, " +
            "and landing that opening blow shatters their guard in an armor-breaking shockwave";

        //熔渣攻城色板
        internal static readonly Color SlagBright = new(218, 208, 194); //石灰刃缘
        internal static readonly Color SlagMain = new(152, 140, 126);   //渣铁体色
        internal static readonly Color SlagHot = new(255, 150, 58);     //熔渣灼橙
        internal static readonly Color SlagDeep = new(22, 18, 14);      //焦黑垫影

        //底伤不加成（2.5 倍开幕是本体强项）：两拍 1.0/1.25x 按 60 帧循环摊算持续单体约原版 112%，
        //对 ≥90% 血量目标的 ×2.5 与原版逐字等效；破幕冲击波 0.35x 是满血命中才有的一次性 AoE，
        //不进持续 DPS，只当开幕收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 开幕重锤手持：两拍重劈。0 破幕竖劈 / 1 追击反手重劈（前压+重顿帧）。
    /// 满血目标（生命 ≥90%）吃 2.5 倍伤害并触发碎甲冲击波。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBreakerBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BreakerBlade;
        protected override int BeatCount => 2;
        protected override Color EdgeBright => GsBreakerBlade.SlagBright;
        protected override Color BodyMain => GsBreakerBlade.SlagMain;
        protected override Color HotAccent => GsBreakerBlade.SlagHot;
        protected override Color DeepShadow => GsBreakerBlade.SlagDeep;

        //攻城巨刃触距与判宽
        protected override float BaseReach => 132f;
        protected override float CollisionWidth => 48f;

        /// <summary>本次挥砍已见到满血目标（ModifyHitExtra 写，OnHitTarget 消费触发冲击波）</summary>
        private bool openerPrimed;
        /// <summary>冲击波一拍只放一次</summary>
        private bool shockFired;

        /// <summary>两拍重劈：破幕竖劈 / 追击反手重劈</summary>
        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 破幕竖劈
            0 => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.3f, Follow = 1.05f, ReachScale = 1.1f, LeanAmp = 0.075f,
                DamageMult = 1.0f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.3f,
            },
            //拍1 追击反手重劈：前压、顿帧更狠
            _ => new GsBroadBeat {
                Raise = 10, Hold = 3, Slash = 5, Recover = 13,
                RaiseBack = 2.45f, Follow = 1.15f, ReachScale = 1.12f, LeanAmp = 0.09f,
                DamageMult = 1.25f, Hitstop = 3, LungeSpeed = 2.8f, SwingPitch = -0.42f,
            },
        };

        //==================== 攻城演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            //巨刃破风的低鸣
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = IsFinisher ? -0.5f : -0.35f }, Owner.Center);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //斩切期甩出熔渣火星与石屑
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.Stone,
                    (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f),
                    60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        /// <summary>满血目标吃 2.5 倍（原版等效），并把破幕标记递给命中钩子</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.life >= target.lifeMax * 0.9f) {
                modifiers.SourceDamage *= 2.5f;
                openerPrimed = true;
            }
        }

        /// <summary>破幕一击：满血目标命中处炸开碎甲冲击波 + 攻城重音（一拍一次）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!openerPrimed) {
                return;
            }
            openerPrimed = false;
            if (shockFired) {
                return;
            }
            shockFired = true;
            SetFlash(6);
            SpawnOwnedProj(ModContent.ProjectileType<GsBreakerBladeShockProj>(),
                target.Center, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.35f)),
                Projectile.knockBack * 0.9f);
            if (!VaultUtils.isServer) {
                //专属重音：攻城锤砸地 + 铁甲碎响
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.25f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //渣铁重击：橙热火星向下砸溅
            int sparks = IsFinisher ? 5 : 3;
            for (int i = 0; i < sparks; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(1f, 4f)),
                    Main.rand.NextBool() ? GsBreakerBlade.SlagHot : GsBreakerBlade.SlagBright,
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>
    /// 碎甲冲击波：破幕一击命中处的扩张震圈。半径 9 帧过冲撑满后回坐，
    /// 伤害只在扩张期结算一次，击退向外；石屑上抛、径向裂纹闪现。
    /// 绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsBreakerBladeShockProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 20;
        private const float MaxRadius = 110f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：9 帧过冲 7% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 9f, 0f, 1f);
                float burst = p < 0.7f ? 1.07f * (p / 0.7f) : MathHelper.Lerp(1.07f, 1f, (p - 0.7f) / 0.3f);
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
                //爆心：石屑上抛 + 熔渣火星四散
                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.Stone, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-6f, -2f)),
                        60, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = false;
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                        Main.rand.NextBool(3) ? GsBreakerBlade.SlagBright : GsBreakerBlade.SlagHot,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 22));
                }
            }
            Lighting.AddLight(Projectile.Center, GsBreakerBlade.SlagHot.ToVector3() * (0.7f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 10f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || star == null || flare == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //爆心闪 + 缓旋光斑
            Color flash = GsBreakerBlade.SlagHot * (0.7f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flash, SegRand(9) * 6.28f + Life * 0.04f,
                flare.Size() * 0.5f, 0.4f, SpriteEffects.None, 0);

            //径向裂纹：六条石灰细线自爆心射出，扩张期最亮
            float crack = MathHelper.Clamp(1.2f - Life01 * 1.6f, 0f, 1f);
            if (crack > 0.01f) {
                for (int i = 0; i < 6; i++) {
                    float ang = (MathHelper.TwoPi * i / 6f) + SegRand(i) * 0.6f;
                    Color cl = GsBreakerBlade.SlagBright * (0.5f * crack);
                    cl.A = 0;
                    Main.EntitySpriteDraw(star, center + (ang.ToRotationVector2() * radius * 0.45f), null, cl, ang + MathHelper.PiOver2,
                        star.Size() * 0.5f, new Vector2(0.05f, radius / star.Height * 0.9f), SpriteEffects.None, 0);
                }
            }

            //扩张震圈：石灰光珠沿当前半径排布，橙热脉动
            int beads = 14;
            for (int i = 0; i < beads; i++) {
                float ang = (MathHelper.TwoPi * i / beads) + SegRand(i + 30) * 0.4f;
                Vector2 at = center + (ang.ToRotationVector2() * radius);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(i + 60) * 6.28f);
                Color bead = Color.Lerp(GsBreakerBlade.SlagBright, GsBreakerBlade.SlagHot, SegRand(i + 90)) * (0.5f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.2f + 0.08f * SegRand(i + 120), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
