using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.NPCs.OverhaulBehavior;
using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs
{
    public delegate void On_OnHitByProjectileDelegate(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone);
    public delegate void On_ModifyIncomingHitDelegate(NPC npc, ref NPC.HitModifiers modifiers);

    internal class NPCSystem : ModSystem
    {
        public static Type npcLoaderType;
        public static MethodBase onHitByProjectile_Method;
        public static MethodBase modifyIncomingHit_Method;

        public override void PostSetupContent() {
            //加载生物定义
            new PerforatorBehavior().Load();
            new HiveMindBehavior().Load();
        }

        public override void Load() {
            npcLoaderType = typeof(NPCLoader);
            onHitByProjectile_Method = npcLoaderType.GetMethod("OnHitByProjectile", BindingFlags.Public | BindingFlags.Static);
            if (onHitByProjectile_Method != null) {
                MonoModHooks.Add(onHitByProjectile_Method, OnHitByProjectileHook);
            }
            modifyIncomingHit_Method = npcLoaderType.GetMethod("ModifyIncomingHit", BindingFlags.Public | BindingFlags.Static);
            if (modifyIncomingHit_Method != null) {
                MonoModHooks.Add(modifyIncomingHit_Method, ModifyIncomingHitHook);
            }
        }

        public void OnHitByProjectileHook(On_OnHitByProjectileDelegate orig, NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone) {
            foreach (NPCCustomizer inds in CWRMod.NPCCustomizerInstances) {
                bool? shouldOverride = null;
                if (inds.On_OnHitByProjectile_IfSpan(projectile)) {
                    shouldOverride = inds.On_OnHitByProjectile(npc, projectile, hit, damageDone);
                }
                if (shouldOverride.HasValue) {
                    if (shouldOverride.Value) {
                        npc.ModNPC?.OnHitByProjectile(projectile, hit, damageDone);
                        return;
                    }
                    else {
                        return;
                    }
                }
            }
            orig.Invoke(npc, projectile, hit, damageDone);
        }

        public void ModifyIncomingHitHook(On_ModifyIncomingHitDelegate orig, NPC npc, ref NPC.HitModifiers modifiers) {
            foreach (NPCCustomizer inds in CWRMod.NPCCustomizerInstances) {
                bool? shouldOverride = inds.On_ModifyIncomingHit(npc, ref modifiers);
                if (shouldOverride.HasValue) {
                    if (shouldOverride.Value) {
                        npc.ModNPC?.ModifyIncomingHit(ref modifiers);
                        return;
                    }
                    else {
                        return;
                    }
                }
            }
            orig.Invoke(npc, ref modifiers);
        }
    }
}
