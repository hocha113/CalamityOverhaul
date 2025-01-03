﻿using CalamityMod.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    /// <summary>
    /// 提供一个强行覆盖目标NPC行为性质的基类，通过On钩子为基础运行
    /// </summary>
    internal class NPCOverride
    {
        /// <summary>
        /// 要修改的NPC的ID值，在目前为止，每一个类型的NPC只能有一个实例对应
        /// </summary>
        public virtual int TargetID => NPCID.None;
        /// <summary>
        /// 模组实例
        /// </summary>
        public static Mod mod => CWRMod.Instance;
        /// <summary>
        /// 最大AI槽位数量，12个槽位，那么最大的数组索引就是11
        /// </summary>
        internal const int MaxAISlot = 12;
        /// <summary>
        /// ai槽位，使用<see cref="NetAISend()"/>来在多人游戏中同步这个字段的内容，在合适的时机调用它
        /// </summary>
        public float[] ai = new float[MaxAISlot];
        /// <summary>
        /// 本地ai槽位，这个值不会自动多人同步，如果有需要，重载<see cref="OtherNetWorkReceive(BinaryReader)"/>来同步它
        /// </summary>
        public float[] localAi = new float[MaxAISlot];
        /// <summary>
        /// 这个实例对应的NPC实例
        /// </summary>
        public NPC npc { get; private set; }
        /// <summary>
        /// 这个实例对应的CWRNpc类型实例
        /// </summary>
        public CWRNpc cwrNPC { get; private set; }
        /// <summary>
        /// 这个实例对应的CalamityGlobalNPC类型实例
        /// </summary>
        public CalamityGlobalNPC calNPC { get; private set; }
        //不要直接设置这个
        private bool _netWorkSend;
        /// <summary>
        /// 用于网络同步，只能在服务端进行设置，其他端口永远返回<see langword="false"/>，
        /// 当设置为<see langword="true"/>时，会自动调用<see cref="OtherNetWorkReceiveHander(BinaryReader)"/>进行网络数据同步
        /// </summary>
        public bool netWorkSend {
            get {
                if (!VaultUtils.isServer) {
                    return false;
                }
                return _netWorkSend;
            }
            set => _netWorkSend = value;
        }
        /// <summary>
        /// 克隆这个实例，注意，克隆出的新对象与原实例将不再具有任何引用关系
        /// </summary>
        /// <returns></returns>
        public NPCOverride Clone() => (NPCOverride)Activator.CreateInstance(GetType());
        /// <summary>
        /// 是否加载这个实例，默认返回<see langword="true"/>
        /// </summary>
        /// <returns></returns>
        public virtual bool CanLoad() { return true; }

        public static void SetDefaults(NPC npc, CWRNpc cwr, CalamityGlobalNPC cal) {
            NPCOverride inds = npc.CWR().NPCOverride;
            foreach (var pcc in NPCSystem.NPCSets) {
                if (pcc.TargetID == npc.type) {
                    inds = pcc.Clone();
                }
            }
            inds.ai = new float[MaxAISlot];
            inds.localAi = new float[MaxAISlot];
            inds.npc = npc;
            inds.cwrNPC = cwr;
            inds.calNPC = cal;
            inds.SetProperty();
            npc.CWR().NPCOverride = inds;
        }

        #region NetWork

        internal void OtherNetWorkSendHander() {
            if (!VaultUtils.isServer || !netWorkSend) {
                return;
            }
            ModPacket netMessage = mod.GetPacket();
            netMessage.Write((byte)CWRMessageType.NPCOverrideOtherAI);
            netMessage.Write(npc.whoAmI);
            OtherNetWorkSend(netMessage);
            netWorkSend = false;
        }
        /// <summary>
        /// 发送网络数据，同步额外的网络数据，重载编写时需要注意与<see cref="OtherNetWorkReceive"/>对应
        /// ，将<see cref="netWorkSend"/>设置为<see langword="true"/>后自动进行一次发包
        /// </summary>
        internal virtual void OtherNetWorkSend(ModPacket netMessage) { }
        //不要在实例中调用这个，而是使用netWorkSend
        internal static void OtherNetWorkReceiveHander(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            npc.CWR().NPCOverride.OtherNetWorkReceive(reader);
        }
        /// <summary>
        /// 接受网络数据，同步额外的网络数据，重载编写时需要注意与<see cref="OtherNetWorkSend"/>对应
        /// </summary>
        internal virtual void OtherNetWorkReceive(BinaryReader reader) { }
        /// <summary>
        /// 发送网络数据，同步<see cref="ai"/>的值，只会在服务端上运行
        /// </summary>
        internal void NetAISend() {
            if (VaultUtils.isServer) {
                var netMessage = mod.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                foreach (var aiValue in ai) {
                    netMessage.Write(aiValue);
                }

                netMessage.Send();
            }
        }
        /// <summary>
        /// 发送网络数据，同步<see cref="ai"/>的值，只会在服务端上运行
        /// </summary>
        /// <param name="npc"></param>
        internal static void NetAISend(NPC npc) {
            if (VaultUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                foreach (var aiValue in ai) {
                    netMessage.Write(aiValue);
                }

                netMessage.Send();
            }
        }

        internal static void NetAIReceive(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            float[] receiveAI = new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                receiveAI[i] = reader.ReadSingle();
            }
            if (npc.active) {
                NPCOverride pCOverride = npc.CWR().NPCOverride;
                for (int i = 0; i < MaxAISlot; i++) {
                    pCOverride.ai[i] = receiveAI[i];
                }
            }
        }

        #endregion
        /// <summary>
        /// 在npc生成的时候调用一次，用于初始化一些实例数据
        /// </summary>
        public virtual void SetProperty() { }
        /// <summary>
        /// 允许编辑ai或者阻断ai运行，返回默认值<see langword="true"/>则会继续运行后续ai行为
        /// ，返回<see langword="false"/>会阻断所有后续ai行为的调用
        /// </summary>
        /// <returns></returns>
        public virtual bool AI() { return true; }

        public virtual bool? On_PreKill() { return null; }

        public virtual bool? CheckDead() { return null; }

        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
