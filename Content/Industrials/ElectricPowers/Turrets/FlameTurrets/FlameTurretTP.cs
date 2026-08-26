using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets
{
    /// <summary>
    /// 火焰喷射塔TP:近距锥形持续喷火,对群压制。开火期每帧耗电,火焰弹为普通
    /// ModProjectile 由权威端生成(spawn包天然广播);凝胶燃料槽提供伤害强化
    /// </summary>
    internal class FlameTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<FlameTurretTile>();
        public override int TargetItem => ModContent.ItemType<FlameTurret>();
        public override float MaxUEValue => 800;
        public override float AttackRange => 300;
        /// <summary>喷火间隔(帧),持续状态下每4帧一发</summary>
        public override int FireInterval => 4;
        /// <summary>对群塔打最近目标,不做Boss优先</summary>
        public override bool BossPriorityTargeting => false;

        /// <summary>开火期每帧耗电</summary>
        internal const float ConsumePerTick = 0.8f;
        /// <summary>基础伤害</summary>
        internal const int BaseDamage = 25;
        /// <summary>凝胶强化伤害</summary>
        internal const int FueledDamage = 37;
        /// <summary>烧掉一枚凝胶所需的开火帧数</summary>
        internal const int GelBurnTicks = 90;

        //TODO(岩浆输入接口):液体管网由并行组同期施工,本组不依赖其代码。
        //待 BaseFluidPipelineTP 落地后在此接入:检测相邻岩浆管道/储罐,按 255 单位岩浆
        //折算一段强化燃烧时间,优先级高于凝胶槽。对应 INDUSTRIAL-EXPANSION.md §7.2
        //火焰塔"second lava consumer"条目。

        /// <summary>凝胶燃料槽(只收 <see cref="ItemID.Gel"/>)</summary>
        internal Item FuelGel = new();
        /// <summary>本 tick 是否处于喷火状态</summary>
        internal bool Firing { get; private set; }
        internal float GlowIntensity;

        //---- 炮口余温:纯客户端表现,升温快冷却慢,停火后辉光渐熄+热气残余 ----
        /// <summary>炮口热度0~1:权威端跟喷火状态,客户端观测喷口附近的火焰弹</summary>
        internal float MuzzleHeat { get; private set; }
        private int heatScanTimer;
        private int heatSmokeTimer;

        private int fuelBurnTimer;
        private int retargetTimer;
        private int textIdleTime;
        //燃料消耗合批推送:脏标记+30帧节流,锚定包兜底
        private bool netDirty;
        private int netCooldown;

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
            FuelGel ??= new Item();
        }

        public override void Initialize() {
            FuelGel ??= new Item();
        }

        private bool HasFuel => FuelGel != null && !FuelGel.IsAir && FuelGel.stack > 0;

        /// <summary>模块生效的持续耗电(节能模块作用点)</summary>
        private float EffectiveConsumePerTick => ConsumePerTick * ModuleRack.TurretEnergyMult;

        #region 序列化:基类(MachineData→AttackPattern→模块架)→燃料槽
        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(FuelGel ?? new Item(), data, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FuelGel = ItemIO.Receive(reader, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_FuelGel"] = ItemIO.Save(FuelGel ?? new Item());
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FuelGel = CWRSaveData.LoadItemFromTag(tag, "_FuelGel", nameof(FlameTurretTP));
        }
        #endregion

        /// <summary>槽位被管道/交互改动后标脏,权威端节流推送</summary>
        internal void MarkFuelDirty() => netDirty = true;

        protected override void UpdateTurret() {
            //权威端节流推送燃料变化
            if (netCooldown > 0) {
                netCooldown--;
            }
            if (netDirty && netCooldown <= 0 && VaultUtils.isServer) {
                netDirty = false;
                netCooldown = 30;
                SendData();
            }
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            Firing = false;
            if (AttackPattern) {
                if (MachineData.UEvalue >= EffectiveConsumePerTick) {
                    UpdateFiring();
                }
                else if (textIdleTime <= 0 && CenterInWorld.FindClosestNPC(EffectiveRange, false, false) != null) {
                    //有敌无电才提示,避免刷屏
                    Defer(() => CombatText.NewText(HitBox, FlameTurret.Tint, FlameTurret.NoEnergyText.Value));
                    textIdleTime = 300;
                }
            }

            UpdateGlow();
            UpdateMuzzleHeat(Firing);
        }

        /// <summary>索敌与持续喷火:目标锁定期每帧耗电,每 FireInterval 帧一发火焰弹</summary>
        private void UpdateFiring() {
            //节流索敌:每10帧扫一次,期间沿用当前目标
            if (--retargetTimer <= 0) {
                retargetTimer = 10;
                TargetByNPC = AcquireTarget();
            }

            NPC target = TargetByNPC;
            if (target == null || !target.active || target.friendly
                || target.Distance(CenterInWorld) > EffectiveRange) {
                TargetByNPC = null;
                return;
            }

            Firing = true;
            MachineData.UEvalue -= EffectiveConsumePerTick;

            //凝胶强化:烧着才计数,烧满一档扣一枚
            if (HasFuel && ++fuelBurnTimer >= GelBurnTicks) {
                fuelBurnTimer = 0;
                FuelGel.stack--;
                if (FuelGel.stack <= 0) {
                    FuelGel.TurnToAir();
                }
                MarkFuelDirty();
            }

            if (++FireCoolden >= EffectiveFireInterval) {
                FireCoolden = 0;
                Fire(target);
            }
        }

        /// <summary>塔顶喷口</summary>
        internal Vector2 MuzzlePosition => PosInWorld + new Vector2(Width * 0.5f, 10f);

        /// <summary>
        /// 炮口余温包络:升温一拍到顶,冷却慢(约2秒读作金属余温);
        /// 余温期低频冒热气,读作刚喷完火的炮管
        /// </summary>
        private void UpdateMuzzleHeat(bool heating) {
            MuzzleHeat = heating
                ? Math.Min(1f, MuzzleHeat + 0.12f)
                : Math.Max(0f, MuzzleHeat - 0.008f);

            if (VaultUtils.isServer || MuzzleHeat < 0.4f || heating) {
                return;
            }
            //停火余温期:炮口偶发热气
            if (++heatSmokeTimer >= 22) {
                heatSmokeTimer = 0;
                Vector2 muzzle = MuzzlePosition;
                if (VaultUtils.IsPointOnScreen(muzzle - Main.screenPosition, 100)) {
                    float heat = MuzzleHeat;
                    Defer(() => PRTLoader.NewParticle<PRT_DefSmoke>(
                        muzzle + VaultUtils.RandVr(5f), new Vector2(0, -0.6f),
                        new Color(70, 58, 52) * (0.32f * heat), Main.rand.NextFloat(0.18f, 0.3f))
                        ?.Configure(Main.rand.Next(34, 50)));
                }
            }
        }

        /// <summary>生成一发火焰弹:普通 ModProjectile,权威端生成,owner 取默认(服务器即255)</summary>
        protected override void Fire(NPC target) {
            int damage = HasFuel ? FueledDamage : BaseDamage;
            Vector2 muzzle = MuzzlePosition;
            Vector2 dir = muzzle.To(target.Center).UnitVector();
            //锥形散布
            dir = dir.RotatedBy(Rand.NextFloat(-0.14f, 0.14f));
            //并行阶段弹幕生成延迟到主线程执行(串行阶段立即执行)
            DeferSpawnProjectile(this.FromObjectGetParent(), muzzle, dir * 16f,
                ModContent.ProjectileType<FlameTurretFire>(), damage, 1f);
        }

        /// <summary>喷口辉光包络,权威端与客户端同一推法</summary>
        private void UpdateGlow() {
            bool lit = AttackPattern && MachineData.UEvalue >= EffectiveConsumePerTick;
            GlowIntensity = lit
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);
        }

        /// <summary>权威 gate 下客户端的表现帧:辉光近似推进+观测喷口火焰弹还原余温</summary>
        protected override void UpdateTurretClient() {
            UpdateGlow();

            //客户端不知 Firing:每5帧扫一次喷口附近的火焰弹当在喷火
            bool firingObserved = false;
            if (++heatScanTimer >= 5) {
                heatScanTimer = 0;
                Vector2 muzzle = MuzzlePosition;
                int fireType = ModContent.ProjectileType<FlameTurretFire>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == fireType && proj.Center.DistanceSQ(muzzle) < 90f * 90f) {
                        firingObserved = true;
                        break;
                    }
                }
                if (firingObserved) {
                    //扫描间隔内保持在顶
                    MuzzleHeat = 1f;
                }
            }
            UpdateMuzzleHeat(firingObserved);
        }

        /// <summary>炮口余温辉光:热度驱动的暖光呼吸,画在充能条同层</summary>
        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();

            if (MuzzleHeat < 0.03f) {
                return;
            }
            var glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex == null) {
                return;
            }
            Vector2 muzzle = MuzzlePosition - Main.screenPosition;
            //高热偏白金,余温沉向橙红;A=0 加色
            Color warm = Color.Lerp(new Color(255, 96, 30), new Color(255, 196, 110), MuzzleHeat);
            warm.A = 0;
            float flicker = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + Position.X);
            float a = MuzzleHeat * flicker;
            spriteBatch.Draw(glowTex, muzzle, null, warm * (0.42f * a), 0f,
                glowTex.Size() * 0.5f, 0.55f + 0.15f * MuzzleHeat, SpriteEffects.None, 0f);
            spriteBatch.Draw(glowTex, muzzle, null, warm * (0.30f * a), 0f,
                glowTex.Size() * 0.5f, 0.28f, SpriteEffects.None, 0f);
        }

        /// <summary>模式翻转的本地反馈</summary>
        protected override void OnModeToggleEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, CenterInWorld);
            CombatText.NewText(HitBox, FlameTurret.Tint,
                AttackPattern ? FlameTurret.TurretOnText.Value : FlameTurret.TurretOffText.Value);
        }

        protected override void OnModeChangedByNet() {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(HitBox, FlameTurret.Tint,
                AttackPattern ? FlameTurret.TurretOnText.Value : FlameTurret.TurretOffText.Value);
        }

        /// <summary>
        /// 右键交互(仅交互客户端执行,SendData 即传播):手持凝胶装填,
        /// Shift 取出燃料,否则翻转开关
        /// </summary>
        public void HandleRightClick() {
            Item held = Main.LocalPlayer.GetItem();

            //手持凝胶:装填
            if (held != null && !held.IsAir && held.type == ItemID.Gel) {
                int moved;
                if (FuelGel == null || FuelGel.IsAir) {
                    FuelGel = held.Clone();
                    moved = held.stack;
                    held.TurnToAir();
                }
                else {
                    int space = FuelGel.maxStack - FuelGel.stack;
                    moved = Math.Min(space, held.stack);
                    FuelGel.stack += moved;
                    held.stack -= moved;
                    if (held.stack <= 0) {
                        held.TurnToAir();
                    }
                }
                if (moved > 0) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    CombatText.NewText(HitBox, FlameTurret.Tint, string.Format(FlameTurret.FuelLoadText.Value, moved));
                    SendData();
                }
                return;
            }

            //手持非凝胶的可堆叠物试图装填时给出拒绝提示(拿工具/武器点塔视为开关操作)
            if (Main.keyState.PressingShift()) {
                //Shift:取出全部燃料(直接入背包,MP下地面掉落会被队友截走)
                if (HasFuel) {
                    int count = FuelGel.stack;
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), FuelGel.Clone());
                    FuelGel.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Grab);
                    CombatText.NewText(HitBox, FlameTurret.Tint, string.Format(FlameTurret.FuelTakeText.Value, count));
                    SendData();
                }
                return;
            }

            //其余:翻转开关
            RightEvent();
        }

        public override void MachineKill() {
            base.MachineKill();
            //掉落槽内燃料(权威端)
            if (!VaultUtils.isClient && HasFuel) {
                DropItem(FuelGel.Clone());
            }
            FuelGel?.TurnToAir();
        }
    }
}
