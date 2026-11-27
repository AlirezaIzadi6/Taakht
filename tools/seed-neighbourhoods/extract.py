#!/usr/bin/env python3
"""One-time extraction script for Tehran and Shiraz neighbourhoods from OpenStreetMap.

Queries Overpass API for `way` and `relation` elements tagged `place=neighbourhood`
inside the administrative boundaries of Tehran and Shiraz, filters out non-neighbourhood
features (parks, airports, universities, squares/intersections), fills missing English
names, computes centres via Overpass `out center` (bounding-box centre for polygons),
and writes `cities.json` and `neighbourhoods.json`.
"""

from __future__ import annotations

import argparse
import json
import re
import time
import unicodedata
import urllib.parse
import urllib.request
from collections import defaultdict
from pathlib import Path

OVERPASS_ENDPOINTS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
]

CITIES = [
    {
        "id": "tehran",
        "prefix": "thr",
        "name_fa": "تهران",
        "name_en": "Tehran",
        "osm_relation_id": 6663864,
    },
    {
        "id": "shiraz",
        "prefix": "shz",
        "name_fa": "شیراز",
        "name_en": "Shiraz",
        "osm_relation_id": 8167754,
    },
]

# Non-neighbourhood prefixes to exclude when tagged place=neighbourhood in OSM.
EXCLUDED_PREFIXES = (
    "میدان ",
    "چهارراه ",
    "تقاطع ",
    "سه راه ",
    "سه‌راه ",
    "بوستان ",
    "فرودگاه ",
    "منطقه هوایی ",
    "دانشگاه ",
    "نمایشگاه ",
    "اراضی ",
)

# Specific non-residential / industrial / cemetery / anomalous entries in OSM.
EXCLUDED_NORMALIZED_FA = {
    "ورزشگاه آزادی",
    "فتح - صنعتی",
    "دارالرحمه",
    "آرد خوشه",
    "پشم شیشه",
    "ریشمک",
    "صنایع الکترونیک",
    "سردخانه فال اسیری",
    "قالیشویی",
    "جنت - ترمینال باربری",
    "بنک داران",
    "تپه تلویزیون",
    "کانادا",
}

# Pilot active neighbourhoods per city (dense, contiguous, transit/locker-accessible cores).
PILOT_SLUGS = {
    "tehran": {
        "jahad",
        "fatemi",
        "amir-abad",
        "yousef-abad",
        "behjat-abad",
        "keshavarz-park-laleh",
        "sanaei",
        "valiasr",
        "saeei",
        "nezami-ganjavi",
        "valfajr",
        "iranshahr",
        "kuy-nasr",
        "shahrara-patris",
        "jamalzadeh",
        "abbas-abad-andisheh",
        "amjadieh",
        "felestin-enghelab",
        "sohrevardi-bagh-saba",
        "tehran-vila",
        "bahar",
        "towhid",
        "sheikh-hadi",
        "jomhuri",
        "sattarkhan",
        "shadman",
        "vanak",
        "ararat",
    },
    "shiraz": {
        "molasadra",
        "rahmat-abad",
        "khold-barin",
        "20-metri-cinema-saadi",
        "neshat",
        "saadi-cinema",
        "afif-abad",
        "abiverdi",
        "valiasr",
        "hava-bord",
        "eram",
        "ghasrodasht",
        "chogia",
        "zand",
        "chanchaneh",
        "jomhuri",
        "bagh-e-takht",
        "zerehi-amirkabir",
        "pasdaran",
    },
}

