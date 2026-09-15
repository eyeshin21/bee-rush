using System;
using System.Collections.Generic;
using System.Text;
using HoneyBeeRush.Core;

namespace HoneyBeeRush.Data
{
    [Serializable]
    public struct BoardVolumeSettings
    {
        public int maxHalfThickness;
        public int cellBudget;
        public float growthCap;
        public float viewYaw;
        public float viewPitch;

        public static BoardVolumeSettings Default => new BoardVolumeSettings
        {
            maxHalfThickness = 2,
            cellBudget = 2000,
            growthCap = 2.5f,
            viewYaw = 24f,
            viewPitch = -16f
        };
    }

    public static class BoardVolumeBuilder
    {
        private struct ExtrusionPlan
        {
            public int duplicateCount;
            public int maxDepth;
            public int budget;
            public int maxHalf;
            public int step;
            public int thickestHalf;
        }

        public static bool ConvertPlanarToVolume(LevelData level, BoardVolumeSettings settings, out string report)
        {
            if (level == null)
            {
                report = "[BoardVolumeBuilder] Level is null; nothing to convert.";
                return false;
            }

            string label = "Level " + level.index + " (" + level.name + ")";

            if (level.IsVolumetric)
            {
                report = "[BoardVolumeBuilder] " + label + " is already volumetric; nothing to convert.";
                return false;
            }

            int cellsBefore = 0;
            bool carriesLayers = false;
            if (level.cells != null)
            {
                for (int i = 0; i < level.cells.Count; i++)
                {
                    CellDef cell = level.cells[i];
                    if (cell == null)
                    {
                        continue;
                    }

                    cellsBefore++;
                    if (cell.layer != 0)
                    {
                        carriesLayers = true;
                    }
                }
            }

            if (cellsBefore == 0)
            {
                report = "[BoardVolumeBuilder] " + label + " has no cells; nothing to convert.";
                return false;
            }

            if (carriesLayers)
            {
                level.cellFormat = LevelData.CellFormatVolume;
                report = "[BoardVolumeBuilder] " + label + " is marked planar but already carries " + level.LayerCount +
                         " layer(s); marked volumetric and kept its " + cellsBefore + " cells without extrusion.";
                return true;
            }

            if (level.crates == null)
            {
                level.crates = new List<CrateDef>();
            }

            int beesBefore = level.TotalBees;
            Dictionary<ColorType, int> nectarBefore = NectarByColor(level.cells);

            ExtrusionPlan plan;
            List<CellDef> extruded = Extrude(level.cells, settings, out plan);
            Dictionary<ColorType, int> nectarAfter = NectarByColor(extruded);

            List<ColorType> colors = new List<ColorType>(nectarBefore.Keys);
            foreach (ColorType color in nectarAfter.Keys)
            {
                if (!nectarBefore.ContainsKey(color))
                {
                    colors.Add(color);
                }
            }

            colors.Sort();

            Dictionary<ColorType, int> delta = new Dictionary<ColorType, int>(colors.Count);
            List<string> warnings = new List<string>();
            for (int i = 0; i < colors.Count; i++)
            {
                ColorType color = colors[i];
                nectarBefore.TryGetValue(color, out int before);
                nectarAfter.TryGetValue(color, out int after);
                int change = after - before;
                delta[color] = change;

                if (change != 0 && !HasCrateOfColor(level.crates, color))
                {
                    warnings.Add("colour " + color + " changed nectar by " + change + " but has no crate to absorb it");
                }
            }

            RebalanceCrates(level.crates, delta);

            level.cells = extruded;
            level.cellFormat = LevelData.CellFormatVolume;
            level.boardViewYaw = settings.viewYaw;
            level.boardViewPitch = settings.viewPitch;
            level.gridBoardData = null;
            level.Normalize();

            StringBuilder sb = new StringBuilder();
            sb.Append("[BoardVolumeBuilder] ").Append(label).Append(": cells ").Append(cellsBefore).Append(" -> ").Append(extruded.Count);
            sb.Append(", layers 1 -> ").Append(plan.thickestHalf * 2 + 1);
            if (plan.maxHalf > 0)
            {
                sb.Append(", maxHalf ").Append(plan.maxHalf).Append(" step ").Append(plan.step);
            }
            else
            {
                sb.Append(", flat (no thickening fits)");
            }

            sb.Append(", depth ").Append(plan.maxDepth).Append(", budget ").Append(plan.budget);
            sb.Append(", bees ").Append(beesBefore).Append(" -> ").Append(level.TotalBees);
            if (plan.duplicateCount > 0)
            {
                sb.Append(", duplicate (q, r) dropped ").Append(plan.duplicateCount);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                sb.AppendLine();
                sb.Append("  WARNING ").Append(warnings[i]);
            }

            report = sb.ToString();
            return true;
        }

