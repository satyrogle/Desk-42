import sys, io, re, html, subprocess
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

url = sys.argv[1]
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 12000
skip = int(sys.argv[3]) if len(sys.argv) > 3 else 0

out = subprocess.run(["curl", "-sL", "--max-time", "50", "--compressed", "-A",
                      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0 Safari/537.36",
                      "-H", "Accept-Language: en-US,en;q=0.9", url], capture_output=True)
page = out.stdout.decode("utf-8", "replace")
page = re.sub(r"(?is)<(script|style|noscript|svg|head)[^>]*>.*?</\1>", " ", page)
page = re.sub(r"(?is)<!--.*?-->", " ", page)
page = re.sub(r"(?i)</(p|div|li|h[1-6]|tr|br)>", "\n", page)
page = re.sub(r"(?i)<br\s*/?>", "\n", page)
page = re.sub(r"<[^>]+>", " ", page)
page = html.unescape(page)
page = re.sub(r"[ \t\r\f\v]+", " ", page)
page = re.sub(r"\n\s*\n+", "\n", page)
print(page[skip:skip + limit])
