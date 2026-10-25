using MegaCrit.Sts2.Core.Models;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>Potion artwork and its optional official model association.</summary>
public sealed record PotionEmoteDefinition(string? TypeName, EmoteType Emote, string AssetName, bool Unused);

/// <summary>Optional-model lookup without statically referencing individual potions.</summary>
public static class PotionEmotes
{
    /// <summary>Public API version for potion lookup.</summary>
    public const int ApiVersion = 1;

    // IDs are permanent. Missing models are still usable as artwork-only emotes.
    private static readonly PotionEmoteDefinition[] Definitions =
    [
        new("Ambergris", EmoteType.PotionAmbergris, "ambergris", false),
        new("Ashwater", EmoteType.PotionAshwater, "ashwater", false),
        new("AttackPotion", EmoteType.PotionAttackPotion, "attack_potion", false),
        new("BeetleJuice", EmoteType.PotionBeetleJuice, "beetle_juice", false),
        new("BlessingOfTheForge", EmoteType.PotionBlessingOfTheForge, "blessing_of_the_forge", false),
        new("BlockPotion", EmoteType.PotionBlockPotion, "block_potion", false),
        new("BloodPotion", EmoteType.PotionBloodPotion, "blood_potion", false),
        new("BoneBrew", EmoteType.PotionBoneBrew, "bone_brew", false),
        new("BottledPotential", EmoteType.PotionBottledPotential, "bottled_potential", false),
        new("Clarity", EmoteType.PotionClarity, "clarity", false),
        new("ColorlessPotion", EmoteType.PotionColorlessPotion, "colorless_potion", false),
        new("CosmicConcoction", EmoteType.PotionCosmicConcoction, "cosmic_concoction", false),
        new("CunningPotion", EmoteType.PotionCunningPotion, "cunning_potion", false),
        new("CureAll", EmoteType.PotionCureAll, "cure_all", false),
        new("DexterityPotion", EmoteType.PotionDexterityPotion, "dexterity_potion", false),
        new("DistilledChaos", EmoteType.PotionDistilledChaos, "distilled_chaos", false),
        new("DropletOfPrecognition", EmoteType.PotionDropletOfPrecognition, "droplet_of_precognition", false),
        new("Duplicator", EmoteType.PotionDuplicator, "duplicator", false),
        new("EnergyPotion", EmoteType.PotionEnergyPotion, "energy_potion", false),
        new("EntropicBrew", EmoteType.PotionEntropicBrew, "entropic_brew", false),
        new("EssenceOfDarkness", EmoteType.PotionEssenceOfDarkness, "essence_of_darkness", false),
        new("ExplosiveAmpoule", EmoteType.PotionExplosiveAmpoule, "explosive_ampoule", false),
        new("FairyInABottle", EmoteType.PotionFairyInABottle, "fairy_in_a_bottle", false),
        new("FirePotion", EmoteType.PotionFirePotion, "fire_potion", false),
        new("FlexPotion", EmoteType.PotionFlexPotion, "flex_potion", false),
        new("FocusPotion", EmoteType.PotionFocusPotion, "focus_potion", false),
        new("Fortifier", EmoteType.PotionFortifier, "fortifier", false),
        new("FoulPotion", EmoteType.PotionFoulPotion, "foul_potion", false),
        new("FruitJuice", EmoteType.PotionFruitJuice, "fruit_juice", false),
        new("FyshOil", EmoteType.PotionFyshOil, "fysh_oil", false),
        new("GamblersBrew", EmoteType.PotionGamblersBrew, "gamblers_brew", false),
        new("GhostInAJar", EmoteType.PotionGhostInAJar, "ghost_in_a_jar", false),
        new("GigantificationPotion", EmoteType.PotionGigantificationPotion, "gigantification_potion", false),
        new("GlowwaterPotion", EmoteType.PotionGlowwaterPotion, "glowwater_potion", false),
        new("HeartOfIron", EmoteType.PotionHeartOfIron, "heart_of_iron", false),
        new("KingsCourage", EmoteType.PotionKingsCourage, "kings_courage", false),
        new("LiquidBronze", EmoteType.PotionLiquidBronze, "liquid_bronze", false),
        new("LiquidMemories", EmoteType.PotionLiquidMemories, "liquid_memories", false),
        new("LuckyTonic", EmoteType.PotionLuckyTonic, "lucky_tonic", false),
        new("MazalethsGift", EmoteType.PotionMazalethsGift, "mazaleths_gift", false),
        new("OrobicAcid", EmoteType.PotionOrobicAcid, "orobic_acid", false),
        new("PoisonPotion", EmoteType.PotionPoisonPotion, "poison_potion", false),
        new("PotOfGhouls", EmoteType.PotionPotOfGhouls, "pot_of_ghouls", false),
        new("PotionOfBinding", EmoteType.PotionPotionOfBinding, "potion_of_binding", false),
        new("PotionOfCapacity", EmoteType.PotionPotionOfCapacity, "potion_of_capacity", false),
        new("PotionOfDoom", EmoteType.PotionPotionOfDoom, "potion_of_doom", false),
        new("PotionShapedRock", EmoteType.PotionPotionShapedRock, "potion_shaped_rock", false),
        new("PowderedDemise", EmoteType.PotionPowderedDemise, "powdered_demise", false),
        new("PowerPotion", EmoteType.PotionPowerPotion, "power_potion", false),
        new("RadiantTincture", EmoteType.PotionRadiantTincture, "radiant_tincture", false),
        new("RegenPotion", EmoteType.PotionRegenPotion, "regen_potion", false),
        new("ShacklingPotion", EmoteType.PotionShacklingPotion, "shackling_potion", false),
        new("ShipInABottle", EmoteType.PotionShipInABottle, "ship_in_a_bottle", false),
        new("SkillPotion", EmoteType.PotionSkillPotion, "skill_potion", false),
        new("SneckoOil", EmoteType.PotionSneckoOil, "snecko_oil", false),
        new("SoldiersStew", EmoteType.PotionSoldiersStew, "soldiers_stew", false),
        new("SpeedPotion", EmoteType.PotionSpeedPotion, "speed_potion", false),
        new("StableSerum", EmoteType.PotionStableSerum, "stable_serum", false),
        new("StarPotion", EmoteType.PotionStarPotion, "star_potion", false),
        new("StrengthPotion", EmoteType.PotionStrengthPotion, "strength_potion", false),
        new("SwiftPotion", EmoteType.PotionSwiftPotion, "swift_potion", false),
        new("TouchOfInsanity", EmoteType.PotionTouchOfInsanity, "touch_of_insanity", false),
        new("VulnerablePotion", EmoteType.PotionVulnerablePotion, "vulnerable_potion", false),
        new("WeakPotion", EmoteType.PotionWeakPotion, "weak_potion", false),
    ];
    private static readonly IReadOnlyDictionary<string, PotionEmoteDefinition> ByType = Definitions
        .Where(d => d.TypeName != null).ToDictionary(d => d.TypeName!, StringComparer.Ordinal);

    /// <summary>All registered artwork, including entries without a model in either inspected branch.</summary>
    public static IReadOnlyList<PotionEmoteDefinition> All { get; } = Array.AsReadOnly(Definitions);

    /// <summary>Maps a registered content or exact official model type; unknown, null and unregistered types return false/None.</summary>
    public static bool TryGetEmote(Type? modelType, out EmoteType emote)
    {
        if (ModdedEmotes.TryGetEmote(modelType, EmoteCategory.Potion, out emote)) return true;
        if (modelType == null || modelType.Assembly != typeof(PotionModel).Assembly ||
            modelType.Namespace != "MegaCrit.Sts2.Core.Models.Potions" ||
            !typeof(PotionModel).IsAssignableFrom(modelType) || !ByType.TryGetValue(modelType.Name, out var entry))
            return false;
        emote = entry.Emote;
        return true;
    }

    /// <summary>Maps an existing model without creating, modifying or loading any model resources.</summary>
    public static bool TryGetEmote(PotionModel? model, out EmoteType emote) => TryGetEmote(model?.GetType(), out emote);
}