        public static List<CellDef> ExtrudePicture(IReadOnlyList<CellDef> planarCells, BoardVolumeSettings settings)
        {
            ExtrusionPlan plan;
            return Extrude(planarCells, settings, out plan);
        }

        public static void RebalanceCrates(List<CrateDef> crates, Dictionary<ColorType, int> nectarDeltaByColor)
        {
            if (crates == null || crates.Count == 0 || nectarDeltaByColor == null || nectarDeltaByColor.Count == 0)
            {
                return;
            }

            List<ColorType> colors = new List<ColorType>(nectarDeltaByColor.Keys);
            colors.Sort();

            List<int> members = new List<int>();
            for (int c = 0; c < colors.Count; c++)
            {
                ColorType color = colors[c];
                int delta = nectarDeltaByColor[color];
                if (delta == 0)
                {
                    continue;
                }

                members.Clear();
                for (int i = 0; i < crates.Count; i++)
                {
                    CrateDef crate = crates[i];
                    if (crate != null && crate.colorType == color)
                    {
                        members.Add(i);
                    }
                }

                if (members.Count == 0)
                {
                    continue;
                }

                RebalanceColor(crates, members, delta);
            }
        }

        private static void RebalanceColor(List<CrateDef> crates, List<int> members, int delta)
        {
            int count = members.Count;
            long currentSum = 0;
            for (int i = 0; i < count; i++)
            {
                currentSum += crates[members[i]].beeCount;
            }

            long targetSum = currentSum + delta;
            long floor = targetSum >= count ? 1 : 0;

            long[] targets = new long[count];
            long[] shares = new long[count];
            long[] remainders = new long[count];
            bool[] pinned = new bool[count];
            bool[] bumped = new bool[count];
            int freeCount = count;
            long pinnedSum = 0;

            while (freeCount > 0)
            {
                long freeCurrent = 0;
                long freeWeight = 0;
                for (int i = 0; i < count; i++)
                {
                    if (pinned[i])
                    {
                        continue;
                    }

                    int bees = crates[members[i]].beeCount;
                    freeCurrent += bees;
                    freeWeight += bees > 0 ? bees : 0;
                }

                bool evenSplit = freeWeight <= 0;
                if (evenSplit)
                {
                    freeWeight = freeCount;
                }

                long freeDelta = targetSum - pinnedSum - freeCurrent;
                long magnitude = freeDelta < 0 ? -freeDelta : freeDelta;
                long sign = freeDelta < 0 ? -1 : 1;
                long assigned = 0;

                for (int i = 0; i < count; i++)
                {
                    shares[i] = 0;
                    remainders[i] = 0;
                    bumped[i] = false;
                    if (pinned[i])
                    {
                        continue;
                    }

                    int bees = crates[members[i]].beeCount;
                    long weight = evenSplit ? 1 : (bees > 0 ? bees : 0);
                    long product = magnitude * weight;
                    shares[i] = product / freeWeight;
                    remainders[i] = product % freeWeight;
                    assigned += shares[i];
                }

                long leftover = magnitude - assigned;
                while (leftover > 0)
                {
                    int pick = -1;
                    for (int i = 0; i < count; i++)
                    {
                        if (pinned[i] || bumped[i])
                        {
                            continue;
                        }

                        if (pick < 0 || remainders[i] > remainders[pick])
                        {
                            pick = i;
                        }
                    }

                    if (pick < 0)
                    {
                        break;
                    }

                    shares[pick]++;
                    bumped[pick] = true;
                    leftover--;
                }

                bool violated = false;
                for (int i = 0; i < count; i++)
                {
                    if (pinned[i])
                    {
                        continue;
                    }

                    targets[i] = crates[members[i]].beeCount + sign * shares[i];
                    if (targets[i] < floor)
                    {
                        violated = true;
                    }
                }

                if (!violated)
                {
                    break;
                }

                for (int i = 0; i < count; i++)
                {
                    if (pinned[i] || targets[i] >= floor)
                    {
                        continue;
                    }

                    pinned[i] = true;
                    targets[i] = floor;
                    pinnedSum += floor;
                    freeCount--;
                }
            }

            for (int i = 0; i < count; i++)
            {
                long value = targets[i];
                if (value < floor)
                {
                    value = floor;
                }

                if (value > int.MaxValue)
                {
                    value = int.MaxValue;
                }

                crates[members[i]].beeCount = (int)value;
            }
        }

