"""Validate fixed optional-mod registrations without running mod assemblies (Python standard library only)."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
MODEL_CATEGORIES = {"Character", "Creature", "Relic", "Power", "Potion", "Enchantment", "Ancient", "Modifier"}
CATEGORIES = MODEL_CATEGORIES | {"None"}
CATEGORY_BLOCKS = {"None": 0, "Power": 1, "Relic": 2, "Enchantment": 3, "Potion": 4, "Character": 5, "Creature": 6, "Ancient": 7, "Modifier": 8}


def workshop_ids(group):
    sources = group.get("WorkshopIds", []) or []
    assert isinstance(sources, list), "WorkshopIds must be an array"
    singular = group.get("WorkshopId")
    sources = list(dict.fromkeys(sources + ([singular] if singular else [])))
    assert sources and all(isinstance(i, str) and i.isascii() and i.isdecimal() and 0 < int(i) <= 18446744073709551615 for i in sources), "Invalid Workshop IDs"
    return sources


def entry_workshop_id(group, entry):
    sources = workshop_ids(group)
    source = entry.get("WorkshopId")
    assert source is not None or len(sources) == 1, f'Missing entry WorkshopId: {entry["Name"]}'
    source = sources[0] if source is None else source
    assert source in sources, f'Entry WorkshopId outside group: {entry["Name"]}'
    return source


def load_assembly_selections(path):
    selections = json.loads(path.read_text(encoding="utf-8-sig"))
    assert isinstance(selections, dict), "Assembly selections must be an object"
    for source, assemblies in selections.items():
        assert source.isascii() and source.isdecimal() and int(source) > 0 and isinstance(assemblies, dict), "Invalid assembly selection source"
        for assembly, choice in assemblies.items():
            assert isinstance(assembly, str) and isinstance(choice, dict), "Invalid assembly selection"
            assert isinstance(choice.get("path"), str) and isinstance(choice.get("sha256"), str), "Assembly selection requires path and sha256"
            assert re.fullmatch(r"[0-9a-fA-F]{64}", choice["sha256"]), "Invalid selection SHA256"
    return selections


def verify_model(workshop_root, source, entry, ilspy, type_cache, assembly_selections=None):
    # Workshop roots can contain nested character DLLs, and unrelated items can share a DLL filename.
    folder = workshop_root / source
    choice = (assembly_selections or {}).get(source, {}).get(entry["Assembly"])
    if choice is not None:
        relative = choice["path"]
        assert relative and "\\" not in relative and ":" not in relative, "Use a Workshop-relative path with forward slashes"
        path = Path(relative)
        assert not path.is_absolute() and ".." not in path.parts and path.name == entry["Assembly"] + ".dll", "Invalid selected DLL path"
        dll = (folder / path).resolve()
        assert dll.is_relative_to(folder.resolve()) and dll.is_file(), f"Selected DLL missing or outside Workshop item: {dll}"
        if dll not in type_cache:
            assert hashlib.sha256(dll.read_bytes()).hexdigest() == choice["sha256"].lower(), f"Selected DLL changed; recheck version and selection: {dll}"
        dlls = [dll]
    else:
        dlls = sorted(folder.rglob(entry["Assembly"] + ".dll")) if folder.is_dir() else []
    assert dlls, f'Missing installed DLL: {folder}/{entry["Assembly"]}.dll'
    matches = []
    for dll in dlls:
        if dll not in type_cache:
            output = subprocess.run([ilspy, "-l", "c", str(dll)], check=True, capture_output=True, text=True, encoding="utf-8").stdout
            type_cache[dll] = set(re.findall(r"^Class (.+)$", output, re.M))
        if entry["TypeName"] in type_cache[dll]:
            matches.append(dll)
    assert len(matches) == 1, f'Missing or ambiguous exact type: {source}/{entry["TypeName"]}: {matches}'
    return matches[0]


def entry_models(group, entry):
    """Enumerate exact model identities, keeping Workshop provenance per assembly alias."""
    aliases = entry.get("TypeAliases", []) or []
    extra = entry.get("ModelAliases", []) or []
    assert isinstance(aliases, list) and isinstance(extra, list), "Model aliases must be arrays"
    assert not (aliases or extra) or not (entry.get("CharacterCookie") or entry["Category"] == "Character"), "Character/cookie aliases not supported"
    source = entry_workshop_id(group, entry)
    result = [dict(Assembly=entry["Assembly"], TypeName=n, WorkshopId=source) for n in [entry["TypeName"]] + aliases]
    for model in extra:
        assert isinstance(model, dict), "ModelAliases entries must be objects"
        chosen_source = source if model.get("WorkshopId") is None else model["WorkshopId"]
        entry_workshop_id(group, dict(Name=entry["Name"], WorkshopId=chosen_source))
        result.append(dict(Assembly=model.get("Assembly"), TypeName=model.get("TypeName"), WorkshopId=chosen_source))
    for model in result:
        assert isinstance(model["Assembly"], str) and model["Assembly"].strip(), "Invalid model assembly"
        assert isinstance(model["TypeName"], str) and model["TypeName"].strip(), "Invalid type name"
    return result


def validate_fixed_routes(group, enum_ids, models):
    """Validate reuse routes independently of catalogue IDs/assets; share the normal model conflict set."""
    routes = group.get("FixedRoutes", [])
    assert isinstance(routes, list), "FixedRoutes must be an array"
    for route in routes:
        label = f'{group["Key"]}/{route.get("TypeName", "<missing>")}'
        category = route.get("Category")
        target = route.get("TargetId")
        assert category in MODEL_CATEGORIES - {"Character"}, f"Unsupported fixed-route category: {label}"
        assert type(target) is int and 0 < target < 10000 and target in enum_ids, f"Fixed route requires an official target ID: {label}"
        assert target // 1000 == CATEGORY_BLOCKS[category], f"Fixed-route target category mismatch: {label}"
        assert isinstance(route.get("Assembly"), str) and route["Assembly"].strip(), f"Missing fixed-route assembly: {label}"
        assert isinstance(route.get("TypeName"), str) and route["TypeName"].strip(), f"Missing fixed-route type: {label}"
        entry_workshop_id(group, dict(route, Name=route["TypeName"]))
        binding = category if route.get("BindingCategory") is None else route["BindingCategory"]
        assert binding in MODEL_CATEGORIES - {"Character"}, f"Unsupported fixed-route binding category: {label}"
        model = (route["Assembly"], route["TypeName"], binding, False)
        assert model not in models, f"Duplicate model: {label}"
        models.add(model)
    return routes


def validate_image_overrides(group, registered_ids, assets, guards):
    """Image-only skin rules never allocate a new identity or require a model assembly."""
    rules = group.get("ImageOverrides", [])
    assert isinstance(rules, list), "ImageOverrides must be an array"
    for rule in rules:
        target, category = rule.get("TargetId"), rule.get("Category")
        label = f'{group["Key"]}/image-override/{target}'
        assert type(target) is int and target in registered_ids, f"Override target is not registered: {label}"
        assert category in CATEGORY_BLOCKS or category == "Meme", f"Invalid override category: {label}"
        block = 9 if category == "Meme" else CATEGORY_BLOCKS[category]
        assert target % 10000 // 1000 == block, f"Override category mismatch: {label}"
        asset, source, fingerprint = rule.get("AssetName"), rule.get("SourceResource"), rule.get("SourceSha256")
        assert isinstance(asset, str) and asset.startswith(f'mods/{group["Key"]}/') and ".." not in asset and "\\" not in asset and ":" not in asset, f"Invalid override asset: {label}"
        assert isinstance(source, str) and source.startswith("res://") and ".." not in source and "\\" not in source, f"Invalid override probe: {label}"
        assert isinstance(fingerprint, str) and re.fullmatch(r"[0-9a-fA-F]{64}", fingerprint), f"Invalid override fingerprint: {label}"
        match = rule.get("Match", "ResourceFile")
        assert match in ("ResourceFile", "CharacterIcon"), f"Invalid override match: {label}"
        if match == "CharacterIcon":
            assert category == "Character" and type(rule.get("Width")) is int and rule["Width"] > 0 and type(rule.get("Height")) is int and rule["Height"] > 0, f"Invalid icon dimensions: {label}"
        guard = (target, source, fingerprint.lower())
        assert guard not in guards, f"Duplicate override guard: {label}"
        guards.add(guard)
        identity = (category, asset)
        assert identity not in assets, f"Duplicate override artwork: {label}"
        assets.add(identity)
    return rules


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workshop-root", type=Path, help="Optional STS2 Workshop content directory; verify declared type names with ilspycmd.")
    parser.add_argument("--require-assets", action="store_true", help="Fail if any icon or outline has not been supplied yet.")
    parser.add_argument("--assembly-selections", type=Path, help="JSON selecting a Workshop-relative DLL and SHA256 by Workshop ID and assembly name.")
    parser.add_argument("--ilspy", default=str(Path.home() / ".dotnet/tools/ilspycmd.exe") if os.name == "nt" else "ilspycmd")
    args = parser.parse_args()
    assert not args.assembly_selections or args.workshop_root, "--assembly-selections requires --workshop-root"
    selections = load_assembly_selections(args.assembly_selections) if args.assembly_selections else {}
    enums = re.findall(r"^\s*(\w+)\s*=\s*(\d+),", (ROOT / "EmotePlusCode/Reaction/EmoteType.cs").read_text(encoding="utf-8-sig"), re.M)
    ids = [int(value) for _, value in enums]
    cookie_ids = {int(value) for name, value in enums if name in {"RelicYummyCookieDefect", "RelicYummyCookieIronclad", "RelicYummyCookieNecro", "RelicYummyCookieRegent", "RelicYummyCookieSilent"}}
    assert len(ids) == len(set(ids)), "Duplicate enum values"
    seen_ids, names, assets, models = set(), set(), set(), set()
    type_cache = {}
    folders = dict(Character="characters", Creature="creatures", Relic="relics", Power="powers", Potion="potions", Enchantment="enchantments", Ancient="ancients", Modifier="modifiers")
    folders["None"] = ""
    missing = []
    files = sorted((ROOT / "EmotePlusCode/ContentMods").glob("*.json"))
    groups = [json.loads(file.read_text(encoding="utf-8-sig")) for file in files]
    for group in groups:
        by_id = {e["Id"]: e for e in group["Entries"]}
        for entry in group["Entries"]:
            if entry.get("ArtworkEmoteId") is None: continue
            source = by_id.get(entry["ArtworkEmoteId"])
            assert source is not None and source is not entry and source.get("ArtworkEmoteId") is None and source["AssetName"] == entry["AssetName"] and not entry.get("CharacterCookie") and entry["Category"] != "Character", f'Invalid artwork reference: {entry["Name"]}'
    enum_ids = {name: int(value) for name, value in enums}
    official = set()
    for name in ("NoneEmotes", "PowerEmotes", "RelicEmotes", "EnchantmentEmotes", "PotionEmotes", "CharacterEmotes", "CreatureEmotes", "AncientEmotes", "ModifierEmotes", "MemeEmotes"):
        text = (ROOT / "EmotePlusCode/Reaction" / (name + ".cs")).read_text(encoding="utf-8-sig")
        official.update(enum_ids[n] for n in re.findall(r"EmoteType\.(\w+)", text) if n in enum_ids and n != "None")
    registered = official | {e["Id"] for g in groups for e in g["Entries"]}
    override_guards = set()
    for file, group in zip(files, groups):
        assert file.stem == group["Key"] and group["ModIds"], file
        workshop_ids(group)
        for character in (e for e in group["Entries"] if e["Category"] == "Character"):
            dedicated = any(e.get("CharacterCookie") and e["Assembly"] == character["Assembly"] and e["TypeName"] == character["TypeName"] for e in group["Entries"])
            assert dedicated != (character.get("CookieEmoteId") is not None), f'Exactly one cookie definition or reuse route required: {group["Key"]}/{character["Name"]}'
        for entry in group["Entries"]:
            source = entry_workshop_id(group, entry)
            label = f'{group["Key"]}/{entry["Name"]}'
            assert 10000 <= entry["Id"] <= 2147483647 and entry["Id"] in ids, f"Missing/invalid enum: {label}"
            assert entry["Id"] not in seen_ids and entry["Name"] not in names, f"Duplicate identity: {label}"
            seen_ids.add(entry["Id"]); names.add(entry["Name"])
            assert entry["Category"] in CATEGORIES, label
            binding = entry["Category"] if entry.get("BindingCategory") is None else entry["BindingCategory"]
            assert binding in MODEL_CATEGORIES, f"Missing/invalid model binding category: {label}"
            assert entry["Category"] == "None" or binding == entry["Category"] or (entry["Category"] == "Modifier" and binding == "Relic"), f"Invalid artwork/model binding: {label}"
            assert binding != "Character" or entry["Category"] == "Character", f"Character binding requires Character artwork: {label}"
            assert entry["Id"] % 10000 // 1000 == CATEGORY_BLOCKS[entry["Category"]], f"ID outside category range: {label}"
            asset = entry["AssetName"]
            assert asset.startswith(f'mods/{group["Key"]}/') and ".." not in asset and "\\" not in asset, label
            identity = (entry["Category"], asset)
            if entry.get("ArtworkEmoteId") is None:
                assert identity not in assets, f"Duplicate artwork: {label}"
                assets.add(identity)
            assert not entry.get("CharacterCookie") or entry["Category"] == "Relic", label
            if entry.get("CookieEmoteId") is not None:
                assert entry["Category"] == "Character" and type(entry["CookieEmoteId"]) is int and entry["CookieEmoteId"] in cookie_ids, f"Invalid cookie reuse target: {label}"
            bindings = entry_models(group, entry)
            for exact in bindings:
                model = (exact["Assembly"], exact["TypeName"], binding, entry.get("CharacterCookie", False))
                assert model not in models, f"Duplicate model: {label}/{exact['TypeName']}"
                models.add(model)
            for side in ("icons", "outlines"):
                source = next((e for e in group["Entries"] if e["Id"] == entry.get("ArtworkEmoteId")), entry)
                path = ROOT / "EmotePlus/ui/emote" / side / folders[source["Category"]] / (source["AssetName"] + ".png")
                if not path.is_file(): missing.append(str(path.relative_to(ROOT)))
            if args.workshop_root:
                for exact in bindings:
                    verify_model(args.workshop_root, exact["WorkshopId"], exact, args.ilspy, type_cache, selections)
        for route in validate_fixed_routes(group, ids, models):
            if args.workshop_root:
                source = entry_workshop_id(group, dict(route, Name=route["TypeName"]))
                verify_model(args.workshop_root, source, route, args.ilspy, type_cache, selections)
        for rule in validate_image_overrides(group, registered, assets, override_guards):
            for side in ("icons", "outlines"):
                folder = "memes" if rule["Category"] == "Meme" else folders[rule["Category"]]
                path = ROOT / "EmotePlus/ui/emote" / side / folder / (rule["AssetName"] + ".png")
                if not path.is_file(): missing.append(str(path.relative_to(ROOT)))
    print(f"PASS: {len(seen_ids)} optional emotes; {len(override_guards)} image overrides; unique IDs, names, models and asset routes")
    if missing:
        print("Pending artwork:\n" + "\n".join(missing))
        assert not args.require_assets, "Supply both images before releasing"


if __name__ == "__main__":
    main()
