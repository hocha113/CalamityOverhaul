using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.MimicPerchedAuxBrains
{
    /// <summary>
    /// 拟态副脑 ModPlayer，四幻象生成/冷却/FreeDodge
    /// <br/>仅本机 RequestRespawnPhantoms，ai[0]=槽位索引
    /// </summary>
    internal class MimicPerchedAuxBrainPlayer : ModPlayer
    {
        /// <summary>幻象数量 4</summary>
        public const int PhantomCount = 4;

        /// <summary>冷却剩余帧</summary>
        public int TriggerCooldownTimer;
        private float triggerCooldownCarry;

        /// <summary>混乱视觉剩余帧，触发后 30</summary>
        public int ChaosVisualTimer;
        private float chaosVisualCarry;

        /// <summary>上帧是否装备，检测切换</summary>
        private bool wasEquippedLastFrame;

        /// <summary>获取装备的拟态副脑，未装备 null</summary>
        public static MimicPerchedAuxBrain GetEquipped(Player player) {
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is MimicPerchedAuxBrain mimic) {
                    return mimic;
                }
            }
            return null;
        }

        /// <summary>场上是否已有指定槽位幻象</summary>
        public static bool HasPhantom(Player player, int phantomSlot) {
            int type = ModContent.ProjectileType<MimicPhantom>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner == player.whoAmI && p.type == type && (int)p.ai[0] == phantomSlot) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>清除该玩家全部幻象</summary>
        public static void ClearPhantoms(Player player) {
            int type = ModContent.ProjectileType<MimicPhantom>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner == player.whoAmI && p.type == type) {
                    p.Kill();
                }
            }
        }

        /// <summary>补满缺失幻象，仅本机</summary>
        public static void RequestRespawnPhantoms(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            MimicPerchedAuxBrain equipped = GetEquipped(player);
            if (equipped == null) {
                return;
            }
            int type = ModContent.ProjectileType<MimicPhantom>();
            for (int slot = 0; slot < PhantomCount; slot++) {
                if (HasPhantom(player, slot)) {
                    continue;
                }
                Vector2 offset = MimicPhantom.GetOrbitOffset(slot, 0f, equipped.OrbitRadius);
                Projectile.NewProjectile(new EntitySource_ItemUse(player, new Item()), player.Center + offset, Vector2.Zero
                    , type, 1, 0f, player.whoAmI, ai0: slot);
            }
        }

        public override void ResetEffects() {
            //冷却与混乱视觉计时持续递减
            BaseCyberware.TickFrameDown(ref TriggerCooldownTimer, ref triggerCooldownCarry);
            BaseCyberware.TickFrameDown(ref ChaosVisualTimer, ref chaosVisualCarry);
        }

        public override void PostUpdate() {
            //每帧检测装备状态，自动维持四个幻象数量
            bool equipped = GetEquipped(Player) != null;

            if (equipped && Player.whoAmI == Main.myPlayer) {
                //冷却结束后自动补全缺失的幻象
                if (TriggerCooldownTimer <= 0) {
                    RequestRespawnPhantoms(Player);
                }
            }

            if (wasEquippedLastFrame && !equipped && Player.whoAmI == Main.myPlayer) {
                ClearPhantoms(Player);
            }
            wasEquippedLastFrame = equipped;
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            MimicPerchedAuxBrain equipped = GetEquipped(Player);
            if (equipped == null) {
                return false;
            }
            if (TriggerCooldownTimer > 0) {
                return false;
            }
            //至少要有一个存活的幻象才能借身闪避
            if (!AnyPhantomAlive()) {
                return false;
            }

            int sourceDamage = info.SourceDamage;
            int phantomDamage = (int)(sourceDamage * equipped.DamageScaling);
            if (phantomDamage < 1) {
                phantomDamage = 1;
            }

            //尝试解析袭击者实体
            Vector2 attackerCenter = Player.Center;
            int attackerNpcIndex = -1;
            if (info.DamageSource.TryGetCausingEntity(out Entity entity)) {
                attackerCenter = entity.Center;
                if (entity is NPC npc) {
                    attackerNpcIndex = npc.whoAmI;
                }
            }

            //命令所有存活的幻象进入冲撞状态
            CommandPhantomsRush(attackerCenter, attackerNpcIndex, phantomDamage);

            TriggerCooldownTimer = equipped.TriggerCooldown;
            triggerCooldownCarry = 0f;
            ChaosVisualTimer = 30;
            chaosVisualCarry = 0f;

            //制造一阵混乱粒子作为视觉反馈
            for (int i = 0; i < 24; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(Player.Center, Terraria.ID.DustID.ShadowbeamStaff, dustVel, 100, default, 1.4f);
                dust.noGravity = true;
            }

            return true;
        }

        /// <summary>是否仍有 Orbit 态幻象存活</summary>
        private bool AnyPhantomAlive() {
            int type = ModContent.ProjectileType<MimicPhantom>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner == Player.whoAmI && p.type == type && p.ModProjectile is MimicPhantom phantom && phantom.State == MimicPhantom.PhantomState.Orbit) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>命令 Orbit 幻象切换 Rush</summary>
        private void CommandPhantomsRush(Vector2 attackerCenter, int attackerNpcIndex, int phantomDamage) {
            int type = ModContent.ProjectileType<MimicPhantom>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner != Player.whoAmI || p.type != type) {
                    continue;
                }
                if (p.ModProjectile is MimicPhantom phantom && phantom.State == MimicPhantom.PhantomState.Orbit) {
                    phantom.BeginRush(attackerCenter, attackerNpcIndex, phantomDamage);
                }
            }
        }
    }
}
