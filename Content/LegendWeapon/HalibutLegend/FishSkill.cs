using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal abstract class FishSkill : VaultType<FishSkill>, ILocalizedModType
    {
        public string LocalizationCategory => "FishSkill";
        public LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), () => "");
        public LocalizedText Tooltip => this.GetLocalization(nameof(Tooltip), () => "");
        public readonly static Dictionary<Type, int> TypeToID = [];
        public readonly static Dictionary<Type, Texture2D> TypeToTex = [];
        public readonly static Dictionary<int, FishSkill> UnlockFishs = [];
        public int ID => TypeToID[GetType()];
        public virtual string IconTexture => CWRConstant.UI + "Halibut/FishSkill/" + Name;
        public Texture2D Icon => TypeToTex[GetType()];
        /// <summary>
        /// 研究什么鱼才能得到这个技能？
        /// </summary>
        public int UnlockFishID => ItemID.None;
        protected override void VaultRegister() {
            TypeToID[GetType()] = Instances.Count;
            Instances.Add(this);
            if (!VaultUtils.isServer) {
                TypeToTex[GetType()] = ModContent.Request<Texture2D>(IconTexture, AssetRequestMode.ImmediateLoad).Value;
            }
        }

        public override void VaultSetup() {
            _ = DisplayName;
            _ = Tooltip;
            UnlockFishs[UnlockFishID] = this;
            SetStaticDefaults();
            SetDefaults(true);
        }

        public virtual void SetDefaults(bool create = false) {

        }

        public virtual void Use(Item item, Player player) {

        }
    }
}