# Curated English names for OSM neighbourhoods missing or having ambiguous `name:en`.
OVERRIDES_EN = {
    # Tehran (fixes for ambiguous OSM name:en)
    "عباس آباد شهر ری": "Abbas Abad (Shahr-e-Rey)",
    "عباس آباد - اندیشه": "Abbas Abad - Andisheh",
    "اقدسیه شهرری": "Aghdasieh (Shahr-e-Rey)",
    "محله استخر شهرری": "Estakhr (Shahr-e-Rey)",
    "هاشم آباد شهر ری": "Hashem Abad (Shahr-e-Rey)",
    "یافت آباد شمالی": "Yaft Abad Shomali",
    "یافت آباد جنوبی": "Yaft Abad Jonoubi",
    # Tehran (missing name:en)
    "شهرک شهید شهپریان (البرز)": "Shahrak Shahid Shahparian (Alborz)",
    "برومند": "Boroumand",
    "گلستان شمالی": "Golestan Shomali",
    "جمال\u200cزاده": "Jamalzadeh",
    "ملک آباد": "Malek Abad",
    "کوهک": "Kouhak",
    "شهرک شهید رجایی": "Shahrak Shahid Rajaei",
    "شهرک انصار": "Shahrak Ansar",
    "کوی نصر مهرآباد": "Kuye Nasr Mehrabad",
    "پیکان شهر": "Peykan Shahr",
    "قائم چیتگر": "Ghaem Chitgar",
    "شهرک قائم": "Shahrak Ghaem",
    "ده نارمک": "Deh Narmak",
    # Shiraz
    "اردیبهشت غربی": "Ordibehesht Gharbi",
    "شاه داعی الله - دباغ خانه": "Shah Daei Allah - Dabbagh Khaneh",
    "شهرک بهار": "Shahrak Bahar",
    "اصلاح نژاد": "Eslahnejad",
    "قالیشویی": "Ghalishouyi",
    "قبله": "Ghebleh",
    "شهرک مطهری": "Shahrak Motahhari",
    "کوشک میدان": "Koushk Meydan",
    "بهارستان - هجرت": "Baharestan - Hejrat",
    "پشت مله": "Posht Malleh",
    "شهرک مدرس": "Shahrak Modarres",
    "محلاتی": "Mahallati",
    "گویم": "Gouyom",
    "حسینی الهاشمی": "Hosseini Al-Hashemi",
    "دزک": "Dezak",
    "بیست متری سینما سعدی": "20 Metri Cinema Saadi",
    "حجت آباد": "Hojjat Abad",
    "شهرک امیرکبیر": "Shahrak Amirkabir",
    "شهرک فرزانگان": "Shahrak Farzanegan",
    "شهرک گلدشت محمدی": "Shahrak Goldasht Mohammadi",
    "شهرک سبحان": "Shahrak Sobhan",
}

PERSIAN_CHAR_MAP = {
    "ا": "a", "آ": "a", "أ": "a", "إ": "e", "ب": "b", "پ": "p", "ت": "t",
    "ث": "s", "ج": "j", "چ": "ch", "ح": "h", "خ": "kh", "د": "d", "ذ": "z",
    "ر": "r", "ز": "z", "ژ": "zh", "س": "s", "ش": "sh", "ص": "s", "ض": "z",
    "ط": "t", "ظ": "z", "ع": "a", "غ": "gh", "ف": "f", "ق": "gh", "ک": "k",
    "ك": "k", "گ": "g", "ل": "l", "م": "m", "ن": "n", "و": "v", "ه": "h",
    "ة": "h", "ی": "y", "ي": "y", "ئ": "y", "ؤ": "o", "ء": "",
    "۰": "0", "۱": "1", "۲": "2", "۳": "3", "۴": "4",
    "۵": "5", "۶": "6", "۷": "7", "۸": "8", "۹": "9",
}


def normalize_fa(text: str) -> str:
    text = text.strip()
    text = text.replace("ي", "ی").replace("ك", "ک").replace("\u200c", " ")
    text = re.sub(r"\s+", " ", text)
    return text


def dedup_key(text: str) -> str:
    key = normalize_fa(text).replace(" ", "")
    if key == "قصردشت":
        return "قصرالدشت"
    return key


def transliterate_fa(text: str) -> str:
    out = []
    for ch in text:
        if ch in PERSIAN_CHAR_MAP:
            out.append(PERSIAN_CHAR_MAP[ch])
        elif ch.isascii():
            out.append(ch)
        else:
            out.append(" ")
    joined = re.sub(r"\s+", " ", "".join(out)).strip()
    return joined.title()


def slugify(text: str) -> str:
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode("ascii")
    text = text.lower()
    text = re.sub(r"[^a-z0-9]+", "-", text)
    return text.strip("-")


def fetch_overpass(relation_id: int) -> dict:
    area_id = 3600000000 + relation_id
    query = f"""
[out:json][timeout:60];
area({area_id})->.city;
(
  way["place"="neighbourhood"](area.city);
  relation["place"="neighbourhood"](area.city);
);
out center tags;
"""
    body = urllib.parse.urlencode({"data": query}).encode("utf-8")
    for url in OVERPASS_ENDPOINTS:
        for attempt in range(2):
            try:
                req = urllib.request.Request(
                    url,
                    data=body,
                    headers={"User-Agent": "TaakhtSeedScript/1.0"},
                )
                with urllib.request.urlopen(req, timeout=90) as resp:
                    return json.loads(resp.read().decode("utf-8"))
            except Exception as exc:
                print(f"Warning: {url} attempt {attempt + 1} failed: {exc}")
                time.sleep(2)
    raise RuntimeError(f"Failed to fetch Overpass data for relation {relation_id}")


