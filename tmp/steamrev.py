import json, sys, urllib.request, urllib.parse, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

appid = sys.argv[1]
rtype = sys.argv[2] if len(sys.argv) > 2 else "negative"
num = int(sys.argv[3]) if len(sys.argv) > 3 else 40
maxlen = int(sys.argv[4]) if len(sys.argv) > 4 else 900
filt = sys.argv[5] if len(sys.argv) > 5 else "all"

url = ("https://store.steampowered.com/appreviews/%s?json=1&filter=%s&language=english"
       "&review_type=%s&purchase_type=all&num_per_page=%d&day_range=9223372036854775807"
       % (appid, filt, rtype, min(num, 100)))
req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
data = json.load(urllib.request.urlopen(req, timeout=60))
print("num_reviews:", data["query_summary"].get("num_reviews"))
qs = data["query_summary"]
if "total_positive" in qs:
    print("total_pos/neg:", qs.get("total_positive"), qs.get("total_negative"), qs.get("review_score_desc"))
for i, r in enumerate(data["reviews"]):
    t = r["review"].replace("\n", " ").replace("\r", " ")
    print("--- [%d] hrs=%d votes_up=%d" % (i, r["author"]["playtime_at_review"] // 60, r.get("votes_up", 0)))
    print(t[:maxlen])
