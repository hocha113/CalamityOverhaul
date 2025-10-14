using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal abstract class FishSkill : VaultType<FishSkill>, ILocalizedModType
    {
        public string LocalizationCategory => "FishSkill";
        public LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), () => "");
        public LocalizedText Tooltip => this.GetLocalization(nameof(Tooltip), () => "");
        public LocalizedText Studied => this.GetLocalization(nameof(Studied), () => "");
        public readonly static Dictionary<Type, int> TypeToID = [];
        public readonly static Dictionary<Type, Texture2D> TypeToTex = [];
        public readonly static Dictionary<int, FishSkill> UnlockFishs = [];
        public readonly static Dictionary<int, FishSkill> IDToInstance = [];
        public readonly static Dictionary<string, FishSkill> NameToInstance = [];
        public int ID => TypeToID[GetType()];
        public virtual string IconTexture => CWRConstant.UI + "Halibut/FishSkill/" + Name;
        public Texture2D Icon => TypeToTex[GetType()];
        /// <summary>
        /// 技能冷却时间，单位为帧
        /// </summary>
        public int Cooldown;
        /// <summary>
        /// 技能的基础冷却时间，单位为帧
        /// </summary>
        public virtual int DefaultCooldown => 60;
        /// <summary>
        /// 技能冷却比例，1表示完全冷却，0表示刚使用完
        /// </summary>
        public float CooldownRatio => Cooldown / (float)DefaultCooldown;
        /// <summary>
        /// 研究什么鱼才能得到这个技能？
        /// </summary>
        public virtual int UnlockFishID => ItemID.None;
        protected override void VaultRegister() {
            Instances.Add(this);
            TypeToID[GetType()] = Instances.Count;
            IDToInstance[Instances.Count] = this;//技能ID从1开始，0什么都不是
            NameToInstance[FullName] = this;
            if (!VaultUtils.isServer) {
                TypeToTex[GetType()] = ModContent.Request<Texture2D>(IconTexture, AssetRequestMode.ImmediateLoad).Value;
            }
        }

        public override void VaultSetup() {
            _ = DisplayName;
            _ = Tooltip;
            _ = Studied;
            UnlockFishs[UnlockFishID] = this;
            SetStaticDefaults();
            SetDefaults(true);
        }

        public static T GetT<T>() where T : FishSkill {
            if (IDToInstance.TryGetValue(TypeToID[typeof(T)], out var skill) && skill is T t) {
                return t;
            }
            return null;
        }

        public void SetCooldown() {
            Cooldown = DefaultCooldown;
        }

        public virtual void SaveData(TagCompound tag) {

        }

        public virtual void LoadData(TagCompound tag) {

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

        public virtual bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            return true;
        }

        public virtual bool Active(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            return halibutPlayer.SkillID == ID && halibutPlayer.HeldHalibut;
        }
    }
}
