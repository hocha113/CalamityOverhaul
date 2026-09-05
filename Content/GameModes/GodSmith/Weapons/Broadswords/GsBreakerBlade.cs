using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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
            "Reforged: a two-beat wrecking rhythm; deals 2.5x damage to foes above 90% life, and landing that opening blow shatters their guard in an armor-breaking shockwave";
        internal static readonly Color SlagBright = new(218, 208, 194); //石灰刃缘
        internal static readonly Color SlagMain = new(152, 140, 126);   //渣铁体色
        internal static readonly Color SlagHot = new(255, 150, 58);     //熔渣灼橙

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
            SpawnOwnedProj(ModContent.ProjectileType<GsBreakerBladeShockProj>(),
                target.Center, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.35f)),
                Projectile.knockBack * 0.9f);
            if (!VaultUtils.isServer) {
                //专属重音：攻城锤砸地 + 铁甲碎响
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.25f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 碎甲冲击波：破幕一击命中处的扩张震圈。半径 9 帧过冲撑满后回坐，
    /// 伤害只在扩张期结算一次，击退向外；用原版爆炸陷阱爆炸贴图按半径缩放画一笔作范围提示
    /// </summary>
    internal class GsBreakerBladeShockProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2ExplosiveTrapT1Explosion;

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

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.DD2ExplosiveTrapT1Explosion];
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

        public override void AI() => Life++;

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 10f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>范围提示：原版爆炸贴图按当前半径缩放画一笔，帧序随寿命推进，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frames = Math.Max(1, Main.projFrames[Type]);
            Rectangle frame = tex.Frame(1, frames, 0, Math.Min(frames - 1, (int)(Life01 * frames)));
            float scale = Radius * 2f / MathF.Max(frame.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (1f - Life01),
                0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
