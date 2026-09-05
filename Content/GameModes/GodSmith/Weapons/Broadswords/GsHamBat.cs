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
    /// 【饱食之力·火腿棒】材质：老饕圣物级的蜜汁大火腿，油脂就是它的刃。
    /// 签名：①按饱食档位增伤 +5%/+10%/+15%（原版特性原样保留）
    /// ②终结拍命中炸开「香气冲击」小范围波（食物系音效）
    /// </summary>
    internal class GsHamBat : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.HamBat;

        protected override int HeldProjID => ModContent.ProjectileType<GsHamBatHeld>();

        protected override string GsDescFallback =>
            "Reforged: the power of satiety; swings grow greasier and mightier with your Well Fed tier (+5%/+10%/+15% damage), and the finishing beat splatters meat scraps with a burst of savory aroma";
        internal static readonly Color HamGlaze = new(255, 226, 196);  //蜜汁高光
        internal static readonly Color HamPink = new(232, 128, 110);   //火腿粉体色
        internal static readonly Color RoastAmber = new(255, 176, 96); //炙烤琥珀

        //底伤不加成（原版 57/20f、scale1.2 的大肉腿）：三拍 1.0/1.0/1.25x 按 66 帧循环摊算约原版 110%；
        //饱食增伤与原版逐字等效（WellFed 档位 +5%/+10%/+15%，走命中乘区，双方同吃同涨）；
        //终结拍香气冲击 0.3x 小 AoE 摊进循环约 +3%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 饱食之力手持：三拍肉感重击。0 抡腿 / 1 回抡 / 2 满膛全力挥（前压+香气冲击）。
    /// 饱食档位越高增伤越足。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsHamBatHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.HamBat;
        protected override Color EdgeBright => GsHamBat.HamGlaze;
        protected override Color BodyMain => GsHamBat.HamPink;
        protected override Color HotAccent => GsHamBat.RoastAmber;

        //大肉腿触距（原版 scale 1.2）
        protected override float BaseReach => 126f;
        protected override float CollisionWidth => 44f;

        /// <summary>一拍只炸一次香气</summary>
        private bool aromaFired;

        /// <summary>饱食档位 0~3（buff 各端同步，读取无需守门）</summary>
        private int SatietyTier
            => Owner.HasBuff(BuffID.WellFed3) ? 3
             : Owner.HasBuff(BuffID.WellFed2) ? 2
             : Owner.HasBuff(BuffID.WellFed) ? 1 : 0;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 抡腿
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.15f,
            },
            //拍1 回抡
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.95f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.055f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍2 满膛全力挥：前压
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.3f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.09f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.35f,
            },
        };

        //==================== 老饕演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //全力挥的厚风声
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>饱食增伤：与原版逐字等效（+5%/+10%/+15% 按 WellFed 档位）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            int tier = SatietyTier;
            if (tier > 0) {
                modifiers.SourceDamage *= 1f + 0.05f * tier;
            }
        }

        /// <summary>终结拍命中：每击一记啃咬脆响，香气冲击一拍只炸一次</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsFinisher) {
                return;
            }
            if (!VaultUtils.isServer) {
                //啃一大口的脆响
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
            }
            if (aromaFired) {
                return;
            }
            aromaFired = true;
            SpawnOwnedProj(ModContent.ProjectileType<GsHamBatAromaProj>(),
                target.Center, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.3f)),
                Projectile.knockBack * 1.1f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.85f, Pitch = -0.15f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 香气冲击：终结拍命中处炸开的小范围肉香波。半径 8 帧过冲撑满后回坐，
    /// 伤害只在扩张期结算一次、击退向外；用原版泡泡贴图按半径画一笔作范围提示
    /// </summary>
    internal class GsHamBatAromaProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int TotalLife = 24;
        private const float MaxRadius = 92f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 6% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.06f * (p / 0.7f) : MathHelper.Lerp(1.06f, 1f, (p - 0.7f) / 0.3f);
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
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            }
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>圆形判定：香气笼罩范围内即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//香气把人熏开

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