def process_city(city: dict, raw_data: dict) -> list[dict]:
    grouped: dict[str, list[dict]] = defaultdict(list)

    for el in raw_data.get("elements", []):
        tags = el.get("tags", {})
        if tags.get("place") != "neighbourhood":
            continue
        fa = (tags.get("name:fa") or tags.get("name") or "").strip()
        if not re.search(r"[\u0600-\u06FF]", fa):
            continue
        if re.fullmatch(r"[\d۰-۹\s\-]+", fa):
            continue
        if fa.startswith(EXCLUDED_PREFIXES):
            continue
        if normalize_fa(fa) in EXCLUDED_NORMALIZED_FA:
            continue

        center = el.get("center")
        if not center or "lat" not in center or "lon" not in center:
            continue

        en = (
            OVERRIDES_EN.get(fa)
            or OVERRIDES_EN.get(normalize_fa(fa))
            or (tags.get("name:en") or "").strip()
            or transliterate_fa(fa)
        )

        grouped[dedup_key(fa)].append(
            {
                "type": el["type"],
                "name_fa": fa,
                "name_en": en,
                "lat": float(center["lat"]),
                "lon": float(center["lon"]),
            }
        )

    items = []
    used_slugs: set[str] = set()

    for _, entries in grouped.items():
        # Prefer relation metadata over way if both exist; average centres if split.
        entries.sort(key=lambda x: 0 if x["type"] == "relation" else 1)
        primary = entries[0]
        avg_lat = round(sum(e["lat"] for e in entries) / len(entries), 6)
        avg_lon = round(sum(e["lon"] for e in entries) / len(entries), 6)

        raw_slug = slugify(primary["name_en"]) or slugify(transliterate_fa(primary["name_fa"]))
        base_slug = f"{city['prefix']}-{raw_slug}"
        slug = base_slug
        suffix = 2
        while slug in used_slugs:
            slug = f"{base_slug}-{suffix}"
            suffix += 1
        used_slugs.add(slug)

        items.append(
            {
                "id": slug,
                "city_id": city["id"],
                "name_fa": primary["name_fa"],
                "name_en": primary["name_en"],
                "center": {
                    "latitude": avg_lat,
                    "longitude": avg_lon,
                },
                "active": raw_slug in PILOT_SLUGS.get(city["id"], set()),
            }
        )

    items.sort(key=lambda x: x["id"])
    return items


def main() -> int:
    parser = argparse.ArgumentParser(description="Extract Tehran and Shiraz neighbourhoods from OSM.")
    parser.add_argument(
        "--out-dir",
        type=Path,
        default=Path("src/mocks/taxonomy/data"),
        help="Output directory for cities.json and neighbourhoods.json",
    )
    parser.add_argument(
        "--cache-dir",
        type=Path,
        default=None,
        help="Optional directory containing <city>_osm.json cached Overpass responses",
    )
    args = parser.parse_args()

    cities_out = [
        {"id": c["id"], "name_fa": c["name_fa"], "name_en": c["name_en"]}
        for c in CITIES
    ]

    neighbourhoods_out: list[dict] = []
    for city in CITIES:
        if args.cache_dir is not None:
            cache_file = args.cache_dir / f"{city['id']}_osm.json"
            if cache_file.exists():
                raw_data = json.loads(cache_file.read_text(encoding="utf-8"))
            else:
                raw_data = fetch_overpass(city["osm_relation_id"])
                args.cache_dir.mkdir(parents=True, exist_ok=True)
                cache_file.write_text(json.dumps(raw_data, ensure_ascii=False, indent=2), encoding="utf-8")
        else:
            raw_data = fetch_overpass(city["osm_relation_id"])

        city_items = process_city(city, raw_data)
        print(f"{city['id']}: extracted {len(city_items)} neighbourhoods")
        neighbourhoods_out.extend(city_items)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    (args.out_dir / "cities.json").write_text(
        json.dumps(cities_out, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (args.out_dir / "neighbourhoods.json").write_text(
        json.dumps(neighbourhoods_out, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {len(cities_out)} cities and {len(neighbourhoods_out)} neighbourhoods to {args.out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
