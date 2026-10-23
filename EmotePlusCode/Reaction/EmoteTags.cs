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
            [EmoteType.SadSlime] = ["Communicate"],
            [EmoteType.QuestionMark] = ["Communicate"],
            [EmoteType.Heart] = ["Communicate"],
            [EmoteType.ThumbUp] = ["Communicate"],
            [EmoteType.HappyCultist] = ["Meme"],
            [EmoteType.Darv] = ["Ancient", "Meme"],
            [EmoteType.Neow] = ["Ancient", "Meme"],
            [EmoteType.Nonupeipe] = ["Ancient", "Meme"],
            [EmoteType.Orobas] = ["Ancient", "Meme"],
            [EmoteType.Pael] = ["Ancient", "Meme"],
            [EmoteType.Tanx] = ["Ancient", "Meme"],
            [EmoteType.Tezcatara] = ["Ancient", "Meme"],
            [EmoteType.Vakuu] = ["Ancient", "Meme"],
            [EmoteType.Curious] = ["Meme"],
            [EmoteType.Enrage] = ["Meme"],
            [EmoteType.HelloWorld] = ["Meme"],
            [EmoteType.Vulnerable] = ["Meme"],
            [EmoteType.Weak] = ["Meme"],
            [EmoteType.Gold] = ["Meme"],
            [EmoteType.RIP] = ["Meme"],
            [EmoteType.Devotion] = ["Communicate"],
            [EmoteType.RelicYummyCookieDefect] = ["Character"],
            [EmoteType.RelicYummyCookieIronclad] = ["Character"],
            [EmoteType.RelicYummyCookieNecro] = ["Character"],
            [EmoteType.RelicYummyCookieRegent] = ["Character"],
            [EmoteType.RelicYummyCookieSilent] = ["Character"],
        };
}
