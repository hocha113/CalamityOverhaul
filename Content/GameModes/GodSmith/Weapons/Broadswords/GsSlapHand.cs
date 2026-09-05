using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【天罚之掌】材质：神像掌骨蒙金皮的巨掌，掌心有拍痕。
    /// 签名：①原版身份保留：离谱击退与原版拍击粒子（含 Item175 拍声）照旧，
    /// 击退严格随出手向 ②终结拍「掌颂」：双掌合十，掌间震出小范围冲击波，滑稽而虔诚
    /// </summary>
    internal class GsSlapHand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.SlapHand;

        protected override int HeldProjID => ModContent.ProjectileType<GsSlapHandHeld>();

        protected override string GsDescFallback =>
            "Reforged: a three-beat divine slapping; every hit stamps a giant palm print with absurd knockback along the swing, and the finisher claps both palms together, ringing out a small shockwave";
        internal static readonly Color PalmBright = new(255, 226, 178); //金皮亮缘
        internal static readonly Color PalmMain = new(236, 164, 118);   //掌肉体色
        internal static readonly Color PalmHot = new(255, 238, 120);    //天罚金光

        //拍表 1.0/1.0/1.25 均摊 ~1.08x，三拍循环 ~64 帧对原版 20 帧/斩 帧效率 ~0.94x，
        //掌颂冲击波 0.4x 只在终结拍出 → 综合 DPS 约为原版 102%~114%；
        //击退是身份：底伤 +4%，击退再 +15%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.04f;

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.15f;
    }

    /// <summary>
    /// 天罚之掌手持：三拍掌击。0 正手掌 / 1 反手掌（宽扁小弧带前压推步）/
    /// 2 掌颂（长举合十，斩切瞬间掌间震出冲击波）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsSlapHandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.SlapHand;
        protected override Color EdgeBright => GsSlapHand.PalmBright;
        protected override Color BodyMain => GsSlapHand.PalmMain;
        protected override Color HotAccent => GsSlapHand.PalmHot;

        //推掌几何：触及短、判定极宽（一巴掌糊过去的面）
        protected override float BaseReach => 98f;
        protected override float CollisionWidth => 60f;
        protected override float PointBlankRadius => 52f;
        protected override float BladePark => 0.5f;

        private bool clapFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正手掌：小后摆宽推，带步
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.3f, Follow = 0.7f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 1.3f, SwingPitch = 0.2f,
            },
            //拍1 反手掌
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.35f, Follow = 0.72f, ReachScale = 1f, LeanAmp = 0.055f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 1.3f, SwingPitch = 0.34f,
            },
            //拍2 掌颂：长举合十、滞一息、震出
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 14,
                RaiseBack = 1.6f, Follow = 0.8f, ReachScale = 1.08f, LeanAmp = 0.08f,
                DamageMult = 1.25f, Hitstop = 3, LungeSpeed = 2.4f, SwingPitch = -0.1f,
            },
        };

        /// <summary>掌颂爆发：掌间震出冲击波</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || clapFired) {
                return;
            }
            clapFired = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            int clapDamage = Math.Max(1, (int)(Projectile.damage * 0.4f));
            SpawnOwnedProj(ModContent.ProjectileType<GsSlapHandClapProj>(),
                Hand + dir * (FullReach * 0.55f), Vector2.Zero, clapDamage,
                Projectile.knockBack * 0.8f);
        }

        /// <summary>击退身份：命中再补一成五击退，终结拍更狠（方向已由基类钉死随出手向）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.Knockback *= IsFinisher ? 1.3f : 1.15f;

        /// <summary>命中记账（owner 端）：原版拍击粒子广播（内含 Item175 拍声）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            ParticleOrchestraSettings settings = new() { PositionInWorld = target.Center };
            ParticleOrchestrator.RequestParticleSpawn(clientOnly: false,
                ParticleOrchestraType.SlapHand, settings, Owner.whoAmI);
        }

        protected override void PlaySwingSound() {
            //掌风比刀风闷
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = Beat.SwingPitch - 0.3f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item175 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 掌颂冲击波：合十震出的环形冲击。8 帧过冲撑到满径，伤害只在扩张期结算一次，
    /// 击退向外且加重。首帧一记厚拍声；用原版泡泡贴图按半径画一笔作范围提示
    /// </summary>
    internal class GsSlapHandClapProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int TotalLife = 22;
        private const float MaxRadius = 116f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
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
                //合十：一记厚拍 + 空气嗡响
                SoundEngine.PlaySound(SoundID.Item175 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.15f }, Projectile.Center);
            }
        }

        //伤害只在扩张期结算一次
        public override bool? CanDamage() => Life <= 9f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        /// <summary>击退向外并加重：被掌颂震飞是这把武器的教义</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);
            modifiers.Knockback *= 1.4f;
        }

        /// <summary>范围提示：原版泡泡贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - Life01),
                0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
