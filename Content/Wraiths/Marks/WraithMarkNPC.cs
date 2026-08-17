using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Marks
{
    /// <summary>
    /// 印记宿主：每只 NPC 一份，记着身上都挂了谁的印、还剩多久、有多重。<br/>
    /// 权威端结算（役鬼伤害本来就只在权威端算），不发包；
    /// 客户端要画的水光/轮廓光从各自已同步的载体本地推导，别当它是同步状态
    /// </summary>
    internal sealed class WraithMarkNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private struct MarkSlot
        {
            internal int Ticks;
            /// <summary>施加者 whoAmI：A 的鬼雨不能喂 B 的枯手</summary>
            internal int Owner;
            /// <summary>施加时的复苏快照，越接近夺身印得越重</summary>
            internal float Power;
        }

        private readonly MarkSlot[] slots = new MarkSlot[WraithMarkExtensions.Count];

        internal void Apply(WraithMark mark, int ticks, float power, int owner) {
            int index = mark.Index();
            if (index < 0 || ticks <= 0) {
                return;
            }
            ref MarkSlot slot = ref slots[index];
            //换施加者即整条重写；同一人续期取更长的那个
            if (slot.Ticks <= 0 || slot.Owner != owner) {
                slot.Owner = owner;
                slot.Ticks = ticks;
                slot.Power = power;
                return;
            }
            slot.Ticks = System.Math.Max(slot.Ticks, ticks);
            slot.Power = System.Math.Max(slot.Power, power);
        }

        internal bool Has(WraithMark mark, int owner) {
            int index = mark.Index();
            return index >= 0 && slots[index].Ticks > 0
                && (owner < 0 || slots[index].Owner == owner);
        }

        internal float PowerOf(WraithMark mark, int owner)
            => Has(mark, owner) ? slots[mark.Index()].Power : 0f;

        /// <summary>身上来自该施加者的印记合集。</summary>
        internal WraithMark Active(int owner) {
            WraithMark active = WraithMark.None;
            for (int i = 0; i < slots.Length; i++) {
                if (slots[i].Ticks > 0 && (owner < 0 || slots[i].Owner == owner)) {
                    active |= WraithMarkExtensions.FromIndex(i);
                }
            }
            return active;
        }

        internal void Clear() {
            for (int i = 0; i < slots.Length; i++) {
                slots[i] = default;
            }
        }

        /// <summary>喜堂里时间是停住的：缚在身上时其余印记不走表。</summary>
        private bool Frozen() => slots[WraithMark.Betrothed.Index()].Ticks > 0;

        public override void PostAI(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            bool frozen = Frozen();
            for (int i = 0; i < slots.Length; i++) {
                if (slots[i].Ticks <= 0) {
                    continue;
                }
                //缚自己照常走表，否则喜堂永不散场
                if (frozen && WraithMarkExtensions.FromIndex(i) != WraithMark.Betrothed) {
                    continue;
                }
                if (--slots[i].Ticks <= 0) {
                    slots[i] = default;
                }
            }
            TryBurstForOwners(npc);
        }

        /// <summary>逐个施加者点数：谁在这只猎物身上凑齐三印，谁就崩。</summary>
        private void TryBurstForOwners(NPC npc) {
            for (int i = 0; i < slots.Length; i++) {
                int owner = slots[i].Owner;
                if (slots[i].Ticks <= 0) {
                    continue;
                }
                //只在该施加者的首个印上判一次，避免同一人被点三遍
                bool firstOfOwner = true;
                for (int j = 0; j < i; j++) {
                    if (slots[j].Ticks > 0 && slots[j].Owner == owner) {
                        firstOfOwner = false;
                        break;
                    }
                }
                if (firstOfOwner) {
                    WraithCovenBurst.TryBurst(npc, this, owner);
                }
            }
        }

        public override void OnKill(NPC npc) => Clear();
    }
}
