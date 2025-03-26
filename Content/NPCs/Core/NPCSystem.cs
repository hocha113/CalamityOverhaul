using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
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
        #region Data
        internal delegate void On_NPCDelegate(NPC npc);
        internal delegate bool On_NPCDelegate2(NPC npc);
        internal delegate bool On_DrawDelegate(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
        internal delegate void On_DrawDelegate2(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
        internal delegate void On_OnHitByProjectileDelegate(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone);
        internal delegate void On_ModifyIncomingHitDelegate(NPC npc, ref NPC.HitModifiers modifiers);
        internal delegate void On_NPCSetDefaultDelegate();
        public static Type npcLoaderType;
        public static MethodInfo onHitByProjectile_Method;
        public static MethodInfo modifyIncomingHit_Method;
        public static MethodInfo onNPCAI_Method;
        public static MethodInfo onPreKill_Method;
        public static MethodInfo onPreDraw_Method;
        public static MethodInfo onPostDraw_Method;
        public static MethodInfo onCheckDead_Method;
        internal static List<NPCCustomizer> NPCCustomizers { get; private set; } = [];
        public static List<NPCOverride> NPCOverrides { get; private set; } = [];
        public static Dictionary<int, NPCOverride> IDToNPCSetDic { get; private set; } = [];
        #endregion

        #region NetWork
        /// <summary>
        /// 在必要的时候使用这个发送NPC基本数据
        /// </summary>
        /// <param name="npc"></param>
        public static void SendNPCbasicData(NPC npc, int player = -1) {
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.NPCbasicData);
            modPacket.Write((byte)npc.whoAmI);
            modPacket.WriteVector2(npc.position);
            modPacket.Write(npc.rotation);
            modPacket.Send(player);
        }
        /// <summary>
        /// 接收NPC基本数据
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="whoAmI"></param>
        public static void NPCbasicDataHandler(BinaryReader reader) {
            int whoAmI = reader.ReadByte();
            Vector2 pos = reader.ReadVector2();
            float rot = reader.ReadSingle();

            NPC npc = CWRUtils.GetNPCInstance(whoAmI);

            if (npc != null) {
                npc.position = pos;
                npc.rotation = rot;

                if (VaultUtils.isServer) {
                    ModPacket modPacket = CWRMod.Instance.GetPacket();
                    modPacket.Write((byte)CWRMessageType.NPCbasicData);
                    modPacket.Write((byte)npc.whoAmI);
                    modPacket.WriteVector2(npc.position);
                    modPacket.Write(npc.rotation);
                    modPacket.Send();
                }
            }
        }
        #endregion

        private static void LoadNPCSets() {
            NPCCustomizers = VaultUtils.GetSubclassInstances<NPCCustomizer>();
            NPCOverrides = VaultUtils.GetSubclassInstances<NPCOverride>();
            NPCOverrides.RemoveAll(npc => !npc.CanLoad());
            IDToNPCSetDic = [];
            foreach (var npcOverh in NPCOverrides) {
                IDToNPCSetDic.Add(npcOverh.TargetID, npcOverh);
            }
        }

        private static MethodInfo GetMethodInfo(string key) => npcLoaderType.GetMethod(key, BindingFlags.Public | BindingFlags.Static);

        private static void DompLog(string name) => CWRMod.Instance.Logger.Info($"ERROR:Fail To Load! {name} Is Null!");

        private void LoaderMethodAndHook() {
            {
                onHitByProjectile_Method = GetMethodInfo("OnHitByProjectile");
                if (onHitByProjectile_Method != null) {
                    CWRHook.Add(onHitByProjectile_Method, OnHitByProjectileHook);
                }
                else {
                    DompLog("onHitByProjectile_Method");
                }
            }
            {
                modifyIncomingHit_Method = GetMethodInfo("ModifyIncomingHit");
                if (modifyIncomingHit_Method != null) {
                    CWRHook.Add(modifyIncomingHit_Method, ModifyIncomingHitHook);
                }
                else {
                    DompLog("modifyIncomingHit_Method");
                }
            }
            {
                onNPCAI_Method = GetMethodInfo("NPCAI");
                if (onNPCAI_Method != null) {
                    CWRHook.Add(onNPCAI_Method, OnNPCAIHook);
                }
                else {
                    DompLog("onNPCAI_Method");
                }
            }
            {
                onPreDraw_Method = GetMethodInfo("PreDraw");
                if (onPreDraw_Method != null) {
                    CWRHook.Add(onPreDraw_Method, OnPreDrawHook);
                }
                else {
                    DompLog("onPreDraw_Method");
                }
            }
            {
                onPostDraw_Method = GetMethodInfo("PostDraw");
                if (onPostDraw_Method != null) {
                    CWRHook.Add(onPostDraw_Method, OnPostDrawHook);
                }
                else {
                    DompLog("onPostDraw_Method");
                }
            }
            {
                onCheckDead_Method = GetMethodInfo("CheckDead");
                if (onCheckDead_Method != null) {
                    CWRHook.Add(onCheckDead_Method, OnCheckDeadHook);
                }
                else {
                    DompLog("onCheckDead_Method");
                }
            }
            {
                onPreKill_Method = GetMethodInfo("PreKill");
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
            On_NPC.SetDefaults += OnNPCSetDefaultsHook;
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
            NPCCustomizers?.Clear();
            NPCOverrides?.Clear();
            IDToNPCSetDic?.Clear();
            On_NPC.SetDefaults -= OnNPCSetDefaultsHook;
        }

        //这个钩子保证修改可以运行在最后，防止被其他的模组覆盖效果
        public static void OnNPCSetDefaultsHook(On_NPC.orig_SetDefaults orig, NPC npc, int Type, NPCSpawnParams spawnparams) {
            orig.Invoke(npc, Type, spawnparams);
            NPCOverride.SetDefaults(npc);
        }

        //public static void OnDrawNPCHeadBossHook(On_DrawNPCHeadBossDelegate orig, Entity theNPC, byte alpha
        //    , float headScale, float rotation, SpriteEffects effects, int bossHeadId, float x, float y) {
        //    bool reset = true;
        //    if (theNPC is NPC npc && NPCOverride.TryFetchByID(npc.type, out var npcOverride)) {
        //        reset = npcOverride.PreDrawNPCHeadBoss(npc, alpha, headScale, rotation, effects, bossHeadId, x, y);
        //    }
        //    if (reset) {
        //        orig.Invoke(theNPC, alpha, headScale, rotation, effects, bossHeadId, x, y);
        //    }
        //}

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
            foreach (NPCCustomizer inds in NPCCustomizers) {
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
            foreach (NPCCustomizer inds in NPCCustomizers) {
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
