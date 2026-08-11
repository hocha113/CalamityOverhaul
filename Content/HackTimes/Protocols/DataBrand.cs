using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 数据烙印：双态协议。剪贴板空 → 抄下目标前缀（不动原物）；
    /// 剪贴板非空 → 把记录的前缀确定性印到目标上，然后清空剪贴板。<br/>
    /// 与 <see cref="Reappraise"/> 的分工：那边是随机重掷（赌），这边是确定性转移（抄）。<br/>
    /// 联机结算全落在拥有者客户端：服务端看不到剪贴板，OnApply 只放行广播，
    /// owner 在 OnReplicatedApply 里定模式；写靶改的是世界掉落物，
    /// 客户端上行 <c>SyncItem</c> 与原版拾取/丢弃走同一条 msg21，权限语义一致
    /// </summary>
    internal class DataBrand : QuickHackDef
    {
        private static readonly Color BrandInk = new(210, 150, 255);

        /// <summary>HUD 剪贴板标签模板，DataBrandHudTag 用</summary>
        internal static LocalizedText HudTag { get; private set; }
        private static LocalizedText denyNoPrefix;
        private static LocalizedText denyIncompatible;

        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 6;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Item;
            UnlockedByDefault = false;

            HudTag = this.GetLocalization(nameof(HudTag), () => "烙印: {0}");
            denyNoPrefix = this.GetLocalization("DenyNoPrefix",
                () => "这件东西上没有前缀可抄");
            denyIncompatible = this.GetLocalization("DenyIncompatible",
                () => "记录的前缀贴不上这件东西");
        }

        public override void Unload() {
            base.Unload();
            HudTag = null;
            denyNoPrefix = null;
            denyIncompatible = null;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            //可堆叠物没有前缀语义；Prefix(-3) = 能否带前缀的只读检查
            return HackTargets.TryItem(target, out Item item)
                && item.maxStack == 1
                && (item.prefix > 0 || item.Prefix(-3));
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            if (!CanApplyTo(target)) return false;
            //模式校验只有拥有者本机有剪贴板可查；服务端对远程施术者只做基础校验，
            //owner 结算时用同一谓词再验一次（OwnerModeAllows），不存在双份逻辑
            if (caster == null || caster.whoAmI != Main.myPlayer) return true;
            return HackTargets.TryItem(target, out Item item)
                && OwnerModeAllows(caster, item);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryItem(target, out Item item, out int itemIndex)
                || caster == null) {
                return false;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                SettleOwner(caster, item, itemIndex);
            }
            //服务器不动物品：先记还是先写取决于施术者的剪贴板，那是 owner 端状态；
            //放行广播，owner 客户端在 OnReplicatedApply 里结算
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryItem(target, out Item item, out int itemIndex)) return;
            Player caster = HackEffectTracker.ResolveEffectCaster(this, target);
            if (caster?.active == true && caster.whoAmI == Main.myPlayer) {
                SettleOwner(caster, item, itemIndex);
                return;
            }
            //旁观者只看到一次数据脉冲，不区分记/写
            EmitPulse(item.Center);
        }

        #region 拥有者结算

        //记源与写靶的模式谓词，UI 预检与结算共用这一份
        private static bool OwnerModeAllows(Player caster, Item item) {
            if (!caster.TryGetModPlayer(out DataBrandPlayer brand)) return false;
            return brand.ClipboardPrefix <= 0
                ? item.prefix > 0
                : CanBrandOnto(item, brand.ClipboardPrefix);
        }

        private static bool CanBrandOnto(Item item, int prefixId)
            => item.stack == 1 && item.maxStack == 1
                && item.CanApplyPrefix(prefixId);

        private void SettleOwner(Player caster, Item item, int itemIndex) {
            if (!caster.TryGetModPlayer(out DataBrandPlayer brand)) return;

            //态一：记源。不消耗原物、不改原物，只抄前缀
            if (brand.ClipboardPrefix <= 0) {
                if (item.prefix <= 0) {
                    Deny(denyNoPrefix);
                    return;
                }
                brand.ClipboardPrefix = item.prefix;
                EmitRecord(item.Center, caster.Center);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.35f },
                    item.Center);
                return;
            }

            //态二：写靶
            int prefixId = brand.ClipboardPrefix;
            if (!CanBrandOnto(item, prefixId)) {
                //写失败保留剪贴板，这次 RAM 算学费
                Deny(denyIncompatible);
                return;
            }
            //探针先过一遍完整 Prefix：CanApplyPrefix 不含 ItemLoader.PrefixChance
            //的模组否决，先 ResetPrefix 再失败会白白洗掉靶子原有的前缀
            Item probe = new(item.type);
            if (!probe.Prefix(prefixId)) {
                Deny(denyIncompatible);
                return;
            }
            //Prefix 乘的是当前数值，不先清空就是连乘（同 Reappraise 的教训）
            item.ResetPrefix();
            item.Prefix(prefixId);
            brand.ClipboardPrefix = 0;
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.SyncItem, number: itemIndex);
            }
            EmitBrand(item.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.55f },
                item.Center);
        }

        private void Deny(LocalizedText reason) {
            if (Main.dedServ || reason == null) return;
            NotificationPopupSystem.Add(
                new HackTimeAccessDeniedEntry(DisplayName, reason));
        }

        #endregion

        #region 表现

        //记源：一串火花从掉落物爬向施术者，读作数据被抄走
        private static void EmitRecord(Vector2 from, Vector2 to) {
            Vector2 delta = to - from;
            int steps = (int)MathHelper.Clamp(delta.Length() / 26f, 3f, 22f);
            for (int i = 0; i <= steps; i++) {
                Vector2 pos = from + delta * (i / (float)steps);
                Vector2 vel = delta.SafeNormalize(Vector2.UnitY) * 1.2f;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, BrandInk, 0.65f)
                    ?.Configure(false, 15);
            }
        }

        //写靶：环形烙压 + 方形码点，读作被盖了一记印
        private static void EmitBrand(Vector2 center) {
            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f;
                Vector2 offset = angle.ToRotationVector2() * 22f;
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    -offset * 0.06f, BrandInk, 0.8f)
                    ?.Configure(false, 20);
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.2f, 1.2f);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel, BrandInk,
                    Main.rand.NextFloat(4f, 7f))
                    ?.Configure(Color.Lerp(BrandInk, Color.White, 0.4f), 24);
            }
        }

        private static void EmitPulse(Vector2 center) {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                PRTLoader.NewParticle<PRT_Spark>(center,
                    angle.ToRotationVector2() * 1.6f, BrandInk, 0.6f)
                    ?.Configure(false, 14);
            }
        }

        #endregion
    }
}
