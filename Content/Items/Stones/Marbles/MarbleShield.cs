using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石战盾，石卫护盾+举盾完美格挡反制</summary>
    internal class MarbleShield : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.accessory = true;
            Item.defense = 5;
            Item.value = Item.sellPrice(0, 0, 90, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            MarbleShieldPlayer mp = player.GetModPlayer<MarbleShieldPlayer>();
            mp.Equipped = true;
            mp.HideVisual = hideVisual;
            player.GetKnockback<MeleeDamageClass>() += 0.1f;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Accessory_Skills, "[KEY]"
                , CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.Accessory_Skills.DisplayName}]");
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 20)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleShieldPlayer : ModPlayer
    {
        public bool Equipped;
        public bool HideVisual;
        public int RechargeTimer;
        public int BlockTimer;
        public int BlockCooldown;
        //纯视觉，本地演算不同步
        public float OrbitAngle;
        public float BlockLerp;

        public const int RechargeTime = 720;
        public const int BlockWindow = 22;
        public const int BlockCooldownTime = 300;
        public const int ShardCount = 3;
        private const float OrbitRadius = 44f;
        private const float BlockRange = 160f;

        public bool BarrierReady => RechargeTimer <= 0;
        public bool Blocking => BlockTimer > 0;
        /// <summary>充能 0~1，1=就绪</summary>
        public float ChargeProgress => 1f - RechargeTimer / (float)RechargeTime;

        public override void ResetEffects() {
            if (RechargeTimer > 0) {
                RechargeTimer--;
                //充能就绪边沿音+闪光
                if (RechargeTimer == 0 && Equipped && Player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.35f, Volume = 0.9f }, Player.Center);
                    Vector2 pos = ShardWorldPosition(ShardCount - 1);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero
                        , GraniteMarbleVFX.MarbleGold, 0).Configure(0.06f, 0.5f, 14);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(pos, Main.rand.NextVector2Circular(2.2f, 2.2f)
                            , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.6f))
                            .Configure(Main.rand.Next(14, 22), 0.08f);
                    }
                }
            }
            if (BlockTimer > 0) {
                BlockTimer--;
            }
            if (BlockCooldown > 0) {
                BlockCooldown--;
            }
            Equipped = false;
            HideVisual = false;
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (!Equipped || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (CWRKeySystem.Accessory_Skills.JustPressed && BlockCooldown <= 0) {
                BlockTimer = BlockWindow;
                BlockCooldown = BlockCooldownTime;
                //举盾双层音
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.3f, Volume = 0.9f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.55f, Volume = 0.4f }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0).Configure(0.1f, 0.8f, 16);
            }
        }

        public override void PostUpdate() {
            if (!Equipped) {
                BlockLerp = 0f;
                return;
            }

            //举盾收拢，松开散回
            BlockLerp = MathHelper.Lerp(BlockLerp, Blocking ? 1f : 0f, Blocking ? 0.3f : 0.12f);
            //就绪稍快，充能滞缓，举盾停转
            OrbitAngle += (BarrierReady ? 0.05f : 0.032f) * (1f - BlockLerp);
            if (OrbitAngle > MathHelper.TwoPi) {
                OrbitAngle -= MathHelper.TwoPi;
            }

            if (Blocking) {
                ShatterNearbyProjectiles();
            }

            if (!VaultUtils.isServer && !HideVisual) {
                Lighting.AddLight(Player.MountedCenter
                    , GraniteMarbleVFX.MarbleGold.ToVector3() * (BarrierReady ? 0.45f : 0.22f));
                //第三块石尘凝聚
                if (!BarrierReady && Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_Smoke>(ShardWorldPosition(ShardCount - 1)
                        , Main.rand.NextVector2Circular(0.6f, 0.6f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.2f, 0.35f)).Configure(18, 0.5f, 0.04f);
                }
            }
        }

        /// <summary>slot∈[0,3) 环绕↔盾面按 BlockLerp 插值</summary>
        public Vector2 ShardWorldPosition(int slot) {
            float angle = OrbitAngle + MathHelper.TwoPi / ShardCount * slot;
            Vector2 orbit = new Vector2(MathF.Cos(angle) * OrbitRadius, MathF.Sin(angle) * OrbitRadius * 0.34f - 4f);
            Vector2 wall = new Vector2(Player.direction * (30f - Math.Abs(slot - 1) * 4f), (slot - 1) * 17f);
            return Player.MountedCenter + Vector2.Lerp(orbit, wall, BlockLerp);
        }

        /// <summary>纵深 0~1，举盾收敛为 1</summary>
        public float ShardDepth(int slot) {
            float angle = OrbitAngle + MathHelper.TwoPi / ShardCount * slot;
            float depth = (MathF.Sin(angle) + 1f) * 0.5f;
            return MathHelper.Lerp(depth, 1f, BlockLerp);
        }

        //完美格挡窗，免伤+近战强化
        public override bool FreeDodge(Player.HurtInfo info) {
            if (Equipped && Blocking) {
                BurstGuard(40, true);
                Player.AddBuff(ModContent.BuffType<MarbleRiposteBuff>(), MarbleRiposteBuff.Duration);
                return true;
            }
            return false;
        }

        //石卫，就绪吸一次伤，第三块碎掉进充能
        public override bool ConsumableDodge(Player.HurtInfo info) {
            if (Equipped && BarrierReady) {
                RechargeTimer = RechargeTime;
                BurstGuard(24, false);
                return true;
            }
            return false;
        }

        private void BurstGuard(int damage, bool strong) {
            if (!VaultUtils.isServer) {
                //格挡/吸收分层音
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = strong ? -0.1f : 0.2f, Volume = 1.1f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = strong ? 0.1f : -0.2f, Volume = 0.8f }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0).Configure(0.15f, strong ? 1.4f : 0.9f, 24);

                //吸收炸第三块，格挡三块齐振
                if (strong) {
                    for (int slot = 0; slot < ShardCount; slot++) {
                        Vector2 pos = ShardWorldPosition(slot);
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_MarbleChip>(pos
                                , Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * 2f
                                , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                                .Configure(Main.rand.Next(18, 30));
                        }
                    }
                }
                else {
                    Vector2 breakPos = ShardWorldPosition(ShardCount - 1);
                    for (int i = 0; i < 9; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(breakPos
                            , Main.rand.NextVector2Circular(5f, 4f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                            , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.45f, 0.8f))
                            .Configure(Main.rand.Next(22, 34));
                    }
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(breakPos, Main.rand.NextVector2Circular(2.5f, 2.5f)
                            , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.55f)).Configure(24, 0.7f, 0.05f);
                    }
                }
                for (int i = 0; i < (strong ? 12 : 6); i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(24, 0.7f, 0.05f);
                }

                if (strong && CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Player.Center
                        , Main.rand.NextVector2Unit(), 4f, 5f, 9, 700f, FullName));
                }
            }

            if (Player.whoAmI == Main.myPlayer) {
                int count = strong ? 8 : 5;
                for (int i = 0; i < count; i++) {
                    Vector2 v = (MathHelper.TwoPi / count * i + Main.rand.NextFloat(0.3f)).ToRotationVector2() * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, v
                        , ModContent.ProjectileType<MarbleShard>(), damage, 4f, Player.whoAmI);
                }
            }
        }

        //盾反仅 owner 本地，Kill 近旁敌弹+生成反击石刃，不改敌弹阵营/owner
        private void ShatterNearbyProjectiles() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            bool any = false;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.friendly || proj.damage <= 0) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, proj.Center) > BlockRange) {
                    continue;
                }

                //反击伤=敌弹×1.5封顶60
                int counterDamage = Math.Clamp((int)(proj.damage * 1.5f), 15, 60);
                Vector2 dir = proj.velocity.LengthSquared() > 0.01f
                    ? -proj.velocity.SafeNormalize(Vector2.UnitX)
                    : Player.Center.To(proj.Center).SafeNormalize(Vector2.UnitX * Player.direction);
                Projectile.NewProjectile(Player.FromObjectGetParent(), proj.Center, dir * 11f
                    , ModContent.ProjectileType<MarbleShard>(), counterDamage, 5f, Player.whoAmI);

                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, Vector2.Zero
                        , GraniteMarbleVFX.MarbleGold, 0.6f).Configure(GraniteMarbleVFX.MarbleGold, 14, 0.2f, 0.7f);
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(proj.Center
                            , dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 5f)
                            , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.65f))
                            .Configure(Main.rand.Next(16, 26));
                    }
                }

                proj.Kill();
                any = true;
            }

            if (any && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.45f, Volume = 0.75f }, Player.Center);
            }
        }
    }

    /// <summary>三块石片环绕层，就绪全亮、充能重组第三块、举盾收拢+金环</summary>
    internal class MarbleShieldLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f) {
                return false;
            }
            Player player = drawInfo.drawPlayer;
            if (!player.active || player.dead || player.ghost) {
                return false;
            }
            MarbleShieldPlayer mp = player.GetModPlayer<MarbleShieldPlayer>();
            return mp.Equipped && !mp.HideVisual;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player player = drawInfo.drawPlayer;
            MarbleShieldPlayer mp = player.GetModPlayer<MarbleShieldPlayer>();

            Texture2D sliver = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;

            float time = (float)Main.timeForVisualEffects * 0.05f;
            //A=0 加算发光
            Color gold = GraniteMarbleVFX.MarbleGold with { A = 0 };
            Color core = GraniteMarbleVFX.MarbleCore with { A = 0 };
            Color dust = GraniteMarbleVFX.MarbleDust;

            //举盾金环随窗衰减
            if (mp.Blocking && mp.BlockLerp > 0.05f) {
                float window = mp.BlockTimer / (float)MarbleShieldPlayer.BlockWindow;
                Vector2 ringPos = player.MountedCenter + new Vector2(player.direction * 26f, player.gfxOffY) - Main.screenPosition;
                float ringScale = 92f / ring.Width;
                drawInfo.DrawDataCache.Add(new DrawData(ring, ringPos, null, gold * (0.75f * window * mp.BlockLerp)
                    , time * 0.4f, ring.Size() / 2f, new Vector2(ringScale), SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(ring, ringPos, null, core * (0.4f * window * mp.BlockLerp)
                    , -time * 0.3f, ring.Size() / 2f, new Vector2(ringScale * 0.8f), SpriteEffects.None));
            }

            float pulse = 0.85f + MathF.Sin(time * 2.2f) * 0.15f;

            for (int slot = 0; slot < MarbleShieldPlayer.ShardCount; slot++) {
                //第三块按进度重组
                float rebuild = 1f;
                if (!mp.BarrierReady && slot == MarbleShieldPlayer.ShardCount - 1) {
                    rebuild = mp.ChargeProgress;
                    if (rebuild < 0.12f) {
                        continue;
                    }
                }

                Vector2 pos = mp.ShardWorldPosition(slot) + Vector2.UnitY * player.gfxOffY - Main.screenPosition;
                float depth = mp.ShardDepth(slot);
                float scale = (0.72f + depth * 0.42f) * (0.45f + rebuild * 0.55f);
                float alpha = (0.55f + depth * 0.45f) * (0.3f + rebuild * 0.7f);
                //就绪/举盾全亮，充能转暗
                float lit = mp.BarrierReady || mp.Blocking ? 1f : 0.6f;
                //环绕翻滚，举盾立正
                float tumble = MathHelper.WrapAngle(time * 1.4f + slot * 2.4f);
                float rotation = MathHelper.Lerp(tumble, 0f, mp.BlockLerp);

                drawInfo.DrawDataCache.Add(new DrawData(glow, pos, null, gold * (0.5f * alpha * lit * pulse)
                    , 0f, glow.Size() / 2f, new Vector2(0.5f * scale), SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(sliver, pos, null, dust * (0.85f * alpha)
                    , rotation, sliver.Size() / 2f, new Vector2(0.4f, 0.82f) * scale, SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(sliver, pos, null, gold * (0.9f * alpha * lit)
                    , rotation + 0.9f, sliver.Size() / 2f, new Vector2(0.26f, 0.55f) * scale, SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(sliver, pos, null, core * (0.8f * alpha * lit * pulse)
                    , rotation, sliver.Size() / 2f, new Vector2(0.18f, 0.6f) * scale, SpriteEffects.None));
            }
        }
    }

    /// <summary>完美格挡近战强化</summary>
    internal class MarbleRiposteBuff : ModBuff
    {
        /// <summary>持续帧，5秒</summary>
        public const int Duration = 300;
        /// <summary>近战伤加成</summary>
        public const float MeleeBonus = 0.1f;

        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleShield";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetDamage<MeleeDamageClass>() += MeleeBonus;
        }
    }
}
