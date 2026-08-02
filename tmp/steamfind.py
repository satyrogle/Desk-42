import json, sys, urllib.request, urllib.parse, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

for term in sys.argv[1:]:
    url = ("https://store.steampowered.com/api/storesearch/?term=%s&l=en&cc=us"
           % urllib.parse.quote(term))
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        d = json.load(urllib.request.urlopen(req, timeout=40))
        print("== %s" % term)
        for it in d.get("items", [])[:4]:
            print("   %-8s %s" % (it.get("id"), it.get("name")))
    except Exception as e:
        print("== %s ERROR %s" % (term, e))
