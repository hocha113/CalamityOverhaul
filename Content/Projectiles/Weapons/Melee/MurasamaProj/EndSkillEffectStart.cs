using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class EndSkillEffectStart : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int CanDamageTime = 50;
        public const int CanDamageInNPCCountNum = 30;

        public Player player => Main.player[Projectile.owner];

        private ref float Time => ref Projectile.ai[0];

        /// <summary>
        /// 在场的符合条件的NPC是否小于<see cref="CanDamageInNPCCountNum"/>，如果是，返回<see langword="true"/>
        /// </summary>
        /// <returns></returns>
        public static bool CanDealDamageToNPCs() {
            int count = 0;
            foreach (NPC n in Main.npc) {
                if (!n.active || n.friendly) {
                    continue;
                }
                count++;
            }
            return count <= CanDamageInNPCCountNum;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }

        public override bool? CanDamage() => false;

        private Dictionary<int, Vector2> endPosDic;
        private Dictionary<int, float> endRotDic;
        private Dictionary<int, Vector2> endProjPosDic;
        private Dictionary<int, float> endProjRotDic;

        public Vector2 OrigPos {
            get => new Vector2(Projectile.ai[1], Projectile.ai[2]);
            set {
                Projectile.ai[1] = value.X;
                Projectile.ai[2] = value.Y;
            }
        }

        public override void AI() {
            foreach(Player p in Main.player) {
                if (p?.active == true) {
                    p.CWR().EndSkillEffectStartBool = true;
                }
            }

            int breakoutProjType = ModContent.ProjectileType<MurasamaBreakOut>();
            int heldProjType = ModContent.ProjectileType<MurasamaHeldProj>();
            int orbType = ModContent.ProjectileType<MurasamaEndSkillOrb>();

            bool projIsSafe(Projectile proj) {
                return proj.type != ModContent.ProjectileType<MurasamaBreakOut>() 
                    && proj.type != ModContent.ProjectileType<MurasamaHeldProj>() 
                    && proj.type != ModContent.ProjectileType<MurasamaEndSkillOrb>()
                    && !proj.hide;
            }

            Projectile.Center = Main.player[Projectile.owner].Center;
            if (Time == 0) {
                endPosDic = new Dictionary<int, Vector2>();
                endRotDic = new Dictionary<int, float>();
                endProjPosDic = new Dictionary<int, Vector2>();
                endProjRotDic = new Dictionary<int, float>();

                foreach (NPC npc in Main.npc) {
                    if (npc.Alives()) {
                        endPosDic.Add(npc.whoAmI, npc.position);
                        endRotDic.Add(npc.whoAmI, npc.rotation);
                    }
                }
                foreach (Projectile proj in Main.projectile) {
                    //弹幕对象必须活跃，同时不能是相关的刀体弹幕
                    if (proj.Alives() && projIsSafe(proj)) {
                        endProjPosDic.Add(proj.whoAmI, proj.position);
                        endProjRotDic.Add(proj.whoAmI, proj.rotation);
                    }
                }

                if ((Murasama.NameIsVergil(player) || Main.zenithWorld) && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.parent(), OrigPos, Vector2.Zero
                        , ModContent.ProjectileType<PowerSoundEgg>(), Projectile.damage, 0, Projectile.owner);
                }
            }

            foreach (int index in endPosDic.Keys) {
                if (index >= 0 && index < Main.maxNPCs) {
                    NPC overNpc = Main.npc[index];
                    if (overNpc == null) {
                        continue;
                    }
                    if (overNpc.active) {
                        overNpc.position = endPosDic[index];
                        overNpc.rotation = endRotDic[index];
                    }
                }
            }
            
            foreach (int index in endProjPosDic.Keys) {
                if (index >= 0 && index < Main.maxProjectiles) {
                    Projectile overProj = Main.projectile[index];
                    if (overProj == null) {
                        continue;
                    }
                    if (!projIsSafe(overProj)) {
                        continue;
                    }
                    if (overProj.active) {
                        overProj.position = endProjPosDic[index];
                        overProj.rotation = endProjRotDic[index];
                    }
                }
            }
            if (Time == CanDamageTime) {
                if (!CanDealDamageToNPCs() && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.parent(), OrigPos, Vector2.Zero
                        , ModContent.ProjectileType<EndSkillMakeDamage>(), Projectile.damage, 0, Projectile.owner);
                }
            }
            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (Murasama.NameIsVergil(player) || Main.zenithWorld) {
                SoundStyle[] sounds = new SoundStyle[] { CWRSound.V_YouSouDiad , CWRSound.V_ThisThePwero , CWRSound.V_You_Wo_Namges_Is_The_Pwero };
                SoundEngine.PlaySound(sounds[Main.rand.Next(sounds.Length)]);
                if (Main.rand.NextBool(13)) {
                    player.QuickSpawnItem(player.parent(), ModContent.ItemType<FoodStallChair>());
                }
            }
        }
    }
}
