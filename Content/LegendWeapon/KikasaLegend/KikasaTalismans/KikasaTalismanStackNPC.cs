using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符 NPC 叠层统一承载（洇痕/渍/霉蚀……一符一类，不另开 GlobalNPC）。<br/>
    /// 紧凑条目 Kind（符网络 id+1，同滴标签口径）/Count/Timer，懒分配，无叠层零成本；
    /// 计时各端本地走完（收到广播后自走），归零整类清除。<br/>
    /// <b>联机模型</b>：写入端=效果归属端（命中挂钩在 owner 客户端、死亡传播在服务端），
    /// 写入即广播绝对量（<see cref="CWRMessageType.KikasaTalismanStack"/> 定长 9 字节）——
    /// 服务端承载（lifeRegen/OnKill 权威）并转播给旁观端做表现；
    /// 丢包由下一次写入自愈，多写入者按后到覆盖（可接受的表现级近似）。<br/>
    /// 效果语义由定义挂钩解释：ModifyStackLifeRegen / OnStackNPCKill / DrawNPCStack
    /// </summary>
    internal sealed class KikasaTalismanStackNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>单类叠层条目；Kind=0 为空槽</summary>
        private struct StackEntry
        {
            public byte Kind;
            public byte Count;
            public ushort Timer;
        }

        /// <summary>同一 NPC 可并存的叠层类数（洇/渍/霉 + 1 余量）</summary>
        private const int MaxKinds = 4;

        //懒分配：绝大多数 NPC 一辈子不挨符
        private StackEntry[] entries;

        //====本地承载====

        private int IndexOf(byte kind) {
            if (entries == null) {
                return -1;
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Kind == kind) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>本地写入绝对量；count=0 清除该类。槽满时挤掉剩时最短的一类</summary>
        private void ApplyLocal(byte kind, byte count, ushort timer) {
            int idx = IndexOf(kind);
            if (count == 0) {
                if (idx >= 0) {
                    entries[idx] = default;
                }
                return;
            }
            if (idx < 0) {
                entries ??= new StackEntry[MaxKinds];
                int victim = 0;
                for (int i = 0; i < entries.Length; i++) {
                    if (entries[i].Kind == 0) {
                        victim = i;
                        break;
                    }
                    if (entries[i].Timer < entries[victim].Timer) {
                        victim = i;
                    }
                }
                idx = victim;
            }
            entries[idx] = new StackEntry { Kind = kind, Count = count, Timer = timer };
        }

        public override void PostAI(NPC npc) {
            if (entries == null) {
                return;
            }
            //计时各端本地自走，归零清类；广播携带的 Timer 让各端近似同拍
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Kind == 0) {
                    continue;
                }
                if (entries[i].Timer > 0 && --entries[i].Timer == 0) {
                    entries[i] = default;
                }
            }
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (entries == null) {
                return;
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Kind != 0
                    && KikasaTalismanHooks.TryGetTagDefinition(entries[i].Kind,
                        out KikasaTalismanDefinition definition)) {
                    definition.ModifyStackLifeRegen(npc, entries[i].Count, ref damage);
                }
            }
        }

        public override void OnKill(NPC npc) {
            if (entries == null) {
                return;
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Kind != 0
                    && KikasaTalismanHooks.TryGetTagDefinition(entries[i].Kind,
                        out KikasaTalismanDefinition definition)) {
                    definition.OnStackNPCKill(npc, entries[i].Count);
                }
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (entries == null || npc.IsABestiaryIconDummy) {
                return;
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Kind != 0
                    && KikasaTalismanHooks.TryGetTagDefinition(entries[i].Kind,
                        out KikasaTalismanDefinition definition)) {
                    definition.DrawNPCStack(spriteBatch, npc, entries[i].Count,
                        entries[i].Timer, screenPos, drawColor);
                }
            }
        }

        //====读写 API（符实现调用；写入端=效果归属端）====

        private static byte KindOf(KikasaTalismanDefinition definition)
            => (byte)KikasaTalismanHooks.TagIdFor(definition);

        /// <summary>当前叠层数，无为 0</summary>
        internal static int GetStacks(NPC npc, KikasaTalismanDefinition definition) {
            byte kind = KindOf(definition);
            if (kind == 0 || npc == null
                || !npc.TryGetGlobalNPC(out KikasaTalismanStackNPC host)) {
                return 0;
            }
            int idx = host.IndexOf(kind);
            return idx >= 0 ? host.entries[idx].Count : 0;
        }

        /// <summary>写入绝对层数并广播；count≤0 即清除</summary>
        internal static void SetStacks(NPC npc, KikasaTalismanDefinition definition,
            int count, int timerFrames) {
            byte kind = KindOf(definition);
            if (kind == 0 || npc?.active != true
                || !npc.TryGetGlobalNPC(out KikasaTalismanStackNPC host)) {
                return;
            }
            byte clamped = (byte)Utils.Clamp(count, 0, byte.MaxValue);
            ushort timer = (ushort)Utils.Clamp(timerFrames, 0, ushort.MaxValue);
            host.ApplyLocal(kind, clamped, timer);
            Broadcast(npc, kind, clamped, timer);
        }

        /// <summary>叠加 delta（钳到 cap）并刷新计时，返回新层数</summary>
        internal static int AddStacks(NPC npc, KikasaTalismanDefinition definition,
            int delta, int cap, int timerFrames) {
            int next = Utils.Clamp(GetStacks(npc, definition) + delta, 0, cap);
            SetStacks(npc, definition, next, timerFrames);
            return next;
        }

        /// <summary>清除该类叠层并广播</summary>
        internal static void ClearStacks(NPC npc, KikasaTalismanDefinition definition)
            => SetStacks(npc, definition, 0, 0);

        //====联机：紧凑广播（定长 9 字节：npcWho/npcType/kind/count/timer）====

        private static void Broadcast(NPC npc, byte kind, byte count, ushort timer) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                return;
            }
            //客户端发服务器（承载+转播），服务端起源直接广播全体
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.KikasaTalismanStack);
            packet.Write((byte)npc.whoAmI);
            packet.Write(npc.type);
            packet.Write(kind);
            packet.Write(count);
            packet.Write(timer);
            packet.Send();
        }

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.KikasaTalismanStack) {
                return;
            }
            //链式 handler 共用一条流：定长负载先读满，校验只做丢弃
            int npcWho = reader.ReadByte();
            int npcType = reader.ReadInt32();
            byte kind = reader.ReadByte();
            byte count = reader.ReadByte();
            ushort timer = reader.ReadUInt16();

            if (npcWho >= Main.maxNPCs || kind == 0) {
                return;
            }
            NPC npc = Main.npc[npcWho];
            //跨端身份：槽位+类型双校验，类型不符=槽位已被复用，静默丢弃（计时兜底自清残留）
            if (npc?.active != true || npc.type != npcType) {
                return;
            }
            if (npc.TryGetGlobalNPC(out KikasaTalismanStackNPC host)) {
                host.ApplyLocal(kind, count, timer);
            }
            if (Main.netMode == NetmodeID.Server) {
                //服务端校验通过后原样转播给发送者之外的所有端
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.KikasaTalismanStack);
                packet.Write((byte)npcWho);
                packet.Write(npcType);
                packet.Write(kind);
                packet.Write(count);
                packet.Write(timer);
                packet.Send(-1, whoAmI);
            }
        }
    }
}