        private static List<CellDef> Extrude(IReadOnlyList<CellDef> planarCells, BoardVolumeSettings settings, out ExtrusionPlan plan)
        {
            plan = new ExtrusionPlan();
            List<CellDef> result = new List<CellDef>();
            if (planarCells == null || planarCells.Count == 0)
            {
                return result;
            }

            List<CellDef> sources = new List<CellDef>(planarCells.Count);
            List<HexCoord> coords = new List<HexCoord>(planarCells.Count);
            Dictionary<HexCoord, int> indexByCoord = new Dictionary<HexCoord, int>(planarCells.Count);
            int duplicates = 0;

            for (int i = 0; i < planarCells.Count; i++)
            {
                CellDef cell = planarCells[i];
                if (cell == null)
                {
                    continue;
                }

                HexCoord coord = new HexCoord(cell.q, cell.r);
                if (indexByCoord.ContainsKey(coord))
                {
                    duplicates++;
                    continue;
                }

                indexByCoord.Add(coord, sources.Count);
                sources.Add(cell);
                coords.Add(coord);
            }

            int count = sources.Count;
            plan.duplicateCount = duplicates;
            if (count == 0)
            {
                return result;
            }

            int[] depth = ComputeSilhouetteDepth(coords, indexByCoord);

            int maxDepth = 1;
            for (int i = 0; i < count; i++)
            {
                if (depth[i] > maxDepth)
                {
                    maxDepth = depth[i];
                }
            }

            int[] histogram = new int[maxDepth + 1];
            for (int i = 0; i < count; i++)
            {
                histogram[depth[i]]++;
            }

            int budget = ComputeBudget(count, settings);
            ChooseThickness(histogram, maxDepth, count, budget, settings.maxHalfThickness, out int maxHalf, out int step);

            int[] halfThickness = new int[count];
            int thickest = 0;
            long total = 0;
            for (int i = 0; i < count; i++)
            {
                int t = maxHalf > 0 && step > 0 ? Math.Min(maxHalf, (depth[i] - 1) / step) : 0;
                halfThickness[i] = t;
                total += 2 * t + 1;
                if (t > thickest)
                {
                    thickest = t;
                }
            }

            plan.maxDepth = maxDepth;
            plan.budget = budget;
            plan.maxHalf = maxHalf;
            plan.step = step;
            plan.thickestHalf = thickest;

            result.Capacity = (int)Math.Min(total, int.MaxValue);
            for (int layer = -thickest; layer <= thickest; layer++)
            {
                int reach = layer < 0 ? -layer : layer;
                for (int i = 0; i < count; i++)
                {
                    if (halfThickness[i] < reach)
                    {
                        continue;
                    }

                    CellDef source = sources[i];
                    result.Add(new CellDef
                    {
                        q = source.q,
                        r = source.r,
                        layer = layer,
                        colorType = source.colorType,
                        nectar = source.nectar,
                        locked = source.locked
                    });
                }
            }

            return result;
        }

