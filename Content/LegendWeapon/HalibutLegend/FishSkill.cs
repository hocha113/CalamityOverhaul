﻿using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
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
        public readonly static Dictionary<int, FishSkill> IDToInstance = [];
        public int ID => TypeToID[GetType()];
        public virtual string IconTexture => CWRConstant.UI + "Halibut/FishSkill/" + Name;
        public Texture2D Icon => TypeToTex[GetType()];
        /// <summary>
        /// 研究什么鱼才能得到这个技能？
        /// </summary>
        public virtual int UnlockFishID => ItemID.None;
        protected override void VaultRegister() {
            Instances.Add(this);
            TypeToID[GetType()] = Instances.Count;
            IDToInstance[Instances.Count] = this;//技能ID从1开始，0什么都不是
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

        public virtual bool? CanUseItem(Item item, Player player) {
            return null;
        }

        public virtual bool? AltFunctionUse(Item item, Player player) {
            return null;
        }

        public virtual bool? UseItem(Item item, Player player) {
            return null;
        }

        public virtual bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return null;
        }

        public virtual bool? ShootAlt(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return null;
        }
    }
}
