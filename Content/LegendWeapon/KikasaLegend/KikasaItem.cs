using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 传奇武器·鬼伞。第一能力模块：血湖领域——持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Domain"/> 开阖，
    /// 输入与状态机在 <see cref="KikasaDomains.KikasaDomainPlayer"/>；
    /// 领域含鬼雨异化表里形态——按 <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/>
    /// （默认中键）血湖沸腾倒转切换血/雨形态。
    /// 第二能力模块：湖藏——领域中持物按 <see cref="Common.CWRKeySystem.Kikasa_Sink"/>
    /// 沉物入湖存储，持伞按 <see cref="Common.CWRKeySystem.Legend_UIControl"/> 开湖窗提取；
    /// 数据与输入在 <see cref="KikasaVaults.KikasaVaultPlayer"/>，
    /// 沉浮演出在 <see cref="KikasaVaults.KikasaLakeFX"/>。
    /// 第三能力模块：能力复制——湖记住最后一只被沉溺的生物，
    /// 按 <see cref="Common.CWRKeySystem.Kikasa_Summon"/> 召唤对应鬼奴驱使；
    /// 记录与输入在 <see cref="KikasaServants.KikasaServantPlayer"/>，
    /// 穷举条目在 <see cref="KikasaServants.KikasaServantIndex"/>。
    /// 第四能力模块：普攻·墨雨——按住左键撑出悬伞
    /// <see cref="KikasaRains.KikasaRainUmbrella"/>，头顶自旋按节拍降下大墨滴追踪敌人。
    /// 第五能力模块：鬼梦——领域中按 <see cref="Common.CWRKeySystem.Kikasa_DreamReflect"/>
    /// 唤醒倒影恶犬（湖镜里的人影换成黑犬），再按
    /// <see cref="Common.CWRKeySystem.Kikasa_DreamPull"/> 把一切拉入鬼梦
    /// （湖沸腾倒转，红天村落、湖水不见；物品封禁、左键连唤恶犬，重按归返）；
    /// 相位与包络在 <see cref="KikasaDomains.KikasaDomainPlayer"/> 与
    /// <see cref="KikasaDreams.KikasaDreamDirector"/>，玩家锁与唤犬在
    /// <see cref="KikasaDreams.KikasaDreamPlayer"/>
    /// </summary>
    internal class KikasaItem : ModItem
    {
        public override void SetDefaults() {
            Item.width = 50;
            Item.height = 54;
            Item.damage = 100;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.channel = true;
            Item.shoot = ModContent.ProjectileType<KikasaRainUmbrella>();
            Item.shootSpeed = 1f;
            Item.value = Terraria.Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Purple;
        }

        /// <summary>右键=倒撑蓄力重击</summary>
        public override bool AltFunctionUse(Player player) => true;

        //悬伞在场时不重复开伞
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<KikasaRainUmbrella>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //ai[0]:0=墨雨,1=蓄力倒撑(重击模块接管)
            float mode = player.altFunctionUse == 2 ? 1f : 0f;
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero,
                type, damage, knockback, player.whoAmI, mode);
            return false;
        }
    }
}
