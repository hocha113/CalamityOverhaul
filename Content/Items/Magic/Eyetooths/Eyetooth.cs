using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Eyetooths
{
    /// <summary>
    /// 泣血瞳牙，克眼掉落的法师牙镖。飞刀式使用：不显示手持贴图，
    /// 由 <see cref="UseStyle"/> 手搓投掷动作，甩臂中段才真正放镖
    /// </summary>
    internal class Eyetooth : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Eyetooth";

        /// <summary>放镖帧，甩臂鞭出的中段</summary>
        private const int ReleaseTick = 3;

        //Shoot 只暂存弹道，真正的生成在投掷动作里
        private bool pendingThrow;
        private Vector2 pendingVelocity;
        private int pendingDamage;
        private float pendingKnockback;

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 60;
            Item.damage = 10;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 6;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.6f;
            Item.UseSound = null;//出手音效在牙镖首帧
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<EyetoothDart>();
            Item.shootSpeed = 7.5f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.whoAmI == Main.myPlayer) {
                pendingThrow = true;
                pendingVelocity = velocity;
                pendingDamage = damage;
                pendingKnockback = knockback;
            }
            return false;
        }

        /// <summary>投掷动作：反手蓄势、甩臂鞭出、顺势收臂，后臂小幅反拧配重</summary>
        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            int e = player.itemAnimationMax - player.itemAnimation;
            float aim = player.itemRotation;
            if (player.direction < 0) {
                aim += MathHelper.Pi;
            }
            float dg = player.direction * player.gravDir;

            float armAngle;
            Player.CompositeArmStretchAmount stretch;
            if (e < 2) {
                //反手蓄势，牙镖压在肩后
                float q = e / 2f;
                armAngle = aim - dg * MathHelper.Lerp(2.6f, 2.15f, q);
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            else if (e < 4) {
                //鞭出，过冲一点再回
                float q = (e - 2) / 2f;
                float eased = 1f - MathF.Pow(1f - q, 3f);
                armAngle = aim + dg * MathHelper.Lerp(-2.15f, 0.3f, eased);
                stretch = Player.CompositeArmStretchAmount.Full;
            }
            else if (e < 8) {
                //回坐落定
                float q = (e - 4) / 4f;
                armAngle = aim + dg * MathHelper.Lerp(0.3f, 0.05f, 1f - (1f - q) * (1f - q));
                stretch = Player.CompositeArmStretchAmount.Full;
            }
            else {
                //收臂松劲
                float q = MathHelper.Clamp((e - 8) / 8f, 0f, 1f);
                armAngle = aim + dg * MathHelper.Lerp(0.05f, -0.4f, q * q);
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            player.SetCompositeArmFront(true, stretch, armAngle - MathHelper.PiOver2);

            //后臂反拧，蓄势时向前压，鞭出后向后甩
            float backAngle = e < 2 ? aim + dg * 0.6f : aim - dg * 0.5f;
            player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , backAngle - MathHelper.PiOver2);

            //甩臂中段放镖；高攻速下动画不足时兜底在最后一帧放
            if (pendingThrow && player.whoAmI == Main.myPlayer
                && (e >= ReleaseTick || player.itemAnimation <= 1)) {
                pendingThrow = false;
                ThrowDart(player);
            }
        }

        private void ThrowDart(Player player) {
            Vector2 dir = pendingVelocity.SafeNormalize(Vector2.UnitX);
            Vector2 hand = player.GetPlayerStabilityCenter();
            Vector2 spawn = hand + dir * 13f;
            if (!Collision.CanHitLine(hand, 4, 4, spawn, 4, 4)) {
                spawn = hand;
            }
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), spawn, pendingVelocity
                , Item.shoot, pendingDamage, pendingKnockback, player.whoAmI, 0f, -1f);
            EyetoothVFX.LaunchSpit(spawn, pendingVelocity);
        }
    }

    /// <summary>
    /// 牙创流血，DoT 走 <see cref="Content.CWRNpc.EyetoothBleed"/> 标志 → UpdateLifeRegen，
    /// 这里只置标志与限频渗血
    /// </summary>
    internal class EyetoothWound : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "EyetoothWound";
        private int time;

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().EyetoothBleed = true;
            if (++time % 8 == 0) {
                EyetoothVFX.WoundDrip(npc);
            }
        }
    }
}
