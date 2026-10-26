namespace EmotePlus.EmotePlusCode.Reaction;

// Search tags may overlap; they never determine artwork directories. Strings must match EmoteSearch.Tags.
internal static class EmoteTags
{
    internal static readonly IReadOnlyDictionary<EmoteType, string[]> ByEmote =
        new Dictionary<EmoteType, string[]>
        {
            [EmoteType.Exclamation] = ["Communicate"],
            [EmoteType.Skull] = ["Meme"],
            [EmoteType.ThumbDown] = ["Communicate"],
            [EmoteType.SadSlime] = ["Communicate", "Meme"],
            [EmoteType.QuestionMark] = ["Communicate"],
            [EmoteType.Heart] = ["Communicate"],
            [EmoteType.ThumbUp] = ["Communicate"],
            [EmoteType.HappyCultist] = ["Meme"],
            [EmoteType.Gold] = ["Communicate"],
            
            [EmoteType.PowerCurious] = ["Communicate"],
            [EmoteType.PowerEnrage] = ["Communicate"],
            [EmoteType.HelloWorld] = ["Communicate"],
            [EmoteType.PowerNecroMastery] = ["Communicate"],
            [EmoteType.PowerRage] = ["Communicate"],
            [EmoteType.PowerSicEm] = ["Communicate"],
            [EmoteType.PowerTagTeam] = ["Communicate"],
            [EmoteType.PowerUnmovable] = ["Communicate"],
            [EmoteType.PowerWasteAway] = ["Communicate"],
            
            [EmoteType.RelicByrdpip] = ["Creature"],
            [EmoteType.RelicPaelsLegion] = ["Creature"],
            
            // DO NOT DELETE: modded character's cookie has different route to add Character tag.
            [EmoteType.RelicYummyCookieDefect] = ["Character"],
            [EmoteType.RelicYummyCookieIronclad] = ["Character"],
            [EmoteType.RelicYummyCookieNecro] = ["Character"],
            [EmoteType.RelicYummyCookieRegent] = ["Character"],
            [EmoteType.RelicYummyCookieSilent] = ["Character"],
            
            [EmoteType.MemeDevotion] = ["Communicate"],
            [EmoteType.MemeThonking] = ["Communicate"],
        };
}
