"""Validate fixed optional-mod registrations without running mod assemblies (Python standard library only)."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CATEGORIES = {"Character", "Creature", "Relic", "Power", "Potion", "Enchantment", "Ancient"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workshop-root", type=Path, help="Optional STS2 Workshop content directory; verify declared type names with ilspycmd.")
    parser.add_argument("--require-assets", action="store_true", help="Fail if any icon or outline has not been supplied yet.")
    parser.add_argument("--ilspy", default=str(Path.home() / ".dotnet/tools/ilspycmd.exe") if os.name == "nt" else "ilspycmd")
    args = parser.parse_args()
    enums = re.findall(r"^\s*(\w+)\s*=\s*(\d+),", (ROOT / "EmotePlusCode/Reaction/EmoteType.cs").read_text(encoding="utf-8-sig"), re.M)
    ids = [int(value) for _, value in enums]
    assert len(ids) == len(set(ids)), "Duplicate enum values"
    seen_ids, names, assets, models = set(), set(), set(), set()
    type_cache = {}
    folders = dict(Character="characters", Creature="creatures", Relic="relics", Power="powers", Potion="potions", Enchantment="enchantments", Ancient="ancients")
    missing = []
    for file in sorted((ROOT / "EmotePlusCode/ContentMods").glob("*.json")):
        group = json.loads(file.read_text(encoding="utf-8-sig"))
        assert file.stem == group["Key"] and group["ModIds"], file
        for character in (e for e in group["Entries"] if e["Category"] == "Character"):
            assert any(e.get("CharacterCookie") and e["Assembly"] == character["Assembly"] and e["TypeName"] == character["TypeName"] for e in group["Entries"]), f'Missing character cookie: {group["Key"]}/{character["Name"]}'
        for entry in group["Entries"]:
            label = f'{group["Key"]}/{entry["Name"]}'
            assert 0 < entry["Id"] <= 2147483647 and entry["Id"] in ids, f"Missing/invalid enum: {label}"
            assert entry["Id"] not in seen_ids and entry["Name"] not in names, f"Duplicate identity: {label}"
            seen_ids.add(entry["Id"]); names.add(entry["Name"])
            assert entry["Category"] in CATEGORIES, label
            asset = entry["AssetName"]
            assert asset.startswith(f'mods/{group["Key"]}/') and ".." not in asset and "\\" not in asset, label
            identity = (entry["Category"], asset)
            assert identity not in assets, f"Duplicate artwork: {label}"
            assets.add(identity)
            assert not entry.get("CharacterCookie") or entry["Category"] == "Relic", label
            model = (entry["Assembly"], entry["TypeName"], entry["Category"], entry.get("CharacterCookie", False))
            assert model not in models, f"Duplicate model: {label}"
            models.add(model)
            for side in ("icons", "outlines"):
                path = ROOT / "EmotePlus/ui/emote" / side / folders[entry["Category"]] / (asset + ".png")
                if not path.is_file(): missing.append(str(path.relative_to(ROOT)))
            if args.workshop_root:
                dll = args.workshop_root / group["WorkshopId"] / (entry["Assembly"] + ".dll")
                assert dll.is_file(), f"Missing installed DLL: {dll}"
                if dll not in type_cache:
                    output = subprocess.run([args.ilspy, "-l", "c", str(dll)], check=True, capture_output=True, text=True, encoding="utf-8").stdout
                    type_cache[dll] = set(re.findall(r"^Class (.+)$", output, re.M))
                assert entry["TypeName"] in type_cache[dll], f"Missing exact type: {label}"
    print(f"PASS: {len(seen_ids)} optional emotes; unique IDs, names, models and asset routes")
    if missing:
        print("Pending artwork:\n" + "\n".join(missing))
        assert not args.require_assets, "Supply both images before releasing"


if __name__ == "__main__":
    main()
