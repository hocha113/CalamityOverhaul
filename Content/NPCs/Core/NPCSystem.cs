using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    /// <summary>
    /// 所有关于NPC行为覆盖和性质加载的钩子在此处挂载
    /// </summary>
    internal class NPCSystem : ModSystem
    {
        internal delegate void On_NPCDelegate(NPC npc);
        internal delegate bool On_NPCDelegate2(NPC npc);
        internal delegate bool On_DrawDelegate(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
        internal delegate void On_DrawDelegate2(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
        internal delegate void On_OnHitByProjectileDelegate(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone);
        internal delegate void On_ModifyIncomingHitDelegate(NPC npc, ref NPC.HitModifiers modifiers);

        public static Type npcLoaderType;
        public static MethodInfo onHitByProjectile_Method;
        public static MethodInfo modifyIncomingHit_Method;
        public static MethodInfo onNPCAI_Method;
        public static MethodInfo onPreKill_Method;
        public static MethodInfo onPreDraw_Method;
        public static MethodInfo onPostDraw_Method;
        public static MethodInfo onCheckDead_Method;
        public static List<NPCOverride> NPCSets { get; private set; }

        private void LoadNPCSets() {
            NPCSets = [];
            foreach (Type type in VaultUtils.GetSubclassTypeList(typeof(NPCOverride))) {
                if (type != typeof(NPCOverride)) {
                    object obj = Activator.CreateInstance(type);
                    if (obj is NPCOverride inds) {
                        if (inds.CanLoad() && CWRServerConfig.Instance.BiologyOverhaul) {//前提是开启了生物修改
                            NPCSets.Add(inds);
                        }
                    }
                }
            }
        }

        private MethodInfo getMethodInfo(string key) => npcLoaderType.GetMethod(key, BindingFlags.Public | BindingFlags.Static);

        private void DompLog(string name) => CWRMod.Instance.Logger.Info($"ERROR:Fail To Load! {name} Is Null!");

        private void LoaderMethodAndHook() {
            {
                onHitByProjectile_Method = getMethodInfo("OnHitByProjectile");
                if (onHitByProjectile_Method != null) {
                    CWRHook.Add(onHitByProjectile_Method, OnHitByProjectileHook);
                }
                else {
                    DompLog("onHitByProjectile_Method");
                }
            }
            {
                modifyIncomingHit_Method = getMethodInfo("ModifyIncomingHit");
                if (modifyIncomingHit_Method != null) {
                    CWRHook.Add(modifyIncomingHit_Method, ModifyIncomingHitHook);
                }
                else {
                    DompLog("modifyIncomingHit_Method");
                }
            }
            {
                onNPCAI_Method = getMethodInfo("NPCAI");
                if (onNPCAI_Method != null) {
                    CWRHook.Add(onNPCAI_Method, OnNPCAIHook);
                }
                else {
                    DompLog("onNPCAI_Method");
                }
            }
            {
                onPreDraw_Method = getMethodInfo("PreDraw");
                if (onPreDraw_Method != null) {
                    CWRHook.Add(onPreDraw_Method, OnPreDrawHook);
                }
                else {
                    DompLog("onPreDraw_Method");
                }
            }
            {
                onPostDraw_Method = getMethodInfo("PostDraw");
                if (onPostDraw_Method != null) {
                    CWRHook.Add(onPostDraw_Method, OnPostDrawHook);
                }
                else {
                    DompLog("onPostDraw_Method");
                }
            }
            {
                onCheckDead_Method = getMethodInfo("CheckDead");
                if (onCheckDead_Method != null) {
                    CWRHook.Add(onCheckDead_Method, OnCheckDeadHook);
                }
                else {
                    DompLog("onCheckDead_Method");
                }
            }
            {
                onPreKill_Method = getMethodInfo("PreKill");
                if (onPreKill_Method != null) {
                    CWRHook.Add(onPreKill_Method, OnPreKillHook);
                }
                else {
                    DompLog("onPreKill_Method");
                }
            }
        }

        public override void Load() {
            npcLoaderType = typeof(NPCLoader);
            LoadNPCSets();
            LoaderMethodAndHook();
        }

        public override void Unload() {
            npcLoaderType = null;
            onHitByProjectile_Method = null;
            modifyIncomingHit_Method = null;
            onNPCAI_Method = null;
            onPreKill_Method = null;
            onPreDraw_Method = null;
            onPostDraw_Method = null;
            onCheckDead_Method = null;
            NPCSets?.Clear();
        }

        public static bool OnPreKillHook(On_NPCDelegate2 orig, NPC npc) {
            if (npc.type == NPCID.None || !npc.active) {
                return orig.Invoke(npc);
            }
            bool? reset = npc.CWR().NPCOverride.On_PreKill();
            if (reset.HasValue) {
                return reset.Value;
            }
            return orig.Invoke(npc);
        }

        public static bool OnCheckDeadHook(On_NPCDelegate2 orig, NPC npc) {
            if (npc.type == NPCID.None || !npc.active) {
                return orig.Invoke(npc);
            }
            bool? reset = npc.CWR().NPCOverride.CheckDead();
            if (reset.HasValue) {
                return reset.Value;
            }
            return orig.Invoke(npc);
        }

        public static void OnNPCAIHook(On_NPCDelegate orig, NPC npc) {
            if (npc.type == NPCID.None || !npc.active) {
                return;
            }

            CWRNpc cwrNPC = npc.CWR();

            if (CWRPlayer.CanTimeFrozen() || cwrNPC.FrozenActivity) {
                npc.timeLeft++;
                npc.aiAction = 0;
                npc.frameCounter = 0;
                npc.velocity = Vector2.Zero;
                npc.position = npc.oldPosition;
                npc.direction = npc.oldDirection;
                return;
            }

            int type = npc.type;
            bool reset = cwrNPC.NPCOverride.AI();

            cwrNPC.NPCOverride.DoNet();

            npc.type = type;
            if (!reset) {
                return;
            }
            orig.Invoke(npc);
        }

        public static bool OnPreDrawHook(On_DrawDelegate orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (npc.type == NPCID.None || !npc.active) {
                return false;
            }
            bool? reset = npc.CWR().NPCOverride.Draw(spriteBatch, screenPos, drawColor);
            if (reset.HasValue) {
                return reset.Value;
            }

            try {
                return orig.Invoke(npc, spriteBatch, screenPos, drawColor);
            } catch {
                return true;
            }
        }

        public static void OnPostDrawHook(On_DrawDelegate2 orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            bool reset = npc.CWR().NPCOverride.PostDraw(spriteBatch, screenPos, drawColor);
            if (!reset) {
                return;
            }

            orig.Invoke(npc, spriteBatch, screenPos, drawColor);
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
