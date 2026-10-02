namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>
/// EmotePlus's own emote ids (sent as one byte). The vanilla ReactionType cannot be extended: the game writes an
/// enum with just enough bits for its largest DEFINED value, so an extra value cast into it would be cut off.
/// 1-8 are the vanilla emotes with the same numbers as ReactionType, so converting is a plain int cast;
/// new emotes get new numbers (never reuse or renumber: the value goes over the network).
/// </summary>
public enum EmoteType
{
    None = 0,
    Exclamation = 1,
    Skull = 2,
    ThumbDown = 3,
    SadSlime = 4,
    QuestionMark = 5,
    Heart = 6,
    ThumbUp = 7,
    HappyCultist = 8,
    Darv = 9,
    Neow = 10,
    Nonupeipe = 11,
    Orobas = 12,
    Pael = 13,
    Tanx = 14,
    Tezcatara = 15,
    Vakuu = 16,
    Curious = 17,
    Enrage = 18,
    HelloWorld = 19,
    Vulnerable = 20,
    Weak = 21,
    Gold = 22,
    RIP = 23,
    Devotion = 24,
}