        private static int[] ComputeSilhouetteDepth(List<HexCoord> coords, Dictionary<HexCoord, int> indexByCoord)
        {
            int count = coords.Count;
            int[] depth = new int[count];
            Queue<int> frontier = new Queue<int>(count);
            int directionCount = HexCoord.Directions.Length;

            for (int i = 0; i < count; i++)
            {
                HexCoord coord = coords[i];
                for (int dir = 0; dir < directionCount; dir++)
                {
                    if (!indexByCoord.ContainsKey(coord.Neighbor(dir)))
                    {
                        depth[i] = 1;
                        frontier.Enqueue(i);
                        break;
                    }
                }
            }

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                HexCoord coord = coords[current];
                for (int dir = 0; dir < directionCount; dir++)
                {
                    if (indexByCoord.TryGetValue(coord.Neighbor(dir), out int next) && depth[next] == 0)
                    {
                        depth[next] = depth[current] + 1;
                        frontier.Enqueue(next);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (depth[i] <= 0)
                {
                    depth[i] = 1;
                }
            }

            return depth;
        }

        private static int ComputeBudget(int count, BoardVolumeSettings settings)
        {
            double scaled = (double)count * settings.growthCap;
            int growthLimit;
            if (double.IsNaN(scaled) || scaled <= 0d)
            {
                growthLimit = 0;
            }
            else if (scaled >= int.MaxValue)
            {
                growthLimit = int.MaxValue;
            }
            else
            {
                growthLimit = (int)Math.Floor(scaled);
            }

            return Math.Max(count, Math.Min(settings.cellBudget, growthLimit));
        }

        private static void ChooseThickness(int[] histogram, int maxDepth, int count, int budget, int maxHalfThickness, out int chosenHalf, out int chosenStep)
        {
            chosenHalf = 0;
            chosenStep = 0;
            long bestTotal = count;
            int bestReached = 0;

            int halfCap = Math.Min(maxHalfThickness, maxDepth - 1);
            for (int maxHalf = halfCap; maxHalf >= 1; maxHalf--)
            {
                for (int step = 1; step <= maxDepth; step++)
                {
                    long total = 0;
                    for (int d = 1; d <= maxDepth; d++)
                    {
                        int cellsAtDepth = histogram[d];
                        if (cellsAtDepth == 0)
                        {
                            continue;
                        }

                        int t = Math.Min(maxHalf, (d - 1) / step);
                        total += (long)cellsAtDepth * (2 * t + 1);
                    }

                    if (total <= count)
                    {
                        break;
                    }

                    if (total > budget)
                    {
                        continue;
                    }

                    int reached = Math.Min(maxHalf, (maxDepth - 1) / step);
                    bool better = reached > bestReached
                                  || (reached == bestReached && (total > bestTotal
                                      || (total == bestTotal && (step < chosenStep || (step == chosenStep && maxHalf > chosenHalf)))));
                    if (!better)
                    {
                        continue;
                    }

                    bestReached = reached;
                    bestTotal = total;
                    chosenHalf = maxHalf;
                    chosenStep = step;
                }
            }
        }

        private static Dictionary<ColorType, int> NectarByColor(IReadOnlyList<CellDef> cells)
        {
            Dictionary<ColorType, int> map = new Dictionary<ColorType, int>();
            if (cells == null)
            {
                return map;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                CellDef cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                map.TryGetValue(cell.colorType, out int current);
                map[cell.colorType] = current + cell.nectar;
            }

            return map;
        }

        private static bool HasCrateOfColor(List<CrateDef> crates, ColorType color)
        {
            if (crates == null)
            {
                return false;
            }

            for (int i = 0; i < crates.Count; i++)
            {
                CrateDef crate = crates[i];
                if (crate != null && crate.colorType == color)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
