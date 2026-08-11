using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 身份伪装：让这件掉落物在敌人眼里像玩家，替施术者吸三十秒仇恨。<br/>
    /// 诱饵登记表在权威端与每个客户端各存一份（客户端由复制生命周期喂），
    /// 这样 <see cref="EntityMasqueradeNPC"/> 的位置伪装 pass 和
    /// <see cref="EntityMasqueradeItem"/> 的拾取闸在每个端都能本地判定。<br/>
    /// 恢复路径见 <see cref="ReleaseDecoy"/> 上的清单
    /// </summary>
    internal class EntityMasquerade : QuickHackDef
    {
        /// <summary>诱饵的仇恨半径（像素）</summary>
        internal const float LureRadius = 800f;
        private const int DurationFrames = 1800;

        private static readonly Color Hologram = new(120, 255, 190);

        private sealed class DecoyState
        {
            public int ItemType;
            public int CasterIndex;
            public int OrigRare;
            public bool RareBumped;
        }

        //Main.item 槽位 → 诱饵；槽位在联机里全局同步，可作跨端键，
        //但取用前必须验 ItemType 快照，防槽位被新掉落物复用后继承旧登记
        private static readonly Dictionary<int, DecoyState> decoys = [];
        private static readonly List<int> staleBuf = [];

        public override void SetDefaults() {
            UploadTime = 110;
            RamCost = 4;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Item;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => DurationFrames;

        public override void Unload() {
            base.Unload();
            decoys.Clear();
            staleBuf.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryItem(target, out Item item, out int index)) return false;
            //白天会自然消失的落星当不了三十秒的诱饵
            if (item.type == ItemID.FallenStar) return false;
            return !IsActiveDecoySlot(index);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryItem(target, out Item item, out int index)
                || caster == null) {
                return false;
            }
            RegisterDecoy(index, item, caster.whoAmI, authority: true);
            if (Main.netMode != NetmodeID.Server) EmitCloak(item.Center);
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryItem(target, out Item item, out int index)) return false;
            if (!decoys.TryGetValue(index, out DecoyState state)) return false;
            //槽位被别的物品占了：登记作废，效果收队；新物品不做任何还原
            if (item.type != state.ItemType) {
                decoys.Remove(index);
                return false;
            }
            //防偷怪：原版每帧递减，钉在 2 就永远到不了触发线
            item.timeLeftInWhichTheItemCannotBeTakenByEnemies = 2;
            if (!Main.dedServ) TickPresentation(item, elapsed);
            return true;
        }

        /// <summary>
        /// 恢复路径清单：<br/>
        /// ① 到期 / 施术者死亡或离线 → 追踪器调 OnRemove（目标有效）→ 本方法：
        ///    还原稀有度、清防偷标记、向上弹出、服务器补发 SyncItem；<br/>
        /// ② 物品意外消失（岩浆烧不掉但可能被物品上限顶掉）→ 目标失效，
        ///    追踪器跳过 OnRemove → 登记表由 OnTick / 读取路径的类型校验惰性清掉；<br/>
        /// ③ 效果期内拾取被 <see cref="EntityMasqueradeItem.CanPickup"/> 挡死，
        ///    若别的系统绕过闸直接拿走 → 走 ②；<br/>
        /// ④ 世界卸载 → <see cref="EntityMasqueradeSystem.ClearWorld"/> 清整表；<br/>
        /// ⑤ Mod 卸载 → <see cref="Unload"/> 清整表；<br/>
        /// ⑥ 客户端快照重建 / 效果移除广播 → OnReplicatedRemove 清本机登记
        /// </summary>
        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryItem(target, out Item item, out int index)) return;
            ReleaseDecoy(index, item, pop: true);
            if (Main.netMode != NetmodeID.Server) EmitRelease(item.Center);
            if (VaultUtils.isServer) {
                //弹出速度与抓取延迟要让客户端看到
                NetMessage.SendData(MessageID.SyncItem, number: index);
            }
        }

        #region 复制端

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryItem(target, out Item item, out int index)) return;
            Player caster = HackEffectTracker.ResolveEffectCaster(this, target);
            //施术者解析不到就不登记：权威端很快会终止该效果，本机不用抢跑
            if (caster == null) return;
            RegisterDecoy(index, item, caster.whoAmI, authority: false);
            EmitCloak(item.Center);
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryItem(target, out Item item, out int index)) return;
            if (!decoys.TryGetValue(index, out DecoyState state)) return;
            if (item.type != state.ItemType) {
                decoys.Remove(index);
                return;
            }
            item.timeLeftInWhichTheItemCannotBeTakenByEnemies = 2;
            TickPresentation(item, elapsed);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!HackTargets.TryItem(target, out Item item, out int index)) return;
            //弹出与稀有度由权威端的 SyncItem 带过来，本机只撤登记
            ReleaseDecoy(index, item, pop: false);
            EmitRelease(item.Center);
        }

        #endregion

        #region 登记表

        private static void RegisterDecoy(int index, Item item, int casterIndex,
            bool authority) {
            DecoyState state = new() {
                ItemType = item.type,
                CasterIndex = casterIndex,
                OrigRare = item.rare,
            };
            //岩浆只烧 rare == 0 的掉落物，权威端垫一档稀有度换岩浆豁免；
            //rare 不进 msg21（客户端由 netDefaults+Prefix 重建），无需同步
            if (authority && item.rare == 0) {
                item.rare = 1;
                state.RareBumped = true;
            }
            decoys[index] = state;
        }

        private static void ReleaseDecoy(int index, Item item, bool pop) {
            if (!decoys.TryGetValue(index, out DecoyState state)) return;
            decoys.Remove(index);
            //槽位已换人就不动新物品
            if (item.type != state.ItemType) return;
            if (state.RareBumped && item.rare == 1) {
                item.rare = state.OrigRare;
            }
            if (pop) {
                //到期弹出，免得压在敌群里够不着
                item.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -5.5f);
                item.noGrabDelay = 0;
            }
        }

        internal static bool HasAnyDecoy => decoys.Count > 0;

        /// <summary>
        /// 取该玩家名下、离 npcCenter 最近且在 <see cref="LureRadius"/> 内的诱饵落点；
        /// 顺手把失效条目清掉（惰性清账是恢复路径 ② 的兜底）
        /// </summary>
        internal static bool TryGetLureAnchor(int casterIndex, Vector2 npcCenter,
            out Vector2 anchor) {
            anchor = default;
            float best = LureRadius * LureRadius;
            bool found = false;
            staleBuf.Clear();
            foreach ((int slot, DecoyState state) in decoys) {
                Item item = Main.item[slot];
                if (item?.active != true || item.type != state.ItemType) {
                    staleBuf.Add(slot);
                    continue;
                }
                if (state.CasterIndex != casterIndex) continue;
                float distSq = Vector2.DistanceSquared(item.Center, npcCenter);
                if (distSq < best) {
                    best = distSq;
                    anchor = item.Center;
                    found = true;
                }
            }
            for (int i = 0; i < staleBuf.Count; i++) {
                decoys.Remove(staleBuf[i]);
            }
            staleBuf.Clear();
            return found;
        }

        /// <summary>这件实例是不是活跃诱饵；引用比对，不信任 <c>Item.whoAmI</c></summary>
        internal static bool IsDecoyInstance(Item item) {
            if (item == null || decoys.Count == 0) return false;
            foreach ((int slot, DecoyState state) in decoys) {
                if (!ReferenceEquals(Main.item[slot], item)) continue;
                return item.active && item.type == state.ItemType;
            }
            return false;
        }

        private static bool IsActiveDecoySlot(int index) {
            if (!decoys.TryGetValue(index, out DecoyState state)) return false;
            Item item = Main.item[index];
            if (item?.active == true && item.type == state.ItemType) return true;
            decoys.Remove(index);
            return false;
        }

        internal static void ResetAll() {
            decoys.Clear();
            staleBuf.Clear();
        }

        #endregion

        #region 表现

        //持续期：微光呼吸 + 偶发一粒上浮的全息方块。
        //签名级的扫描线人形轮廓留给 polish（见交付报告待办）
        private static void TickPresentation(Item item, int elapsed) {
            Lighting.AddLight(item.Center, Hologram.ToVector3() * 0.22f);
            if (elapsed % 9 != 0) return;
            Vector2 pos = item.Center
                + new Vector2(Main.rand.NextFloat(-12f, 12f), 6f);
            PRTLoader.NewParticle<PRT_CyberSquare>(pos,
                new Vector2(0f, Main.rand.NextFloat(-0.9f, -0.4f)), Hologram,
                Main.rand.NextFloat(3f, 5f))
                ?.Configure(Color.Lerp(Hologram, Color.White, 0.35f), 30);
        }

        //披上伪装：一圈全息环收拢
        private static void EmitCloak(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 offset = angle.ToRotationVector2() * 30f;
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    -offset * 0.05f, Hologram, 0.75f)
                    ?.Configure(false, 22);
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(center,
                    Main.rand.NextVector2Circular(1f, 1f), Hologram,
                    Main.rand.NextFloat(4f, 7f))
                    ?.Configure(Color.Lerp(Hologram, Color.White, 0.35f), 28);
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, center);
        }

        //卸下伪装：小规模散场
        private static void EmitRelease(Vector2 center) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f)
                    + new Vector2(0f, -1f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Hologram, 0.6f)
                    ?.Configure(true, 18);
            }
            SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.2f }, center);
        }

        #endregion
    }

    /// <summary>切世界清诱饵表；坐标与槽位都属于上一个世界</summary>
    internal sealed class EntityMasqueradeSystem : ModSystem
    {
        public override void ClearWorld() => EntityMasquerade.ResetAll();
    }

    /// <summary>伪装期间的掉落物保护：任何人都拿不起来</summary>
    internal sealed class EntityMasqueradeItem : GlobalItem
    {
        public override bool CanPickup(Item item, Player player)
            => !EntityMasquerade.IsDecoyInstance(item);
    }
}
