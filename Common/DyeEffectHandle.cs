using InnoVault.GameSystem;
using InnoVault.PRT;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
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

    internal class DyeHitEffect : ModSystem
    {
        public static bool IsHitEffectUpdate { get; private set; } = false;
        private delegate void On_OnHitNPCWithProj_Delegate(Projectile proj, NPC target, in NPC.HitInfo hit, int damageDone);
        public override void Load() {//这个钩子应该在后续的更新里移动到InnoVault中，而不是在应用模组里挂载
            var method = typeof(CombinedHooks).GetMethod("OnHitNPCWithProj", BindingFlags.Public | BindingFlags.Static);
            VaultHook.Add(method, OnHitNPCWithProjHook);
        }

        private static void OnHitNPCWithProjHook(On_OnHitNPCWithProj_Delegate orig, Projectile proj, NPC target, in NPC.HitInfo hit, int damageDone) {
            IsDyeDustEffectActive = IsHitEffectUpdate = true;
            int dyeItemID = 0;
            if (proj.Alives()) {
                dyeItemID = proj.CWR().DyeItemID;
            }
            if (dyeItemID == 0 && target.Alives()) {
                dyeItemID = target.CWR().DyeItemID;
            }
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            orig.Invoke(proj, target, hit, damageDone);
            IsDyeDustEffectActive = IsHitEffectUpdate = false;
            DyeShaderData = null;
        }
    }
}
