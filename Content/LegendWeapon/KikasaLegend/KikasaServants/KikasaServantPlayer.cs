using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼伞·能力复制玩家态。湖永久记住最后一只被沉溺的生物（随存档保存，
    /// 储钱罐语义只活在所有者本机），按 <see cref="CWRKeySystem.Kikasa_Summon"/>
    /// 召唤对应的鬼奴为己驱使；再按一次遣返。记录在沉溺权威完成帧入账
    /// （单机直写、联机走 KikasaDrownNet 的完成通报），与演出层无耦合
    /// </summary>
    public class KikasaServantPlayer : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        /// <summary>湖记住的生物类型，0=还没记住过；只在所有者本机有意义</summary>
        public int LastDrownedType { get; private set; }

        /// <summary>湖记住的武器物品类型，0=没记住过；与生物记忆互斥覆盖，只认最后沉的那个</summary>
        public int LastDrownedItemType { get; private set; }

        /// <summary>召唤点距玩家的横向上限</summary>
        private const float SummonRangeX = 600f;

        //本机乐观锁：召唤/遣返后的短冷却，防连点
        private uint localLockUntil;

        public static LocalizedText ServantUnknown { get; private set; }

        public override void SetStaticDefaults() {
            ServantUnknown = this.GetLocalization(nameof(ServantUnknown), () => "湖还没学会驱使它");
        }

        //==================== 记录 ====================

        /// <summary>
        /// 沉溺权威完成帧的入账口：覆盖式记忆，只认最后一只。
        /// 所有者本机之外调用无害但无意义（数据不外播）
        /// </summary>
        internal void RecordDrowned(int npcType) {
            if (npcType <= NPCID.None || npcType >= NPCLoader.NPCCount) {
                return;
            }
            LastDrownedType = npcType;
            LastDrownedItemType = 0;
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //轻声确认拍：湖把它收进了记性里
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, Player.Center);
        }

        /// <summary>
        /// 已注册武器沉湖时的入账口：与生物记忆同一条覆盖式记性，最多一个非零。
        /// 由 KikasaVaultPlayer.TrySink 在入账帧调用（湖藏数据只活在所有者本机）
        /// </summary>
        internal void RecordDrownedItem(int itemType) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return;
            }
            if (!KikasaArmsIndex.TryGet(itemType, out _)) {
                return;
            }
            LastDrownedItemType = itemType;
            LastDrownedType = 0;
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //同一记确认拍：湖学会了驱使这批武器
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, Player.Center);
        }

        //==================== 输入 ====================

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }
            if (HackTime.Active) {
                return;
            }
            if (CWRKeySystem.Kikasa_Summon.JustPressed) {
                ToggleServant();
            }
        }

        /// <summary>场上属于此玩家的鬼奴（穷举实现共用 IKikasaServant 报到）；无则 null</summary>
        internal IKikasaServant FindActiveServant() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Player.whoAmI
                    && proj.ModProjectile is IKikasaServant servant) {
                    return servant;
                }
            }
            return null;
        }

        /// <summary>同一个键：场上有自己的鬼奴就遣返，没有就试着召</summary>
        private void ToggleServant() {
            IKikasaServant active = FindActiveServant();
            if (active != null) {
                //刚召完的短锁窗内不受理遣返：双击不该把出水一半的鬼奴按回去
                if (Main.GameUpdateCount < localLockUntil) {
                    return;
                }
                //已在溶解中：轻点一声表示没受理，别让按键静默吞掉
                if (active.IsDismissing) {
                    Refuse();
                    return;
                }
                active.BeginDismiss();
                localLockUntil = Main.GameUpdateCount + 30;
                return;
            }

            if (Main.GameUpdateCount < localLockUntil) {
                Refuse();
                return;
            }
            KikasaVaultPlayer vault = Player.GetModPlayer<KikasaVaultPlayer>();
            if (!vault.LakeReady) {
                Refuse();
                return;
            }

            //出水点：光标横位钳在玩家近旁，纵位就是湖面
            KikasaDomainPlayer domain = Player.GetModPlayer<KikasaDomainPlayer>();
            float x = MathHelper.Clamp(Main.MouseWorld.X,
                Player.Center.X - SummonRangeX, Player.Center.X + SummonRangeX);
            Vector2 emergeAt = new(x, domain.LakeWorldY);

            //械奴分支：湖最后记住的是武器——复制体数量按湖藏存量折算，原件不消耗
            if (LastDrownedItemType > ItemID.None) {
                if (!KikasaArmsIndex.TryGet(LastDrownedItemType, out KikasaArmsIndex.ArmsSpawner armsSpawner)) {
                    Refuse();
                    return;
                }
                int count = CountStoredArms(vault, LastDrownedItemType);
                if (count <= 0) {
                    //武器都被捞走了，湖里没有可凝形的原件
                    Refuse();
                    return;
                }
                armsSpawner(Player, emergeAt, count);
                localLockUntil = Main.GameUpdateCount + 45;
                return;
            }

            if (LastDrownedType <= NPCID.None) {
                Refuse();
                return;
            }
            if (!KikasaServantIndex.TryGet(LastDrownedType, out KikasaServantIndex.ServantSpawner spawner)) {
                Refuse();
                return;
            }
            spawner(Player, emergeAt);
            localLockUntil = Main.GameUpdateCount + 45;
        }

        /// <summary>湖藏里该武器的存量（计堆叠），械奴复制体数量的依据</summary>
        private static int CountStoredArms(KikasaVaultPlayer vault, int itemType) {
            int count = 0;
            foreach (Item item in vault.Stored) {
                if (item?.IsAir == false && item.type == itemType) {
                    count += Math.Max(item.stack, 1);
                }
            }
            return count;
        }

        private void Refuse() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, Player.Center);
        }

        //==================== 存档 ====================

        public override void SaveData(TagCompound tag) {
            if (LastDrownedType > NPCID.None) {
                if (LastDrownedType < NPCID.Count) {
                    tag["KikasaServantMemory"] = LastDrownedType;
                }
                else if (NPCLoader.GetNPC(LastDrownedType) is ModNPC modNPC) {
                    //模组 NPC 的类型号跨会话不稳定，存全名
                    tag["KikasaServantMemoryName"] = modNPC.FullName;
                }
            }
            if (LastDrownedItemType > ItemID.None) {
                if (LastDrownedItemType < ItemID.Count) {
                    tag["KikasaArmsMemory"] = LastDrownedItemType;
                }
                else if (ItemLoader.GetItem(LastDrownedItemType) is ModItem modItem) {
                    //模组物品同理存全名
                    tag["KikasaArmsMemoryName"] = modItem.FullName;
                }
            }
        }

        public override void LoadData(TagCompound tag) {
            LastDrownedType = 0;
            LastDrownedItemType = 0;
            //武器记忆：与生物记忆互斥，读到即定（写侧保证最多一个存在）
            if (tag.TryGet("KikasaArmsMemoryName", out string armsName)
                && ModContent.TryFind(armsName, out ModItem modItem)) {
                LastDrownedItemType = modItem.Type;
            }
            else if (tag.TryGet("KikasaArmsMemory", out int vanillaItem)
                && vanillaItem > ItemID.None && vanillaItem < ItemID.Count) {
                LastDrownedItemType = vanillaItem;
            }
            if (tag.TryGet("KikasaServantMemoryName", out string fullName)
                && ModContent.TryFind(fullName, out ModNPC modNPC)) {
                LastDrownedType = modNPC.Type;
                return;
            }
            if (tag.TryGet("KikasaServantMemory", out int vanillaType)
                && vanillaType > NPCID.None && vanillaType < NPCID.Count) {
                LastDrownedType = vanillaType;
            }
        }
    }

    /// <summary>场上鬼奴的公共报到面：遣返命令由所有者本机下达</summary>
    internal interface IKikasaServant
    {
        /// <summary>已在溶解遣返途中</summary>
        bool IsDismissing { get; }

        /// <summary>进入溶解遣返；从任意状态可达，重复调用无害</summary>
        void BeginDismiss();
    }
}
