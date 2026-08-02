import sys, io, re, html, urllib.parse, subprocess, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/121.0 Safari/537.36")


def clean(s):
    return html.unescape(re.sub(r"<.*?>", " ", s)).strip()


def unwrap(link):
    link = html.unescape(link)
    m = re.search(r"uddg=([^&]+)", link)
    if m:
        link = urllib.parse.unquote(m.group(1))
    return link


def try_html(q):
    out = subprocess.run(["curl", "-s", "--max-time", "40", "-A", UA,
                          "-d", "q=" + urllib.parse.quote(q), "-d", "kl=us-en",
                          "https://html.duckduckgo.com/html/"], capture_output=True)
    page = out.stdout.decode("utf-8", "replace")
    res = re.findall(r'<a rel="nofollow" class="result__a" href="(.*?)">(.*?)</a>', page, re.S)
    snips = re.findall(r'class="result__snippet".*?>(.*?)</a>', page, re.S)
    return [(unwrap(l), clean(t), clean(snips[i])[:300] if i < len(snips) else "")
            for i, (l, t) in enumerate(res)]


def try_lite(q):
    out = subprocess.run(["curl", "-s", "--max-time", "40", "-A", UA,
                          "-d", "q=" + urllib.parse.quote(q),
                          "https://lite.duckduckgo.com/lite/"], capture_output=True)
    page = out.stdout.decode("utf-8", "replace")
    res = re.findall(r'<a[^>]*class="result-link"[^>]*href="(.*?)"[^>]*>(.*?)</a>', page, re.S)
    if not res:
        res = re.findall(r'<a[^>]*href="(https?://[^"]+)"[^>]*class="result-link"[^>]*>(.*?)</a>', page, re.S)
    snips = re.findall(r'class="result-snippet">(.*?)</td>', page, re.S)
    return [(unwrap(l), clean(t), clean(snips[i])[:300] if i < len(snips) else "")
            for i, (l, t) in enumerate(res)]


q = " ".join(sys.argv[1:])
results = []
for fn in (try_html, try_lite, try_html):
    try:
        results = fn(q)
    except Exception as e:
        print("ERR", e)
    if results:
        break
    time.sleep(4)

if not results:
    print("NO RESULTS")
for i, (l, t, s) in enumerate(results[:12]):
    print("[%d] %s\n    %s\n    %s" % (i, t, l, s))
