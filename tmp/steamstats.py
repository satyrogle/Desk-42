import json, sys, urllib.request, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ids = {
    "Lobotomy Corporation": 568220,
    "Library of Ruina": 1256670,
    "Limbus Company": 1973530,
    "Oxygen Not Included": 457140,
    "RimWorld": 294100,
    "Dwarf Fortress": 975370,
    "Songs of Syx": 1162750,
    "IXION": 1113120,
    "Frostpunk": 323190,
    "Frostpunk 2": 1601580,
    "Against the Storm": 1336490,
    "Papers Please": 239030,
    "Do Not Feed the Monkeys": 972270,
    "Rain World": 312520,
}
if len(sys.argv) > 1:
    ids = {}
    for pair in sys.argv[1:]:
        n, i = pair.rsplit(":", 1)
        ids[n] = int(i)

for name, appid in ids.items():
    url = ("https://store.steampowered.com/appreviews/%d?json=1&filter=all&language=all"
           "&review_type=all&purchase_type=all&num_per_page=0&day_range=9223372036854775807" % appid)
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        d = json.load(urllib.request.urlopen(req, timeout=60))["query_summary"]
        tot = d.get("total_reviews", 0)
        pos = d.get("total_positive", 0)
        pct = (100.0 * pos / tot) if tot else 0
        print("%-24s app=%-8d total=%-7d pos=%-7d %.1f%%  %s" % (name, appid, tot, pos, pct, d.get("review_score_desc")))
    except Exception as e:
        print("%-24s app=%-8d ERROR %s" % (name, appid, e))
