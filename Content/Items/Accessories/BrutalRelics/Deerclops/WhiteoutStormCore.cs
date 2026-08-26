using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops
{
    /// <summary>
    /// 白视风暴核：独眼巨鹿残酷遗物。把它的白澈长嚎反转成玩家能力——
    /// 受击自动引爆白化风暴(清明圈以你为心)，奔跑时向行进方向掀起破土冰刺浪
    /// </summary>
    internal class WhiteoutStormCore : BaseBrutalRelic
    {
        /// <summary>风暴持续(6s)</summary>
        internal const int StormTicks = 360;
        /// <summary>内置冷却(20s)</summary>
        internal const int CooldownTicks = 1200;
        /// <summary>爆发基础伤害</summary>
        internal const int BurstDamage = 180;
        /// <summary>冰刺基础伤害</summary>
        internal const int SpikeDamage = 80;
        /// <summary>爆发最大半径(px)</summary>
        internal const float BurstRadius = 500f;
        /// <summary>风暴中额外减伤</summary>
        internal const float StormEndurance = 0.5f;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.defense = 8;
            //同期巨鹿掉落(售约2金/购约10金)的3.5倍
            Item.value = Item.buyPrice(0, 35, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.endurance += 0.12f;
            player.buffImmune[BuffID.Chilled] = true;
            player.buffImmune[BuffID.Frozen] = true;
            player.buffImmune[BuffID.Frostburn] = true;
            player.buffImmune[BuffID.Frostburn2] = true;

            WhiteoutStormPlayer mp = player.GetModPlayer<WhiteoutStormPlayer>();
            mp.Equipped = true;
            mp.SourceItem = Item;
        }
    }

    /// <summary>白视风暴增益：风暴期间高额减伤+击退免疫，图标复用物品贴图</summary>
    internal class WhiteoutStormBuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "WhiteoutStormCore";

        private LocalizedText displayNameCache;
        private LocalizedText descriptionCache;
        public override LocalizedText DisplayName
            => displayNameCache ??= this.GetLocalization(nameof(DisplayName), () => "白视风暴");
        public override LocalizedText Description
            => descriptionCache ??= this.GetLocalization(nameof(Description), () => "获得50%减伤与击退免疫，雪幕不遮挡你的视野");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.endurance += WhiteoutStormCore.StormEndurance;
            player.noKnockback = true;
            player.GetModPlayer<WhiteoutStormPlayer>().StormActive = true;
            //风暴随行演出：各端都按同步的buff自播，旁观者同样看得见
            if (!Main.dedServ) {
                WhiteoutVeilFX.EmitStormAmbient(player);
            }
        }
    }

    /// <summary>
    /// 白视风暴核逐玩家状态。触发/冷却/刺浪全在实例字段；
    /// 受击触发与刺浪生成只在所有者端执行，弹幕经生成包自同步
    /// </summary>
    internal class WhiteoutStormPlayer : ModPlayer
    {
        /// <summary>本帧装备状态(物品钩子逐帧点亮)</summary>
        public bool Equipped;
        /// <summary>本帧风暴激活(buff逐帧点亮)</summary>
        public bool StormActive;
        /// <summary>触发源物品(实体来源用，逐帧刷新)</summary>
        public Item SourceItem;
        /// <summary>风暴内置冷却(仅所有者端有意义)</summary>
        public int StormCooldown;

        /// <summary>刺浪节拍</summary>
        private int spikeTimer;
        /// <summary>持续奔跑计帧(防单步误触)</summary>
        private int runTimer;
        /// <summary>触地宽限帧(斜坡/小跳不断浪)</summary>
        private int groundGrace;

        public override void ResetEffects() {
            Equipped = false;
            StormActive = false;
            SourceItem = null;
        }

        public override void OnHurt(Player.HurtInfo info) {
            //受击自动反制：仅所有者端触发，弹幕/buff经原版同步各端自现
            if (!Equipped || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (StormCooldown > 0 || Player.dead || info.Damage <= 0) {
                return;
            }
            TriggerStorm();
        }

        private void TriggerStorm() {
            StormCooldown = WhiteoutStormCore.CooldownTicks;
            Player.AddBuff(ModContent.BuffType<WhiteoutStormBuff>(), WhiteoutStormCore.StormTicks);

            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(WhiteoutStormCore.BurstDamage);
            IEntitySource source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc(nameof(WhiteoutStormCore));
            Projectile.NewProjectile(source, Player.Center, Vector2.Zero,
                ModContent.ProjectileType<WhiteoutBurstProj>(), damage, 8f, Player.whoAmI);
        }

        public override void PostUpdateEquips() {
            TickCooldown(allowCue: true);
            if (Equipped && !Player.dead) {
                UpdateSpikeWave();
            }
            else {
                runTimer = 0;
                spikeTimer = 0;
            }
        }

        //死亡期间冷却照常回转(PostUpdate系钩子死亡不跑)
        public override void UpdateDead() {
            TickCooldown(allowCue: false);
            runTimer = 0;
            spikeTimer = 0;
            groundGrace = 0;
        }

        private void TickCooldown(bool allowCue) {
            if (StormCooldown <= 0) {
                return;
            }
            StormCooldown--;
            //冷却转好：所有者本地一声冰鸣+霜晶环提示
            if (StormCooldown == 0 && allowCue && Equipped
                && Player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.25f, Volume = 0.6f }, Player.Center);
                WhiteoutVeilFX.EmitReadyCue(Player);
            }
        }

        #region 刺浪(巨鹿刺笼语汇的行进版)
        private void UpdateSpikeWave() {
            //弹幕由玩家动作触发，只在所有者端生成
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (Player.velocity.Y == 0f) {
                groundGrace = 8;
            }
            else if (groundGrace > 0) {
                groundGrace--;
            }

            bool running = Math.Abs(Player.velocity.X) >= 2.5f && groundGrace > 0;
            if (running) {
                runTimer++;
            }
            else {
                runTimer = 0;
            }
            if (runTimer < 18) {
                return;
            }

            spikeTimer++;
            if (spikeTimer < 45) {
                return;
            }
            spikeTimer = 0;
            SpawnSpikeWave();
        }

        /// <summary>向行进方向掀起一排破土冰刺，逐根外扩、沿地形起伏</summary>
        private void SpawnSpikeWave() {
            int dir = Math.Sign(Player.velocity.X);
            if (dir == 0) {
                dir = Player.direction;
            }

            Point feet = Player.Bottom.ToTileCoordinates();
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(WhiteoutStormCore.SpikeDamage);
            IEntitySource source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc(nameof(WhiteoutStormCore));

            for (int i = 0; i < 6; i++) {
                int tileX = feet.X + dir * (3 + i * 2);
                int surfaceY = RelicIceSpikeProj.FindSurfaceTileY(tileX, feet.Y);
                if (!WorldGen.ActiveAndWalkableTile(tileX, surfaceY)) {
                    continue;
                }
                Vector2 pos = new Vector2(tileX * 16f + 8f, surfaceY * 16f - 8f);
                //前倾角随浪头递增，读作向前掀起
                float lean = dir * (0.10f + i * 0.035f);
                Vector2 axis = (-Vector2.UnitY).RotatedBy(lean);
                float scale = Math.Min(0.8f + i * 0.06f, 1.15f);
                int telegraph = 10 + i * 3;
                //生成参数全走NewProjectile的ai槽，随生成包一次同步
                Projectile.NewProjectile(source, pos, axis,
                    ModContent.ProjectileType<RelicIceSpikeProj>(), damage, 4f, Player.whoAmI,
                    telegraph, scale, 0f);
            }
        }
        #endregion
    }
}
