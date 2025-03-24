using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.WildTrees;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Crops;

namespace TimeleftMod
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            helper.Events.Display.RenderedHud += OnRenderedHud;
        }

        private void OnRenderedHud(object sender, StardewModdingAPI.Events.RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            Vector2 mouseTile = Game1.currentCursorTile;

            HoeDirt hoeDirt = GetHoeDirtUnderCursor(mouseTile);
            if (hoeDirt?.crop != null && !hoeDirt.crop.dead.Value)
            {
                string displayName = GetCropDisplayName(hoeDirt.crop);
                int daysRemaining = CalculateDaysRemaining(hoeDirt.crop);
                string hoverText = $"{displayName}\n{(daysRemaining > 0 ? $"{daysRemaining} day{(daysRemaining != 1 ? "s" : "")} left" : "Ready to harvest!")}";
                DrawHoverText(e.SpriteBatch, hoverText);
                return;
            }

            Building building = GetBuildingUnderCursor(mouseTile);
            if (building != null && (building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0))
            {
                string buildingName = GetBuildingDisplayName(building);
                int daysLeft = building.daysOfConstructionLeft.Value > 0 
                    ? building.daysOfConstructionLeft.Value 
                    : building.daysUntilUpgrade.Value;
                
                string status = building.daysOfConstructionLeft.Value > 0 ? "under construction" : "being upgraded";
                string hoverText = $"{buildingName} is {status}\n{daysLeft} day{(daysLeft != 1 ? "s" : "")} left";
                DrawHoverText(e.SpriteBatch, hoverText);
                return;
            }

            TerrainFeature terrainFeature = GetTerrainFeatureUnderCursor(mouseTile);
            if (terrainFeature != null)
            {
                if (terrainFeature is Tree tree)
                {
                    HandleTree(tree, e.SpriteBatch);
                    return;
                }
                else if (terrainFeature is FruitTree fruitTree)
                {
                    HandleFruitTree(fruitTree, e.SpriteBatch);
                    return;
                }
            }

            StardewValley.Object machine = GetObjectUnderCursor(mouseTile);
            if (machine != null && machine.MinutesUntilReady > 0 && machine.heldObject.Value != null)
            {
                if (machine.IsTapper())
                {
                    TerrainFeature tf = GetTerrainFeatureUnderCursor(mouseTile);
                    if (tf is Tree)
                        return;
                }

                HandleMachine(machine, e.SpriteBatch);
                return;
            }
        }

        private void HandleTree(Tree tree, SpriteBatch spriteBatch)
        {
            if (tree.stump.Value || tree.health.Value <= 0)
                return;

            string displayName = GetTreeDisplayName(tree);
            int growthStage = tree.growthStage.Value;
            bool isMature = growthStage >= 5;

            string hoverText;
            WildTreeData data = tree.GetData();
            if (data != null && !isMature)
            {
                int remainingStages = 5 - growthStage;
                float growthChance = tree.fertilized.Value ? data.FertilizedGrowthChance : data.GrowthChance;
                growthChance = Math.Max(0.01f, growthChance);

                if (growthChance >= 1.0f)
                {
                    int daysLeft = remainingStages;
                    hoverText = $"{displayName}\n{daysLeft} day{(daysLeft != 1 ? "s" : "")} left";
                }
                else
                {
                    float expectedDays = remainingStages / growthChance;
                    hoverText = $"{displayName}\n~{expectedDays:F1} days left (average)";
                }
            }
            else
            {
                hoverText = $"{displayName}";
            }

            if (tree.tapped.Value)
            {
                Vector2 treeTile = tree.Tile;
                StardewValley.Object tapper = GetObjectAtTile(treeTile);
                if (tapper != null && tapper.IsTapper())
                {
                    string tapperInfo = GetTapperInfo(tapper);
                    if (!string.IsNullOrEmpty(tapperInfo))
                    {
                        hoverText += "\n" + tapperInfo;
                    }
                }
            }

            DrawHoverText(spriteBatch, hoverText);
        }

        private void HandleFruitTree(FruitTree fruitTree, SpriteBatch spriteBatch)
        {
            if (fruitTree.stump.Value)
                return;

            string displayName = fruitTree.GetDisplayName();
            int daysUntilMature = fruitTree.daysUntilMature.Value;

            string hoverText;
            if (daysUntilMature > 0)
            {
                hoverText = $"{displayName}\n{daysUntilMature} day{(daysUntilMature != 1 ? "s" : "")} until mature";
            }
            else
            {
                if (fruitTree.fruit.Count > 0)
                {
                    hoverText = $"{displayName}\nReady to harvest! ({fruitTree.fruit.Count} fruits)";
                }
                else
                {
                    if (fruitTree.IsInSeasonHere())
                    {
                        hoverText = $"{displayName}\nMature, no fruit today";
                    }
                    else
                    {
                        hoverText = $"{displayName}\nMature, not in season";
                    }
                }
            }

            DrawHoverText(spriteBatch, hoverText);
        }

        private void HandleMachine(StardewValley.Object machine, SpriteBatch spriteBatch)
        {
            string machineName = machine.DisplayName;
            StardewValley.Object output = machine.heldObject.Value;
            string outputName = output?.DisplayName ?? "???";
            string inputName = "???";
            string inputId = "???";

            if (output != null && output.preservedParentSheetIndex.Value != null && output.preservedParentSheetIndex.Value != "-1")
            {
                inputId = $"(O){output.preservedParentSheetIndex.Value}";
                ParsedItemData inputData = ItemRegistry.GetData(inputId);
                if (inputData != null)
                    inputName = inputData.DisplayName;
            }
            else if (machine.lastInputItem.Value != null)
            {
                inputName = machine.lastInputItem.Value.DisplayName;
                inputId = machine.lastInputItem.Value.QualifiedItemId;
            }
            else if (machine.GetMachineData() is MachineData machineData)
            {
                foreach (MachineOutputRule rule in machineData.OutputRules)
                {
                    foreach (MachineOutputTriggerRule trigger in rule.Triggers)
                    {
                        if (trigger.RequiredItemId != null)
                        {
                            inputId = trigger.RequiredItemId;
                            ParsedItemData inputData = ItemRegistry.GetData(inputId);
                            if (inputData != null)
                                inputName = inputData.DisplayName;
                            break;
                        }
                        else if (trigger.RequiredTags != null && trigger.RequiredTags.Contains("egg_item"))
                        {
                            inputName = "Unknown";
                            break;
                        }
                    }
                    if (inputName != "???") break;
                }
            }

            int minutesLeft = machine.MinutesUntilReady;
            string timeText = GetTimeText(minutesLeft);

            string hoverText;
            if (output == null)
            {
                hoverText = $"{machineName}\nEmpty";
            }
            else if (inputName == "???")
            {
                hoverText = $"{machineName}\nProducing: {outputName}\nTime left: {timeText}";
            }
            else
            {
                hoverText = $"{machineName}\n1 {inputName} → 1 {outputName}\nTime left: {timeText}";
            }

            DrawHoverText(spriteBatch, hoverText);
        }

        private string GetTimeText(int minutes)
        {
            if (minutes <= 0)
                return "Ready!";

            int days = minutes / (60 * 24);
            int remainingHours = (minutes % (60 * 24)) / 60;
            int remainingMinutes = minutes % 60;

            if (days > 0)
            {
                if (remainingHours > 0)
                    return $"{days}d {remainingHours}h {remainingMinutes}m";
                else
                    return $"{days}d {remainingMinutes}m";
            }
            else if (remainingHours > 0)
            {
                if (remainingMinutes > 0)
                    return $"{remainingHours}h {remainingMinutes}m";
                else
                    return $"{remainingHours}h";
            }
            else
            {
                return $"{remainingMinutes}m";
            }
        }

        private StardewValley.Object GetObjectAtTile(Vector2 tile)
        {
            if (Game1.currentLocation.objects.TryGetValue(tile, out var obj))
                return obj;
            return null;
        }

        private string GetTapperInfo(StardewValley.Object tapper)
        {
            if (tapper.MinutesUntilReady > 0)
            {
                string timeText = GetTimeText(tapper.MinutesUntilReady);
                string productName = tapper.heldObject.Value?.DisplayName ?? "???";
                return $"Tapper:\n- Producing: {productName}\n- Ready in: {timeText}";
            }
            else if (tapper.heldObject.Value != null)
            {
                return $"Tapper:\n- {tapper.heldObject.Value.DisplayName} is ready to collect!";
            }
            return null;
        }

        private StardewValley.Object GetObjectUnderCursor(Vector2 mouseTile)
        {
            if (Game1.currentLocation.objects.TryGetValue(mouseTile, out var obj))
                return obj;
            return null;
        }

        private string GetTreeDisplayName(Tree tree)
        {
            WildTreeData data = tree.GetData();
            if (data == null)
                return "Unknown Tree";

            // For mature trees (growthStage >= 5), always use the tree's display name
            if (tree.growthStage.Value >= 5)
            {
                return tree.treeType.Value switch
                {
                    "1" => "Oak Tree",
                    "2" => "Maple Tree",
                    "3" => "Pine Tree",
                    "6" => "Palm Tree",
                    "7" => "Mushroom Tree",
                    "8" => "Mahogany Tree",
                    "9" => "Palm Tree",
                    "10" => "Green Rain Tree (Oak)",
                    "11" => "Green Rain Tree (Maple)",
                    "12" => "Green Rain Fern",
                    "13" => "Mystic Tree",
                    _ => "Unknown Tree"
                };
            }
            // For seeds/saplings, show the seed name if available
            else if (!string.IsNullOrEmpty(data.SeedItemId))
            {
                ParsedItemData seedData = ItemRegistry.GetData(data.SeedItemId);
                if (seedData != null)
                    return seedData.DisplayName;
            }

            // Fallback to tree type name
            return tree.treeType.Value switch
            {
                "1" => "Oak Seed",
                "2" => "Maple Seed",
                "3" => "Pine Cone",
                "6" => "Palm Seed",
                "7" => "Mushroom Seed",
                "8" => "Mahogany Seed",
                "9" => "Palm Seed",
                "10" => "Green Rain Seed (Oak)",
                "11" => "Green Rain Seed (Maple)",
                "12" => "Green Rain Fern Spore",
                "13" => "Mystic Seed",
                _ => "Unknown Seed"
            };
        }

        private TerrainFeature GetTerrainFeatureUnderCursor(Vector2 mouseTile)
        {
            if (Game1.currentLocation.terrainFeatures.TryGetValue(mouseTile, out var terrainFeature))
                return terrainFeature;
            return null;
        }

        private void DrawHoverText(SpriteBatch spriteBatch, string text)
        {
            IClickableMenu.drawHoverText(spriteBatch, text, Game1.smallFont);
        }

        private HoeDirt GetHoeDirtUnderCursor(Vector2 mouseTile)
        {
            if (Game1.currentLocation.terrainFeatures.TryGetValue(mouseTile, out var terrainFeature) && terrainFeature is HoeDirt hoeDirt)
                return hoeDirt;

            if (Game1.currentLocation.objects.TryGetValue(mouseTile, out var obj) && obj is IndoorPot pot)
                return pot.hoeDirt.Value;

            return null;
        }

        private Building GetBuildingUnderCursor(Vector2 mouseTile)
        {
            if (Game1.currentLocation is GameLocation buildableLocation)
            {
                foreach (Building building in buildableLocation.buildings)
                {
                    if (building.occupiesTile(mouseTile))
                        return building;
                }
            }
            return null;
        }

        private static int CalculateDaysRemaining(Crop crop)
        {
            if (crop.dead.Value)
                return 0;

            CropData data = crop.GetData();

            if (crop.fullyGrown.Value)
            {
                if (data != null && data.RegrowDays > 0)
                {
                    return Math.Max(0, crop.dayOfCurrentPhase.Value);
                }
                return 0;
            }

            int daysRemaining = 0;
            int currentPhase = crop.currentPhase.Value;
            int dayInCurrentPhase = crop.dayOfCurrentPhase.Value;

            if (currentPhase < crop.phaseDays.Count - 1)
                daysRemaining += Math.Max(0, crop.phaseDays[currentPhase] - dayInCurrentPhase);

            for (int i = currentPhase + 1; i < crop.phaseDays.Count - 1; i++)
                daysRemaining += Math.Max(0, crop.phaseDays[i]);

            return daysRemaining;
        }

        private static string GetCropDisplayName(Crop crop)
        {
            if (crop.forageCrop.Value)
            {
                return crop.whichForageCrop.Value switch
                {
                    "1" => "Spring Onion",
                    "2" => "Ginger",
                    _ => "Forage"
                };
            }

            if (ItemRegistry.GetData("(O)" + crop.indexOfHarvest.Value) is ParsedItemData data)
                return data.DisplayName;

            return "Unknown Crop";
        }
        
        private string GetBuildingDisplayName(Building building)
        {
            if (Game1.buildingData.TryGetValue(building.buildingType.Value, out var buildingData))
            {
                string name = buildingData.Name;
                if (name.StartsWith("[") && name.Contains("]"))
                {
                    string key = name.TrimStart('[').Split(']')[0].Trim();
                    key = key.Replace("LocalizedText ", "").Replace("Localized Text Strings\\", "Strings\\");

                    try
                    {
                        return Game1.content.LoadString(key);
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"[Timeleft] Failed loading localized name for key {key}: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
                    }
                }
                return name;
            }

            return building.buildingType.Value switch
            {
                "Coop" => "Coop",
                "Barn" => "Barn",
                "Shed" => "Shed",
                "Mill" => "Mill",
                "Slime Hutch" => "Slime Hutch",
                "Stable" => "Stable",
                "Well" => "Well",
                "Fish Pond" => "Fish Pond",
                "Cabin" => "Cabin",
                "Earth Obelisk" => "Earth Obelisk",
                "Water Obelisk" => "Water Obelisk",
                "Desert Obelisk" => "Desert Obelisk",
                "Island Obelisk" => "Island Obelisk",
                "Gold Clock" => "Gold Clock",
                _ => building.buildingType.Value
            };
        }
    }
}