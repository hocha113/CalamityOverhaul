using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
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

        /// <summary>召唤点距玩家的横向上限</summary>
        private const float SummonRangeX = 600f;

        //本机乐观锁：召唤/遣返后的短冷却，防连点
        private uint localLockUntil;

        public static LocalizedText ServantNoMemory { get; private set; }
        public static LocalizedText ServantUnknown { get; private set; }
        public static LocalizedText ServantBusy { get; private set; }
        public static LocalizedText MemoryStamp { get; private set; }

        public override void SetStaticDefaults() {
            ServantNoMemory = this.GetLocalization(nameof(ServantNoMemory), () => "湖还没收过活物");
            ServantUnknown = this.GetLocalization(nameof(ServantUnknown), () => "湖还没学会驱使它");
            ServantBusy = this.GetLocalization(nameof(ServantBusy), () => "湖底的手还没缓过来");
            MemoryStamp = this.GetLocalization(nameof(MemoryStamp), () => "湖记住了它");
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
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //轻声确认拍：湖把它收进了记性里
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, Player.Center);
            CombatText.NewText(Player.Hitbox, new Color(190, 84, 80), MemoryStamp.Value);
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

        /// <summary>同一个键：场上有自己的鬼奴就遣返，没有就试着召</summary>
        private void ToggleServant() {
            //先找场上属于自己的鬼奴（穷举实现共用 IKikasaServant 报到）
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Player.whoAmI
                    && proj.ModProjectile is IKikasaServant servant) {
                    servant.BeginDismiss();
                    localLockUntil = Main.GameUpdateCount + 30;
                    return;
                }
            }

            if (Main.GameUpdateCount < localLockUntil) {
                Refuse(ServantBusy);
                return;
            }
            if (!Player.GetModPlayer<KikasaVaultPlayer>().LakeReady) {
                Refuse(KikasaVaultPlayer.LakeNotReady);
                return;
            }
            if (LastDrownedType <= NPCID.None) {
                Refuse(ServantNoMemory);
                return;
            }
            if (!KikasaServantIndex.TryGet(LastDrownedType, out KikasaServantIndex.ServantSpawner spawner)) {
                Refuse(ServantUnknown);
                return;
            }

            //出水点：光标横位钳在玩家近旁，纵位就是湖面
            KikasaDomainPlayer domain = Player.GetModPlayer<KikasaDomainPlayer>();
            float x = MathHelper.Clamp(Main.MouseWorld.X,
                Player.Center.X - SummonRangeX, Player.Center.X + SummonRangeX);
            spawner(Player, new Vector2(x, domain.LakeWorldY));
            localLockUntil = Main.GameUpdateCount + 45;
        }

        private void Refuse(LocalizedText text) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, Player.Center);
            if (Main.netMode != NetmodeID.Server && text != null) {
                CombatText.NewText(Player.Hitbox, new Color(190, 84, 80), text.Value);
            }
        }

        //==================== 存档 ====================

        public override void SaveData(TagCompound tag) {
            if (LastDrownedType <= NPCID.None) {
                return;
            }
            if (LastDrownedType < NPCID.Count) {
                tag["KikasaServantMemory"] = LastDrownedType;
            }
            else if (NPCLoader.GetNPC(LastDrownedType) is ModNPC modNPC) {
                //模组 NPC 的类型号跨会话不稳定，存全名
                tag["KikasaServantMemoryName"] = modNPC.FullName;
            }
        }

        public override void LoadData(TagCompound tag) {
            LastDrownedType = 0;
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
        /// <summary>进入溶解遣返；从任意状态可达，重复调用无害</summary>
        void BeginDismiss();
    }
}
