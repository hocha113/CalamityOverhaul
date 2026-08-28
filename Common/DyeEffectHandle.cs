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
        /// <summary>当前帧染料 <see cref="ArmorShaderData"/></summary>
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
        public override int TargetID => -1;//-1 全局覆盖所有弹幕
        //TargetID == -1 的节点不随实体克隆，是被逐帧换 projectile 上下文的单例，
        //所以 SetProperty 不会被调用，实例字段缓存不到任何东西，必须每拍现查
        public override bool AI() {
            //零染料实体不写染色上下文（染色上下文的消费方只在有染料时才有意义）
            int dyeItemID = projectile.CWR().DyeItemID;
            if (dyeItemID > 0) {
                IsDyeDustEffectActive = IsUpdate = true;
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void PostAI() {
            if (IsUpdate) {
                IsDyeDustEffectActive = IsUpdate = false;
                DyeShaderData = null;
            }
        }
    }

    internal class DyeGlobalNPC : NPCOverride
    {
        public static bool IsUpdate { get; private set; } = false;
        public override int TargetID => -1;//-1 全局覆盖所有 NPC
        //同上，通用节点是共享单例，npc 上下文每拍被换，不能缓存 CWRNpc
        public override bool AI() {
            int dyeItemID = npc.CWR().DyeItemID;
            if (dyeItemID > 0) {
                IsDyeDustEffectActive = IsUpdate = true;
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }
        public override void PostAI() {
            ClearUpdateContext();
        }

        internal static void ClearUpdateContext() {
            if (IsUpdate) {
                IsDyeDustEffectActive = IsUpdate = false;
                DyeShaderData = null;
            }
        }
    }

    internal class DyeGlobalItem : ItemOverride
    {
        public static bool IsShootUpdate { get; private set; } = false;
        public override int TargetID => -1;//-1 全局覆盖所有物品
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
        public override int TargetItemID => ItemID.None;//None 全局覆盖所有物品
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
        public override void Load() {//命中 Hook 后续应迁入 InnoVault
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
