using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Graphics;
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
    /// <summary>鱼技能基类</summary>
    public abstract class FishSkill : VaultType<FishSkill>, ILocalizedModType
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
        public virtual int ResearchDuration => 60 * 20;
        public virtual string IconTexture => CWRConstant.UI + "FishSkill/" + Name;
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
        /// <summary>解锁所需鱼物品 ID</summary>
        public virtual int UnlockFishID => ItemID.None;
        /// <summary>图鉴深度带 0–3，-1 走分配表/稀有度回退</summary>
        public virtual int AtlasTier => -1;
        protected override void VaultRegister() {
            Instances.Add(this);
            TypeToID[GetType()] = Instances.Count;
            IDToInstance[Instances.Count] = this;//技能ID从1开始，0什么都不是
            NameToInstance[FullName] = this;
            if (!VaultUtils.isServer) {
                TypeToTex[GetType()] = CWRUtils.GetT2DAsset(IconTexture, true).Value;
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

        public override void Unload() {
            TypeToID.Clear();
            TypeToTex.Clear();
            UnlockFishs.Clear();
            IDToInstance.Clear();
            NameToInstance.Clear();
            cooldownCarries = null;
        }

        private static float[] cooldownCarries;

        /// <summary>按 <see cref="TimeGear.TimeScale"/> 递减技能冷却</summary>
        internal void TickCooldownDown() {
            if (Cooldown <= 0) {
                return;
            }
            EnsureCooldownCarryArray();
            TimeGear.ConsumeFrames(ref Cooldown, ref cooldownCarries[ID]);
        }

        /// <summary>按 TimeGear 递减帧计时</summary>
        internal static void ConsumeScaled(ref int frames, ref float carry)
            => TimeGear.ConsumeFrames(ref frames, ref carry);

        /// <summary>按 TimeGear 递增帧计时</summary>
        internal static void AdvanceScaled(ref int frames, ref float carry) {
            frames += TimeGear.PullFrameAdvance(ref carry);
        }

        private void EnsureCooldownCarryArray() {
            if (cooldownCarries == null || cooldownCarries.Length <= ID) {
                Array.Resize(ref cooldownCarries, Math.Max(ID + 1, Instances.Count + 1));
            }
        }

        private void ResetCooldownCarry() {
            if (cooldownCarries != null && ID > 0 && ID < cooldownCarries.Length) {
                cooldownCarries[ID] = 0f;
            }
        }

        public static T GetT<T>() where T : FishSkill {
            if (IDToInstance.TryGetValue(TypeToID[typeof(T)], out var skill) && skill is T t) {
                return t;
            }
            return null;
        }

        public void SetCooldown() {
            Cooldown = (int)MathHelper.Clamp(DefaultCooldown, 1, 32767);
            ResetCooldownCarry();
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
        /// <summary>冷却 tick，false 时停止递减</summary>
        public virtual bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            return true;
        }
        /// <summary>是否为当前装备技能</summary>
        public virtual bool Active(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            return halibutPlayer.SkillID == ID && halibutPlayer.HeldHalibut;
        }
        /// <summary>类型 T 是否为当前装备技能</summary>
        public static bool IsActive<T>(Player player) where T : FishSkill => GetT<T>().Active(player);
    }
}
