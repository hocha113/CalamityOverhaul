using CalamityMod;
using CalamityMod.Events;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public static class CWRUtils
    {
        #region System
        /// <summary>
        /// 一个额外的跳字方法，向游戏内打印对象的ToString内容
        /// </summary>
        /// <param name="obj"></param>
        public static void Domp(this object obj, Color color = default) {
            if (color == default) {
                color = Color.White;
            }
            if (obj == null) {
                VaultUtils.Text("ERROR Is Null", Color.Red);
                return;
            }
            VaultUtils.Text(obj.ToString(), color);
        }

        /// <summary>
        /// 一个额外的跳字方法，向控制台面板打印对象的ToString内容，并自带换行
        /// </summary>
        /// <param name="obj"></param>
        public static void DompInConsole(this object obj, bool outputLogger = true) {
            if (obj == null) {
                Console.WriteLine("ERROR Is Null");
                return;
            }
            string value = obj.ToString();
            Console.WriteLine(value);
            if (outputLogger) {
                CWRMod.Instance.Logger.Info(value);
            }
        }

        ///// <summary>
        ///// 将 Item 数组的信息写入指定路径的文件中
        ///// </summary>
        ///// <param name="items">要导出的 Item 数组</param>
        ///// <param name="path">写入文件的路径，默认为 "D:\\Mod_Resource\\input.cs"</param>
        //public static void ExportItemTypesToFile(Item[] items, string path = "D:\\Mod_Resource\\input.cs") {
        //    try {
        //        int columnIndex = 0;
        //        using StreamWriter sw = new(path);
        //        sw.Write("string[] fullItems = new string[] {");
        //        foreach (Item item in items) {
        //            columnIndex++;
        //            // 根据是否有 ModItem 决定写入的内容
        //            string itemInfo = item.ModItem == null ? $"\"{item.type}\"" : $"\"{item.ModItem.FullName}\"";
        //            sw.Write(itemInfo);
        //            sw.Write(", ");
        //            // 每行最多写入9个元素，然后换行
        //            if (columnIndex >= 9) {
        //                sw.WriteLine();
        //                columnIndex = 0;
        //            }
        //        }
        //        sw.Write("};");
        //    } catch (UnauthorizedAccessException) {
        //        CWRMod.Instance.Logger.Info($"UnauthorizedAccessException: 无法访问文件路径 '{path}'. 权限不足");
        //    } catch (DirectoryNotFoundException) {
        //        CWRMod.Instance.Logger.Info($"DirectoryNotFoundException: 文件路径 '{path}' 中的目录不存在");
        //    } catch (PathTooLongException) {
        //        CWRMod.Instance.Logger.Info($"PathTooLongException: 文件路径 '{path}' 太长");
        //    } catch (IOException) {
        //        CWRMod.Instance.Logger.Info($"IOException: 无法打开文件 '{path}' 进行写入");
        //    } catch (Exception e) {
        //        CWRMod.Instance.Logger.Info($"An error occurred: {e.Message}");
        //    }
        //}
        #endregion

        #region AIUtils

        #region 工具部分

        public const float atoR = MathHelper.Pi / 180;

        public static float AtoR(this float num) => num * atoR;

        public static float RtoA(this float num) => num / atoR;

        public static void SetArrowRot(int proj) => Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        public static void SetArrowRot(this Projectile proj) => proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;

        /// <summary>
        /// 如果对象是一个蠕虫体节，那么按机会分母的倒数返回布尔值，如果输入5，那么会有4/5的概率返回<see langword="true"/>
        /// </summary>
        /// <param name="targetNPCType"></param>
        /// <param name="randomCount"></param>
        /// <returns></returns>
        public static bool FromWormBodysRandomSet(int targetNPCType, int randomCount) {
            return CWRLoad.WormBodys.Contains(targetNPCType) && !Main.rand.NextBool(randomCount);
        }
        /// <summary>
        /// 如果对象是一个蠕虫体节，那么按机会分母的倒数返回布尔值，如果输入5，那么会有4/5的概率返回<see langword="true"/>
        /// </summary>
        /// <param name="targetNPCType"></param>
        /// <param name="randomCount"></param>
        /// <returns></returns>
        public static bool FromWormBodysRandomSet(this NPC npc, int randomCount) => FromWormBodysRandomSet(npc.type, randomCount);

        /// <summary>
        /// 这个NPC是否属于一个蠕虫体节
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        public static bool IsWormBody(this NPC npc) => CWRLoad.WormBodys.Contains(npc.type);

        /// <summary>
        /// 根据索引返回在player域中的player实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static Player GetPlayerInstance(int playerIndex) {
            if (playerIndex.ValidateIndex(Main.player)) {
                Player player = Main.player[playerIndex];

                return player.Alives() ? player : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 根据索引返回在npc域中的npc实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static NPC GetNPCInstance(int npcIndex) {
            if (npcIndex.ValidateIndex(Main.npc)) {
                NPC npc = Main.npc[npcIndex];

                return npc.Alives() ? npc : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 根据索引返回在projectile域中的Projectile实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static Projectile GetProjectileInstance(int projectileIndex) {
            if (projectileIndex.ValidateIndex(Main.projectile)) {
                Projectile proj = Main.projectile[projectileIndex];
                return proj.Alives() ? proj : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 获取鞭类弹幕的路径点集
        /// </summary>
        public static List<Vector2> GetWhipControlPoints(this Projectile projectile) {
            List<Vector2> list = [];
            Projectile.FillWhipControlPoints(projectile, list);
            return list;
        }

        #endregion

        #region 行为部分

        public static void DigByTile(this Projectile projectile, SoundStyle soundStyle = default) {
            Collision.HitTiles(projectile.position, projectile.velocity, projectile.width, projectile.height);
            SoundEngine.PlaySound(soundStyle == default ? SoundID.Dig : soundStyle, projectile.position);
        }

        public static void SpawnTrailDust(this Projectile Projectile, int type, float velocityMult
            , int Alpha = 0, Color newColor = default, float Scale = 1f, bool noGravity = true) {
            if (VaultUtils.isServer) {
                return;
            }

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width
                , Projectile.height, type, Alpha: Alpha, newColor: newColor, Scale: Scale);
            dust.noGravity = noGravity;
            dust.velocity = -Projectile.velocity * velocityMult;
        }

        public static void EntityToRot(this NPC entity, float toRot, float rotSpeed) => entity.rotation = ToRot(entity.rotation, toRot, rotSpeed);

        public static float ToRot(float setRot, float toRot, float rotSpeed) {
            setRot = MathHelper.WrapAngle(setRot);
            float diff = MathHelper.WrapAngle(toRot - setRot);
            return setRot + MathHelper.Clamp(diff, -rotSpeed, rotSpeed);
        }

        /// <summary>
        /// 处理实体的旋转行为
        /// </summary>
        public static void EntityToRot(this Projectile entity, float targetRot, float rotSpeed) {
            entity.rotation = MathHelper.WrapAngle(entity.rotation);
            float diff = MathHelper.WrapAngle(targetRot - entity.rotation);
            entity.rotation += diff * rotSpeed;
        }

        /// <summary>
        /// 在必要的时候使用这个发送NPC基本数据
        /// </summary>
        /// <param name="npc"></param>
        public static void SendNPCbasicData(this NPC npc, int player = -1) {
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.NPCbasicData);
            modPacket.Write((byte)npc.whoAmI);
            modPacket.WriteVector2(npc.position);
            modPacket.Write(npc.rotation);
            modPacket.Send(player);
        }

        #endregion

        #endregion

        #region GameUtils
        /// <summary>
        /// 是否处于入侵期间
        /// </summary>
        public static bool Invasion => Main.invasionType > 0 || Main.pumpkinMoon
                || Main.snowMoon || DD2Event.Ongoing || AcidRainEvent.AcidRainEventIsOngoing;

        public static bool IsTool(this Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        public static void GiveMeleeType(this Item item, bool isGiveTrueMelee = false) => item.DamageType = GiveMeleeType(isGiveTrueMelee);

        public static DamageClass GiveMeleeType(bool isGiveTrueMelee = false) => isGiveTrueMelee ? ModContent.GetInstance<TrueMeleeDamageClass>() : DamageClass.Melee;

        /// <summary>
        /// 目标弹药是否应该判定为一个木箭
        /// </summary>
        /// <param name="player"></param>
        /// <param name="ammoType"></param>
        /// <returns></returns>
        public static bool IsWoodenAmmo(this Player player, int ammoType) {
            if (player.hasMoltenQuiver && ammoType == ProjectileID.FireArrow) {
                return true;
            }
            if (ammoType == ProjectileID.WoodenArrowFriendly) {
                return true;
            }

            return false;
        }

        public static bool BladeArmEnchant(this Player player) => player.Calamity().bladeArmEnchant;

        public static bool AdrenalineMode(this Player player) => player.Calamity().adrenalineModeActive;

        public static void SetItemLegendContentTops(ref List<TooltipLine> tooltips, string itemKey) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[legend]") && x.Mod == "Terraria");
            if (legendtops != null) {
                KeyboardState state = Keyboard.GetState();
                if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                    legendtops.Text = Language.GetTextValue($"Mods.CalamityOverhaul.Items.{itemKey}.Legend");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
                else {
                    legendtops.Text = CWRLocText.GetTextValue("Item_LegendOnMouseLang");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.Gold, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
            }
        }

        public static void ModifyLegendWeaponDamageFunc(Item item, int GetOnDamage, int GetStartDamage, ref StatModifier damage) {
            float oldMultiplicative = damage.Multiplicative;
            damage *= GetOnDamage / (float)GetStartDamage;
            damage /= oldMultiplicative;
            //首先，因为SD的运行优先级并不可靠，有的模组的修改在SD之后运行，比如炼狱模式，这个基础伤害缩放保证一些情况不会发生
            damage *= GetStartDamage / (float)item.damage;
            damage *= item.GetPrefixState().damageMult;
        }

        public static void ModifyLegendWeaponKnockbackFunc(Item item, float GetOnKnockback, float GetStartKnockback, ref StatModifier Knockback) {
            Knockback *= GetOnKnockback / (float)GetStartKnockback;
            //首先，因为SD的运行优先级并不可靠，有的模组的修改在SD之后运行，比如炼狱模式，这个基础击退缩放保证一些情况不会发生
            Knockback *= GetStartKnockback / item.knockBack;
            Knockback *= item.GetPrefixState().knockbackMult;
        }

        public static NPC FindNPCFromeType(int type) {
            NPC npc = null;
            foreach (var n in Main.npc) {
                if (!n.active) {
                    continue;
                }
                if (n.type == type) {
                    npc = n;
                }
            }
            return npc;
        }

        public static Recipe AddBlockingSynthesisEvent(this Recipe recipe) =>
             recipe.AddConsumeIngredientCallback((Recipe recipe, int type, ref int amount, bool isDecrafting) => { amount = 0; })
            .AddOnCraftCallback(CWRRecipes.SpawnAction);

        /// <summary>
        /// 用于将一个武器设置为手持刀剑类，这个函数若要正确设置物品的近战属性，需要让其在初始化函数中最后调用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="dontStopOrigShoot"></param>
        public static void SetKnifeHeld<T>(this Item item, bool dontStopOrigShoot = false) where T : ModProjectile {
            if (item.shoot == ProjectileID.None || !item.noUseGraphic
                || item.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                || item.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                CWRItems.ItemMeleePrefixDic[item.type] = true;
            }
            item.noMelee = true;
            item.noUseGraphic = true;
            item.CWR().IsShootCountCorlUse = true;
            item.CWR().IsHeldSwing = true;
            item.CWR().IsHeldSwingDontStopOrigShoot = dontStopOrigShoot;
            item.CWR().SetHeldSwingOrigShootID = item.shoot;//提前存储一下射弹值
            item.CWR().WeaponInSetKnifeHeld = true;
            item.shoot = ModContent.ProjectileType<T>();
            if (item.shootSpeed <= 0) {
                //不能让速度模长为0，这会让向量失去方向的性质，从而影响一些刀剑的方向判定
                item.shootSpeed = 0.0001f;
            }
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为手持对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetHeldProj<T>(this Item item) where T : ModProjectile {
            item.noUseGraphic = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<T>();
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为手持对象
        /// </summary>
        public static void SetHeldProj(this Item item, int id) {
            item.noUseGraphic = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = id;
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为填装枪类实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetCartridgeGun<T>(this Item item, int ammoCapacity = 1) where T : ModProjectile {
            item.SetHeldProj<T>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = ammoCapacity;
        }

        /// <summary>
        /// 复制一个物品的属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetItemCopySD<T>(this Item item) where T : ModItem => item.CloneDefaults(ModContent.ItemType<T>());

        /// <summary>
        /// 弹药是否应该被消耗，先行判断武器自带的消耗抵消比率，再在该基础上计算玩家的额外消耗抵消比率，比如弹药箱加成或者药水
        /// </summary>
        /// <param name="player"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public static bool CanUseAmmoInWeaponShoot(this Player player, Item weapon) {
            bool result = true;
            if (weapon.type != ItemID.None) {
                Item[] magazineContents = weapon.CWR().MagazineContents;
                if (magazineContents.Length <= 0) {
                    return true;
                }
                result = ItemLoader.CanConsumeAmmo(weapon, magazineContents[0], player);
                if (player.IsRangedAmmoFreeThisShot(magazineContents[0])) {
                    result = false;
                }
            }
            return result;
        }

        /// <summary>
        /// 赋予玩家无敌状态，这个函数与<see cref="Player.SetImmuneTimeForAllTypes(int)"/>类似
        /// </summary>
        /// <param name="player">要赋予无敌状态的玩家</param>
        /// <param name="blink">是否允许玩家在无敌状态下闪烁默认为 false</param>
        public static void GivePlayerImmuneState(this Player player, int time, bool blink = false) {
            player.immuneNoBlink = !blink;
            player.immune = true;
            player.immuneTime = time;
            for (int k = 0; k < player.hurtCooldowns.Length; k++) {
                player.hurtCooldowns[k] = player.immuneTime;
            }
        }

        /// <summary>
        /// 将热键绑定的提示信息添加到 TooltipLine 列表中
        /// </summary>
        /// <param name="tooltips">要添加提示信息的 TooltipLine 列表</param>
        /// <param name="mhk">Mod 热键绑定</param>
        /// <param name="keyName">替换的关键字，默认为 "[KEY]"</param>
        /// <param name="modName">Mod 的名称，默认为 "Terraria"</param>
        public static void SetHotkey(this List<TooltipLine> tooltips, ModKeybind mhk, string keyName = "[KEY]", string modName = "") {
            if (Main.dedServ || mhk is null) {
                return;
            }

            string finalKey = mhk.TooltipHotkeyString();
            tooltips.ReplaceTooltip(keyName, finalKey, modName);
        }

        /// <summary>
        /// 替换 TooltipLine 列表中指定关键字的提示信息
        /// </summary>
        /// <param name="tooltips">要进行替换的 TooltipLine 列表</param>
        /// <param name="targetKeyStr">要替换的关键字</param>
        /// <param name="contentStr">替换后的内容</param>
        /// <param name="modName">Mod 的名称，默认为 "Terraria"</param>
        public static void ReplaceTooltip(this List<TooltipLine> tooltips, string targetKeyStr, string contentStr, string modName = "") {
            foreach (var line in tooltips) {
                if (modName != "" && line.Mod != modName) {
                    continue;
                }
                if (!line.Text.Contains(targetKeyStr)) {
                    continue;
                }
                line.Text = line.Text.Replace(targetKeyStr, contentStr);
            }
        }

        public static Color[] GetColorDate(Texture2D tex) {
            Color[] colors = new Color[tex.Width * tex.Height];
            tex.GetData(colors);
            List<Color> nonTransparentColors = [];
            foreach (Color color in colors) {
                if ((color.A > 0 || color.R > 0 || color.G > 0 || color.B > 0) && color != Color.White && color != Color.Black) {
                    nonTransparentColors.Add(color);
                }
            }
            return [.. nonTransparentColors];
        }

        /// <summary>
        /// 快速修改一个物品的简介文本，从<see cref="CWRLocText"/>中拉取资源
        /// </summary>
        public static void OnModifyTooltips(Mod mod, List<TooltipLine> tooltips, LocalizedText value) {
            List<TooltipLine> newTooltips = new(tooltips);
            List<TooltipLine> overTooltips = [];
            List<TooltipLine> prefixTooltips = [];
            foreach (TooltipLine line in tooltips.ToList()) {//复制 tooltips 集合，以便在遍历时修改
                for (int i = 0; i < 9; i++) {
                    if (line.Name == "Tooltip" + i) {
                        line.Hide();
                    }
                }
                if (line.Name == "CalamityDonor" || line.Name == "CalamityDev") {
                    overTooltips.Add(line.Clone());
                    line.Hide();
                }
                if (line.Name.Contains("Prefix")) {
                    prefixTooltips.Add(line.Clone());
                    line.Hide();
                }
            }

            TooltipLine newLine = new(mod, "CWRText", value.Value);
            newTooltips.Add(newLine);
            newTooltips.AddRange(overTooltips);
            tooltips.Clear(); // 清空原 tooltips 集合
            tooltips.AddRange(newTooltips); // 添加修改后的 newTooltips 集合
            tooltips.AddRange(prefixTooltips);
        }

        public static TooltipLine Clone(this TooltipLine tooltipLine) {
            Mod mod = CWRMod.Instance;
            foreach (Mod mod1 in ModLoader.Mods) {
                if (mod1.Name == tooltipLine.Mod) {
                    mod = mod1;
                }
            }
            TooltipLine line = new TooltipLine(mod, tooltipLine.Name, tooltipLine.Text) {
                OverrideColor = tooltipLine.OverrideColor,
                IsModifier = tooltipLine.IsModifier,
                IsModifierBad = tooltipLine.IsModifierBad
            };
            return line;
        }

        public static CWRNpc CWR(this NPC npc) {
            return npc.GetGlobalNPC<CWRNpc>();
        }

        public static CWRPlayer CWR(this Player player) {
            return player.GetModPlayer<CWRPlayer>();
        }

        public static CWRItems CWR(this Item item) {
            if (item.type == ItemID.None) {
                string message = "ERROR: An Empty Transfer Occurred! The Value of Item.type is Zero!";
                VaultUtils.Text(message, Color.Red);
                CWRMod.Instance.Logger.Error(message);
                //throw new InvalidOperationException(message); // 明确终止执行，抛出异常              
                return null;
            }
            return item.GetGlobalItem<CWRItems>();
        }

        public static CWRProjectile CWR(this Projectile projectile) {
            return projectile.GetGlobalProjectile<CWRProjectile>();
        }

        public static void Initialize(this Item item) {
            if (item.CWR().ai == null) {
                item.CWR().ai = [0, 0, 0];
            }
        }

        #endregion

        #region MathUtils

        public static Vector2 randVr(int min, int max) {
            return Main.rand.NextVector2Unit() * Main.rand.Next(min, max);
        }

        public static Vector2 randVr(int max) {
            return Main.rand.NextVector2Unit() * Main.rand.Next(0, max);
        }

        public static Vector2 randVr(float min, float max) {
            return Main.rand.NextVector2Unit() * Main.rand.NextFloat(min, max);
        }

        public static Vector2 randVr(float max) {
            return Main.rand.NextVector2Unit() * Main.rand.NextFloat(0, max);
        }

        public static float GetCorrectRadian(float minusRadian) {
            return minusRadian < 0 ? (MathHelper.TwoPi + minusRadian) / MathHelper.TwoPi : minusRadian / MathHelper.TwoPi;
        }

        /// <summary>
        /// 获取一个随机方向的向量
        /// </summary>
        /// <param name="startAngle">开始角度,输入角度单位的值</param>
        /// <param name="targetAngle">目标角度,输入角度单位的值</param>
        /// <param name="ModeLength">返回的向量的长度</param>
        /// <returns></returns>
        public static Vector2 GetRandomVevtor(float startAngle, float targetAngle, float ModeLength) {
            float angularSeparation = targetAngle - startAngle;
            float randomPosx = ((angularSeparation * Main.rand.NextFloat()) + startAngle) * (MathHelper.Pi / 180);
            float cosValue = MathF.Cos(randomPosx);
            float sinValue = MathF.Sin(randomPosx);

            return new Vector2(cosValue, sinValue) * ModeLength;
        }

        /// <summary>
        /// 检测索引的合法性
        /// </summary>
        /// <returns>合法将返回 <see cref="true"/></returns>
        public static bool ValidateIndex(this int index, Array array) {
            return index >= 0 && index < array.Length;
        }

        /// <summary>
        /// 委托，用于定义曲线的缓动函数
        /// </summary>
        /// <param name="progress">进度，范围在0到1之间。</param>
        /// <param name="polynomialDegree">如果缓动模式是多项式，则此为多项式的阶数</param>
        /// <returns>给定进度下的曲线值。</returns>
        public delegate float CurveEasingFunction(float progress, int polynomialDegree);

        /// <summary>
        /// 表示分段函数的一部分
        /// </summary>
        public struct AnimationCurvePart
        {
            /// <summary>
            /// 用于该段的缓动函数类型
            /// </summary>
            public CurveEasingFunction CurveEasingFunction { get; }

            /// <summary>
            /// 段在动画中的起始位置
            /// </summary>
            public float StartX { get; internal set; }

            /// <summary>
            /// 段的起始高度
            /// </summary>
            public float StartHeight { get; }

            /// <summary>
            /// 段内的高度变化量设为0时段为平直线通常在段末应用，但sinebump缓动类型在曲线顶点应用
            /// </summary>
            public float HeightShift { get; }

            /// <summary>
            /// 如果选择的缓动模式是多项式，则此为多项式的阶数
            /// </summary>
            public int PolynomialDegree { get; }

            /// <summary>
            /// 在考虑高度变化后的段结束高度
            /// </summary>
            public float EndHeight => StartHeight + HeightShift;

            public struct StartData
            {
                public float startX;
                public float startHeight;
                public float heightShift;
                public int degree = 1;

                public StartData() { }
            }

            public AnimationCurvePart(CurveEasingFunction curveEasingFunction
                , float startX, float startHeight, float heightShift, int degree = 1) {
                CurveEasingFunction = curveEasingFunction;
                StartX = startX;
                StartHeight = startHeight;
                HeightShift = heightShift;
                PolynomialDegree = degree;
            }

            public AnimationCurvePart(CurveEasingFunction curveEasingFunction, StartData starData) {
                CurveEasingFunction = curveEasingFunction;
                StartX = starData.startX;
                StartHeight = starData.startHeight;
                HeightShift = starData.heightShift;
                PolynomialDegree = starData.degree;
            }
        }

        /// <summary>
        /// 获取自定义分段函数在任意给定X值的高度，使您可以轻松创建复杂的动画曲线。X值自动限定在0到1之间，但函数高度可以超出0到1的范围
        /// </summary>
        /// <param name="progress">曲线进度。自动限定在0到1之间</param>
        /// <param name="segments">构成完整动画曲线的曲线段数组</param>
        /// <returns>给定X值的函数高度</returns>
        public static float EvaluateCurve(float progress, params AnimationCurvePart[] segments) {
            if (segments.Length == 0) {
                return 0f;
            }

            if (segments[0].StartX != 0) {
                segments[0].StartX = 0;
            }

            progress = MathHelper.Clamp(progress, 0f, 1f); // 限定进度在0到1之间
            float height = 0f;

            for (int i = 0; i < segments.Length; i++) {
                AnimationCurvePart segment = segments[i];
                float startX = segment.StartX;
                float endX = (i < segments.Length - 1) ? segments[i + 1].StartX : 1f;

                if (progress < startX) {
                    continue;
                }


                if (progress >= endX) {
                    continue;
                }

                float segmentProgress = (progress - startX) / (endX - startX); // 计算段内进度
                height = segment.StartHeight + segment.CurveEasingFunction(segmentProgress, segment.PolynomialDegree) * segment.HeightShift;
                break;
            }
            return height;
        }
        #endregion

        #region DrawUtils

        /// <summary>
        /// 获取指定路径的纹理实例 <see cref="Texture2D"/>
        /// </summary>
        /// <param name="texture">纹理路径（相对于模组内容目录的路径）</param>
        /// <param name="immediateLoad">
        /// 是否立即加载纹理：
        /// <br>- <see langword="true"/>：同步加载纹理（适合需要立即使用的资源）</br>
        /// <br>- <see langword="false"/>：异步加载纹理（提升加载性能，适合非紧急资源）</br>
        /// </param>
        /// <returns>返回加载的 Texture2D 实例</returns>
        public static Texture2D GetT2DValue(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad).Value;
        }

        /// <summary>
        /// 获取指定路径的纹理资源（类型为 Asset&lt;Texture2D&gt;）
        /// </summary>
        /// <param name="texture">纹理路径（相对于模组内容目录的路径）</param>
        /// <param name="immediateLoad">
        /// 是否立即加载纹理：
        /// <br>- <see langword="true"/>：同步加载纹理（适合需要立即使用的资源）</br>
        /// <br>- <see langword="false"/>：异步加载纹理（提升加载性能，适合非紧急资源）</br>
        /// </param>
        /// <returns>返回加载的 Asset&lt;Texture2D&gt; 对象，包含纹理资源及其加载状态</returns>
        public static Asset<Texture2D> GetT2DAsset(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad);
        }

        #endregion

        #region TileUtils
        /// <summary>
        /// 检测该位置是否存在一个实心的固体方块
        /// </summary>
        public static bool HasSolidTile(this Tile tile) => tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        #endregion
    }
}
