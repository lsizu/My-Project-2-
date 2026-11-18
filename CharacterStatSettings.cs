using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStatSettings", menuName = "Game/Character Stat Settings")]
public class CharacterStatSettings : ScriptableObject
{
    [System.Serializable]
    public class StarStats
    {
        [Min(0)] public int attack;
        [Min(0)] public int defense;
        [Min(0)] public int weight = 1;
    }

    [System.Serializable]
    public class CostStats
    {
        [Min(1)] public int cost = 1;
        public StarStats[] star1Options = new StarStats[0];
        public StarStats[] star2Options = new StarStats[0];
        public StarStats[] star3Options = new StarStats[0];
    }

    [Tooltip("Costと★ごとのATK/DEF候補と抽選重みです。")]
    public CostStats[] costs =
    {
        new CostStats
        {
            cost = 1,
            star1Options = new[]
            {
                new StarStats { attack = 3, defense = 0 },
                new StarStats { attack = 2, defense = 1 }
            },
            star2Options = new[]
            {
                new StarStats { attack = 4, defense = 0 },
                new StarStats { attack = 3, defense = 1 },
                new StarStats { attack = 2, defense = 2 }
            },
            star3Options = new[]
            {
                new StarStats { attack = 3, defense = 3 }
            }
        },
        new CostStats
        {
            cost = 2,
            star1Options = new[]
            {
                new StarStats { attack = 3, defense = 1 },
                new StarStats { attack = 2, defense = 2 }
            },
            star2Options = new[]
            {
                new StarStats { attack = 4, defense = 1 },
                new StarStats { attack = 3, defense = 2 },
                new StarStats { attack = 2, defense = 3 }
            },
            star3Options = new[]
            {
                new StarStats { attack = 4, defense = 4 }
            }
        },
        new CostStats
        {
            cost = 3,
            star1Options = new[]
            {
                new StarStats { attack = 3, defense = 2 },
                new StarStats { attack = 2, defense = 3 }
            },
            star2Options = new[]
            {
                new StarStats { attack = 4, defense = 2 },
                new StarStats { attack = 3, defense = 3 },
                new StarStats { attack = 2, defense = 4 }
            },
            star3Options = new[]
            {
                new StarStats { attack = 5, defense = 5 }
            }
        },
        new CostStats
        {
            cost = 4,
            star1Options = new[]
            {
                new StarStats { attack = 4, defense = 2 },
                new StarStats { attack = 3, defense = 3 }
            },
            star2Options = new[]
            {
                new StarStats { attack = 5, defense = 2 },
                new StarStats { attack = 4, defense = 3 },
                new StarStats { attack = 3, defense = 4 }
            },
            star3Options = new[]
            {
                new StarStats { attack = 6, defense = 6 }
            }
        },
        new CostStats
        {
            cost = 5,
            star1Options = new[]
            {
                new StarStats { attack = 5, defense = 2 },
                new StarStats { attack = 4, defense = 3 }
            },
            star2Options = new[]
            {
                new StarStats { attack = 6, defense = 2 },
                new StarStats { attack = 5, defense = 3 },
                new StarStats { attack = 4, defense = 4 }
            },
            star3Options = new[]
            {
                new StarStats { attack = 7, defense = 7 }
            }
        }
    };

    public bool TryGetRandomStats(int cost, int star, out int attack, out int defense)
    {
        attack = 0;
        defense = 0;

        CostStats costStats = FindCostStats(cost);
        if (costStats == null)
            return false;

        StarStats[] options = GetStarOptions(costStats, star);
        if (options == null || options.Length == 0)
        {
            Debug.LogError(
                "No stat options configured for Cost " + cost + ", Star " + star + ".",
                this);
            return false;
        }

        long totalWeight = 0;
        foreach (StarStats option in options)
        {
            if (option == null)
                continue;
            if (option.weight < 0)
            {
                Debug.LogError(
                    "Stat option weight cannot be negative for Cost " + cost + ", Star " + star + ".",
                    this);
                return false;
            }
            if (option.weight == 0)
                continue;

            totalWeight += option.weight;
        }

        if (totalWeight <= 0 || totalWeight > int.MaxValue)
        {
            Debug.LogError(
                "Stat option weights must sum to a value between 1 and " +
                int.MaxValue + " for Cost " + cost + ", Star " + star + ".",
                this);
            return false;
        }

        int roll = Random.Range(0, (int)totalWeight);
        StarStats selected = null;
        foreach (StarStats option in options)
        {
            if (option == null || option.weight <= 0)
                continue;

            if (roll < option.weight)
            {
                selected = option;
                break;
            }

            roll -= option.weight;
        }

        if (selected == null)
            return false;

        attack = selected.attack;
        defense = selected.defense;
        return true;
    }

    public bool TryGetStatRanges(
        int cost,
        int star,
        out int minimumAttack,
        out int maximumAttack,
        out int minimumDefense,
        out int maximumDefense)
    {
        minimumAttack = int.MaxValue;
        maximumAttack = int.MinValue;
        minimumDefense = int.MaxValue;
        maximumDefense = int.MinValue;

        CostStats costStats = FindCostStats(cost);
        if (costStats == null)
            return false;

        StarStats[] options = GetStarOptions(costStats, star);
        if (options == null || options.Length == 0)
            return false;

        foreach (StarStats option in options)
        {
            if (option == null || option.weight <= 0)
                continue;

            minimumAttack = Mathf.Min(minimumAttack, option.attack);
            maximumAttack = Mathf.Max(maximumAttack, option.attack);
            minimumDefense = Mathf.Min(minimumDefense, option.defense);
            maximumDefense = Mathf.Max(maximumDefense, option.defense);
        }

        return minimumAttack != int.MaxValue;
    }

    public bool TryGetStatOptions(int cost, int star, out StarStats[] options)
    {
        CostStats costStats = FindCostStats(cost);
        if (costStats == null)
        {
            options = null;
            return false;
        }

        options = GetStarOptions(costStats, star);
        if (options == null)
            return false;

        foreach (StarStats option in options)
        {
            if (option != null && option.weight > 0)
                return true;
        }

        options = null;
        return false;
    }

    public static CharacterStatSettings FindForCurrentScene()
    {
        CharacterDetailPanel detailPanel =
            FindAnyObjectByType<CharacterDetailPanel>(FindObjectsInactive.Include);
        if (detailPanel != null && detailPanel.characterStatSettings != null)
            return detailPanel.characterStatSettings;

        BattleManager battleManager =
            FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (battleManager != null && battleManager.characterStatSettings != null)
            return battleManager.characterStatSettings;

        BoardCharacter boardCharacter =
            FindAnyObjectByType<BoardCharacter>(FindObjectsInactive.Include);
        if (boardCharacter != null && boardCharacter.characterStatSettings != null)
            return boardCharacter.characterStatSettings;

        Debug.LogError("CharacterStatSettings is not assigned in the current scene.");
        return null;
    }

    private CostStats FindCostStats(int cost)
    {
        if (costs == null)
            return null;

        foreach (CostStats costStats in costs)
        {
            if (costStats != null && costStats.cost == cost)
                return costStats;
        }

        return null;
    }

    private StarStats[] GetStarOptions(CostStats costStats, int star)
    {
        switch (star)
        {
            case 1: return costStats.star1Options;
            case 2: return costStats.star2Options;
            case 3: return costStats.star3Options;
            default: return null;
        }
    }

}
