using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EaterOfWorlds
{
    /// <summary>
    /// 蚀界之颚：世界吞噬者残酷遗物。所有攻击叠加酸蚀(削甲+酸液DoT)，
    /// 击杀带酸蚀的敌人时从尸体钻出友方吞世幼虫，钻地伏击并传播酸蚀
    /// </summary>
    internal class WorldEatersMaw : BaseBrutalRelic
    {
        //====酸蚀数值(刻意超模：明显压过肉山前档位，用户签字基调)====
        /// <summary>酸蚀层数上限</summary>
        internal const int MaxStacks = 10;
        /// <summary>每层削减防御</summary>
        internal const float DefShredPerStack = 3f;
        /// <summary>每层 lifeRegen 削减(单位2=每秒1点，12即每秒6点)</summary>
        internal const int DotPerStack = 12;
        /// <summary>酸蚀持续帧(命中刷新)</summary>
        internal const int BrandDuration = 360;

        //====幼虫数值====
        /// <summary>幼虫基础伤害(乘玩家通用增伤)</summary>
        internal const int WormBaseDamage = 70;
        /// <summary>同时存活的幼虫上限(设计声明：同屏多条的封顶)</summary>
        internal const int WormCap = 6;
        /// <summary>幼虫存活时长(帧，设计声明：7秒后自行入土消散)</summary>
        internal const int WormLifetime = 420;

        public override void SetDefaults() {
            base.SetDefaults();
            //同期(世吞档)Boss掉落物约2金，按系列约定取3-5倍
            Item.value = Item.buyPrice(0, 10, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<WorldEatersMawPlayer>().Equipped = true;
        }
    }

    /// <summary>装备状态旗标与酸蚀叠层入口(命中挂钩只在攻击方客户端触发)</summary>
    internal class WorldEatersMawPlayer : ModPlayer
    {
        /// <summary>本帧是否装备生效，物品钩子逐帧点亮</summary>
        public bool Equipped;

        public override void ResetEffects() => Equipped = false;

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Equipped) {
                MawCorrosionNPC.AddStacks(target, 1, Player.whoAmI);
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Equipped) {
                return;
            }
            //幼虫撕咬叠2层，其余弹幕(含召唤/哨兵)叠1层
            int add = proj.type == ModContent.ProjectileType<MawWormProj>() ? 2 : 1;
            MawCorrosionNPC.AddStacks(target, add, Player.whoAmI);
        }
    }
}
