using InnoVault.GameSystem;
using InnoVault.PRT;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using static CalamityOverhaul.Common.DyeEffectHandle;

namespace CalamityOverhaul.Common
{
    internal class DyeEffectHandle
    {
        /// <summary>
        /// 染色体数据
        /// </summary>
        public static ArmorShaderData DyeShaderData { get; internal set; } = null;
        public static bool IsDyeDustEffectActive { get; internal set; } = false;
    }

    internal class DyeGlobalDust : GlobalDust
    {
        public override void OnSpawn(Dust dust) {
            if (!IsDyeDustEffectActive || DyeShaderData == null) {
                return;
            }
            dust.shader = DyeShaderData;
        }
    }

    internal class DyeGlobalPRT : GlobalPRT
    {
        public override void OnSpawn(BasePRT prt) {
            if (!IsDyeDustEffectActive || DyeShaderData == null) {
                return;
            }
            prt.shader = DyeShaderData;
        }
    }

    internal class DyeGlobalProjectile : ProjOverride
    {
        public static bool IsUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有弹幕之上
        public override bool AI() {
            IsDyeDustEffectActive = IsUpdate = true;
            int dyeItemID = projectile.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void PostAI() {
            IsDyeDustEffectActive = IsUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyeGlobalNPC : NPCOverride
    {
        public static bool IsUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有NPC之上
        public override bool AI() {
            IsDyeDustEffectActive = IsUpdate = true;
            int dyeItemID = npc.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }
        public override void PostAI() {
            IsDyeDustEffectActive = IsUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyeGlobalItem : ItemOverride
    {
        public static bool IsShootUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有物品之上
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (item.type == ItemID.None) {
                return null;
            }
            IsDyeDustEffectActive = IsShootUpdate = true;
            int dyeItemID = item.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return null;
        }

        public override void On_PostShoot(Item item, int whoAmI, int weaponDamage) {
            IsDyeDustEffectActive = IsShootUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyePlayer : PlayerOverride
    {
        public static bool IsMeleeEffectUpdate { get; private set; } = false;
        public override int TargetItemID => ItemID.None;//设置为None，这样让这个修改节点运行在所有物品之上
        public override bool On_PreEmitUseVisuals(Item item, ref Rectangle itemRectangle) {
            if (item.type == ItemID.None) {
                return true;
            }
            IsDyeDustEffectActive = IsMeleeEffectUpdate = true;
            int dyeItemID = item.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void On_PostEmitUseVisuals(Item item, ref Rectangle itemRectangle) {
            IsDyeDustEffectActive = IsMeleeEffectUpdate = false;
            DyeShaderData = null;
        }
    }
}
