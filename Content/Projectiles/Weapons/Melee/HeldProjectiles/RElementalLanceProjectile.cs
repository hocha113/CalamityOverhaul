using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RElementalLanceProjectile : BaseSpearProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<Items.Melee.ElementalLanceEcType>();

        public override string Texture => CWRConstant.Projectile_Melee + "ElementalLanceProjectile";

        public Player Owner => Main.player[Projectile.owner];

        public Item elementalLance => Owner.HeldItem;

        public override float InitialSpeed => 3f;

        public override float ReelbackSpeed => 1.1f;

        public override float ForwardSpeed => 0.6f;

        public override Action<Projectile> EffectBeforeReelback => delegate {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity, Projectile.velocity
                , ModContent.ProjectileType<SpatialSpear>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
        };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public int Time { get => (int)Projectile.localAI[1]; set => Projectile.localAI[1] = value; }
        private List<int> ElementalRayList = [];
        private int drawUIalp = 0;
        public override void AI() {
            if (Projectile.ai[1] == 0) {
                base.AI();
            }
            if (Projectile.ai[1] == 1) {
                if (Time == 0f) {
                    SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, Projectile.Center);
                }

                Projectile.velocity = Vector2.Zero;
                if (Owner == null) {
                    Projectile.Kill();
                    return;
                }
                if (elementalLance == null || elementalLance.type != ModContent.ItemType<Items.Melee.ElementalLanceEcType>()
                    && elementalLance.type != ModContent.ItemType<CalamityMod.Items.Weapons.Melee.ElementalLance>()) {
                    Projectile.Kill();
                    return;
                }//因为需要替换原模组的内容，所以这里放弃了直接访问类型来获取属性，作为补救，禁止其余物品发射该弹幕，即使这种情况不应该出现

                if (Projectile.IsOwnedByLocalPlayer()) {
                    float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
                    if (PlayerInput.Triggers.Current.MouseRight) Projectile.timeLeft = 2;
                    Owner.direction = Owner.Center.To(Main.MouseWorld).X > 0 ? 1 : -1;
                }

                if (Projectile.ai[2] == 0) {
                    Projectile.Center = Owner.Center;
                    Projectile.rotation += MathHelper.ToRadians(25);

                    drawUIalp += 5;
                    if (drawUIalp > 255) drawUIalp = 255;

                    if (Time % 15 == 0 && Projectile.IsOwnedByLocalPlayer() && Projectile.localAI[2] != 0) {
                        List<NPC> targets = EnemyHunting();
                        for (int i = 0; i < 4; i++) {
                            int targetWhoAmI = -1;
                            if (i < targets.Count) {
                                targetWhoAmI = targets[i].whoAmI;
                            }
                            Projectile.NewProjectile(
                                Projectile.parent(),
                                Projectile.Center,
                                Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * 5,
                                ModContent.ProjectileType<ElementalSpike>(),
                                Projectile.damage * 2,
                                5,
                                Projectile.owner,
                                i,
                                targetWhoAmI
                                );
                        }
                    }

                    if (Projectile.IsOwnedByLocalPlayer()) {
                        elementalLance.CWR().MeleeCharge += 8.333f;

                    }
                    if (Projectile.localAI[1] > 60) {
                        elementalLance.CWR().MeleeCharge = 500;
                        Projectile.ai[2] = 1;
                        Projectile.localAI[1] = 0;
                    }
                }
                if (Projectile.ai[2] == 1) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 toMous = Owner.Center.To(Main.MouseWorld).UnitVector();
                        Vector2 topos = toMous * 56 + Owner.Center;
                        Projectile.Center = Vector2.Lerp(topos, Projectile.Center, 0.01f);
                        Projectile.rotation = toMous.ToRotation();

                        elementalLance.CWR().MeleeCharge--;
                        if (Owner.ownedProjectileCounts[ModContent.ProjectileType<ElementalRay>()] < 5) {
                            int proj = Projectile.NewProjectile(
                                Projectile.parent(),
                                Projectile.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<ElementalRay>(),
                                Projectile.damage / 3,
                                0,
                                Projectile.owner
                                );
                            ElementalRayList.AddOrReplace(proj);

                            for (int i = 0; i < ElementalRayList.Count; i++) {
                                Projectile projectile = CWRUtils.GetProjectileInstance(ElementalRayList[i]);
                                if (projectile == null) {
                                    ElementalRayList[i] = -1;
                                    continue;
                                }
                                if (projectile.type != ModContent.ProjectileType<ElementalRay>()) {
                                    ElementalRayList[i] = -1;
                                }
                            }

                            CWRUtils.SweepLoadLists(ref ElementalRayList);
                        }

                        for (int i = 0; i < ElementalRayList.Count; i++) {
                            Projectile ray = CWRUtils.GetProjectileInstance(ElementalRayList[i]);
                            if (ray != null) {
                                ray.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 132;
                                ray.timeLeft = 2;
                                ray.ai[0] = i;
                                ray.localAI[1] = i;
                            }
                        }

                        if (elementalLance.CWR().MeleeCharge <= 0) {
                            Projectile.ai[2] = 0;
                            Projectile.localAI[1] = 0;
                            Projectile.localAI[2] = 1;
                            Projectile.netUpdate = true;
                            elementalLance.CWR().MeleeCharge = 0;
                            ElementalRayList = [];
                            SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, Projectile.Center);
                        }
                    }
                }
            }

            Time++;
        }

        public List<NPC> EnemyHunting() {
            List<NPC> closestNPCs = [];

            // 循环遍历所有NPC
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly) {
                    // 计算玩家和NPC之间的距离
                    float distance = Vector2.Distance(Owner.Center, npc.Center);
                    if (distance > 5000) continue;

                    // 将NPC插入到列表中，并保持列表按照距离升序排序
                    int index = closestNPCs.FindIndex(n => Vector2.Distance(Owner.Center, n.Center) > distance);
                    if (index >= 0) {
                        closestNPCs.Insert(index, npc);
                    }
                    else {
                        closestNPCs.Add(npc);
                    }

                    // 如果列表中的NPC数量超过5个，移除最远的NPC
                    if (closestNPCs.Count > 5) {
                        closestNPCs.RemoveAt(5);
                    }
                }
            }
            return closestNPCs;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture2D = ModContent.Request<Texture2D>(Texture).Value;

            if (Projectile.ai[1] == 0) {
                base.PreDraw(ref lightColor);
            }
            if (Projectile.ai[1] == 1) {
                Main.EntitySpriteDraw(
                    texture2D, CWRUtils.WDEpos(Projectile.Center), null, lightColor,
                    Projectile.rotation + MathHelper.PiOver2 + MathHelper.PiOver4, CWRUtils.GetOrig(texture2D),
                    Projectile.scale, SpriteEffects.None);
            }
            return false;
        }

        public override void PostDraw(Color lightColor) {
            DrawElementalChargeBar();
        }

        public void DrawElementalChargeBar() {
            if (Owner == null || Projectile.ai[1] != 1) return;
            Texture2D elementBar = CWRUtils.GetT2DValue(CWRConstant.UI + "ElementBar");
            Texture2D elementTop = CWRUtils.GetT2DValue(CWRConstant.UI + "ElementTop");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(Owner.Center + new Vector2(elementBar.Width / -2 * slp, 135));
            float alp = drawUIalp / 255f;
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((elementBar.Width - offsetwid * 2) * (elementalLance.CWR().MeleeCharge / 500f)), elementBar.Height);

            Main.EntitySpriteDraw(
                elementBar,
                drawPos + new Vector2(offsetwid, 0) * slp,
                backRec,
                Color.White * alp,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                elementTop,
                drawPos,
                null,
                Color.White * alp,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );
        }

        public override void ExtraBehavior() {
            if (Main.rand.NextBool(5)) {
                int num = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.RainbowTorch, Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB));
                Main.dust[num].noGravity = true;
            }
        }
    }
}
