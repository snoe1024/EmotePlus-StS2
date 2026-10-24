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
            [EmoteType.RIP] = ["Meme"],
            [EmoteType.Devotion] = ["Communicate", "Meme"],
            
            [EmoteType.Curious] = ["Communicate"],
            [EmoteType.Enrage] = ["Communicate"],
            [EmoteType.HelloWorld] = ["Communicate"],
            [EmoteType.PowerNecroMasteryPower] = ["Communicate"],
            [EmoteType.PowerRagePower] = ["Communicate"],
            [EmoteType.PowerSicEmPower] = ["Communicate"],
            [EmoteType.PowerTagTeamPower] = ["Communicate"],
            [EmoteType.PowerUnmovablePower] = ["Communicate"],
            [EmoteType.PowerWasteAwayPower] = ["Communicate"],
            
            [EmoteType.RelicByrdpip] = ["Creature"],
            [EmoteType.RelicPaelsLegion] = ["Creature"],
            [EmoteType.RelicYummyCookieDefect] = ["Character"],
            [EmoteType.RelicYummyCookieIronclad] = ["Character"],
            [EmoteType.RelicYummyCookieNecro] = ["Character"],
            [EmoteType.RelicYummyCookieRegent] = ["Character"],
            [EmoteType.RelicYummyCookieSilent] = ["Character"],
            
            [EmoteType.MemeThonking] = ["Communicate"],
        };
}
